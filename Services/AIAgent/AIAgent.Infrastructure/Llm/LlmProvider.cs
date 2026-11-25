using System.Net.Http.Json;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgent.Infrastructure.Llm;

public class LlmProvider : ILlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LlmProvider> _logger;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly object? _thinking;

    public LlmProvider(HttpClient httpClient, IConfiguration configuration, ILogger<LlmProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["AI:ApiKey"] ?? configuration["GEMINI_API_KEY"];
        _model = configuration["AI:Model"] ?? DefaultModel;
        var level = configuration["AI:ThinkingLevel"]?.Trim().ToLowerInvariant();
        if (level is "minimal" or "low" or "medium" or "high")
            _thinking = new { thinkingLevel = level };
        else if (int.TryParse(configuration["AI:ThinkingBudget"], out var budget) && budget >= -1)
            _thinking = new { thinkingBudget = budget };
        if (int.TryParse(configuration["AI:RequestTimeoutSeconds"], out var seconds) && seconds > 0)
            AttemptTimeout = TimeSpan.FromSeconds(seconds);
        if (int.TryParse(configuration["AI:TotalTimeoutSeconds"], out var total) && total > 0)
            Budget = TimeSpan.FromSeconds(total);
    }

    
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(40);

    
    public TimeSpan Budget { get; set; } = TimeSpan.FromSeconds(80);

    
    
    private const string DefaultModel = "gemini-flash-latest";
    private const string MissingThoughtSignature = "skip_thought_signature_validator";
    private const string GeminiBase = "https://generativelanguage.googleapis.com/v1beta/models/";

    
    private async Task<HttpResponseMessage> PostGenerateAsync(string model, object body, TimeSpan timeout, CancellationToken cancellationToken, bool stream = false)
    {
        var action = stream ? "streamGenerateContent?alt=sse" : "generateContent";
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{GeminiBase}{Uri.EscapeDataString(model)}:{action}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("x-goog-api-key", _apiKey);

        
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            
            return await _httpClient.SendAsync(request, stream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead, limit.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Model {Model} did not answer within {Seconds:0} seconds.", model, timeout.TotalSeconds);
            return new HttpResponseMessage(System.Net.HttpStatusCode.RequestTimeout) { Content = new StringContent("{}") };
        }
    }

    private const string AlternateModel = "gemini-flash-lite-latest";
    
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(1500);

    private async Task<HttpResponseMessage> PostWithRecoveryAsync(string requestedModel, object body, CancellationToken cancellationToken, bool stream = false)
    {
        var candidates = new[] { requestedModel, _model, AlternateModel }
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        HttpResponseMessage? last = null;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < candidates.Count; i++)
        {
            var model = candidates[i];
            for (var attempt = 0; attempt < 2; attempt++)
            {
                var remaining = Budget - clock.Elapsed;
                if (remaining <= TimeSpan.Zero && last != null) return last;

                last?.Dispose();
                last = await PostGenerateAsync(model, body, remaining < AttemptTimeout ? remaining : AttemptTimeout, cancellationToken, stream);

                var status = (int)last.StatusCode;
                if (last.IsSuccessStatusCode) return last;

                var isLastModel = i == candidates.Count - 1;
                var overloaded = status is 500 or 502 or 503 or 504;
                if (overloaded && attempt == 0)
                {
                    _logger.LogWarning("Model {Model} answered {Status}; retrying once.", model, status);
                    if (RetryDelay > TimeSpan.Zero) await Task.Delay(RetryDelay, cancellationToken);
                    continue;
                }

                
                var movesOn = status is 404 or 408 or 429 || overloaded;
                if (!movesOn || isLastModel) return last;

                _logger.LogWarning("Model {Model} is unavailable ({Status}); trying {Next}.", model, status, candidates[i + 1]);
                break;
            }
        }

        return last!;
    }

    
    private static string FailureReason(System.Net.HttpStatusCode status) => status switch
    {
        System.Net.HttpStatusCode.RequestTimeout => "The language model took too long to answer.",
        System.Net.HttpStatusCode.TooManyRequests =>
            "The AI model has used up its quota for now (HTTP 429). It resets on its own, so try again later or choose another model.",
        _ => $"The language model returned HTTP {(int)status}."
    };

    private static LlmChatResponse Failed(string reason) => new()
    {
        Content = LlmChatResponse.UnexpectedErrorMessage,
        FallbackReason = reason
    };

    public async Task<LlmChatResponse> ChatAsync(
        List<AgentMessage> messages,
        IEnumerable<IAgentTool>? availableTools = null,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        var targetModel = !string.IsNullOrWhiteSpace(model) ? model.Trim() : _model;

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("No Gemini API key configured. The request will fail with a generic message.");
            return Failed("No language-model key is configured.");
        }

        try
        {
            var requestBody = BuildChatBody(messages, availableTools);

            
            
            var response = await PostWithRecoveryAsync(targetModel, requestBody, cancellationToken);

            using (response)
            {
                if (!response.IsSuccessStatusCode)
                {
                    var errorStr = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Gemini API call failed with status {StatusCode}: {Error}. The request will fail with a generic message.", response.StatusCode, errorStr);
                    return Failed(FailureReason(response.StatusCode));
                }

                return ParseChatResponse(await response.Content.ReadAsStringAsync(cancellationToken));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception communicating with Gemini. The request will fail with a generic message.");
            return Failed("The language model could not be reached or returned an unreadable reply.");
        }
    }

    private Dictionary<string, object?> BuildChatBody(List<AgentMessage> messages, IEnumerable<IAgentTool>? availableTools)
    {
        
        
        var geminiTools = BuildGeminiTools(availableTools);
        var (systemText, contents) = BuildGeminiContents(messages);

        var requestBody = new Dictionary<string, object?>
        {
            ["contents"] = contents
        };

        if (!string.IsNullOrWhiteSpace(systemText))
        {
            requestBody["systemInstruction"] = new
            {
                parts = new[] { new { text = systemText } }
            };
        }

        if (geminiTools != null && geminiTools.Count > 0)
        {
            requestBody["tools"] = geminiTools;
        }

        if (_thinking != null)
        {
            requestBody["generationConfig"] = new { thinkingConfig = _thinking };
        }

        return requestBody;
    }

    public async Task<LlmChatResponse> ChatStreamAsync(
        List<AgentMessage> messages,
        IEnumerable<IAgentTool>? availableTools,
        string? model,
        Func<string, Task> onText,
        CancellationToken cancellationToken = default)
    {
        var targetModel = !string.IsNullOrWhiteSpace(model) ? model.Trim() : _model;

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("No Gemini API key configured. The request will fail with a generic message.");
            return Failed("No language-model key is configured.");
        }

        try
        {
            var requestBody = BuildChatBody(messages, availableTools);
            using var response = await PostWithRecoveryAsync(targetModel, requestBody, cancellationToken, stream: true);

            if (!response.IsSuccessStatusCode)
            {
                var errorStr = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Gemini API call failed with status {StatusCode}: {Error}. The request will fail with a generic message.", response.StatusCode, errorStr);
                return Failed(FailureReason(response.StatusCode));
            }

            return await ModelStream.ReadAsync(response, new GeminiAccumulator(), onText, StreamIdleTimeout, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;   
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception communicating with Gemini. The request will fail with a generic message.");
            return Failed("The language model could not be reached or returned an unreadable reply.");
        }
    }

    
    public TimeSpan StreamIdleTimeout { get; set; } = ModelStream.IdleTimeout;

    internal static LlmChatResponse ParseChatResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        var candidates = doc.RootElement.GetProperty("candidates");
        if (candidates.GetArrayLength() == 0)
        {
            return new LlmChatResponse { Content = "I am ready. How can I assist you with your workspace tasks?" };
        }

        var parts = candidates[0].GetProperty("content").GetProperty("parts");

        var toolCalls = new List<ToolCall>();
        var text = new System.Text.StringBuilder();

        
        string? lastSignature = null;

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("thoughtSignature", out var partSig) && partSig.ValueKind == JsonValueKind.String)
            {
                lastSignature = partSig.GetString();
            }

            if (part.TryGetProperty("functionCall", out var fnCall))
            {
                var argsJson = fnCall.TryGetProperty("args", out var argsElem) ? argsElem.GetRawText() : "{}";
                var id = fnCall.TryGetProperty("id", out var idElem) && !string.IsNullOrWhiteSpace(idElem.GetString())
                    ? idElem.GetString()!
                    : Guid.NewGuid().ToString("N");
                toolCalls.Add(new ToolCall
                {
                    Id = id,
                    Name = fnCall.GetProperty("name").GetString() ?? "",
                    ArgumentsJson = argsJson,
                    
                    ThoughtSignature = part.TryGetProperty("thoughtSignature", out var sig) && sig.ValueKind == JsonValueKind.String
                        ? sig.GetString()
                        : (toolCalls.Count == 0 ? lastSignature : null)
                });
            }
            else if (part.TryGetProperty("text", out var textElem)
                     && !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
            {
                text.Append(textElem.GetString());
            }
        }

        var content = text.ToString().Trim();
        return new LlmChatResponse
        {
            Content = content.Length == 0 ? null : content,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null
        };
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new LlmUnavailableException("No language-model key is configured for the AI service.");
        }

        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } }
        };

        HttpResponseMessage response;
        try
        {
            response = await PostWithRecoveryAsync(_model, body, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Gemini generate request failed.");
            throw new LlmUnavailableException("The language model could not be reached.");
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini generate failed with status {StatusCode}: {Error}", response.StatusCode, raw);
                throw new LlmUnavailableException($"The language model returned HTTP {(int)response.StatusCode}.");
            }

            try
            {
                var parsed = ParseChatResponse(raw);
                if (!string.IsNullOrWhiteSpace(parsed.Content)) return parsed.Content;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Gemini generate returned an unreadable reply.");
            }

            throw new LlmUnavailableException("The language model returned no text.");
        }
    }

    private static (string systemInstruction, List<object> contents) BuildGeminiContents(List<AgentMessage> messages)
    {
        string? systemText = null;
        var turns = new List<(string Role, object Turn)>();
        var pendingResults = new List<object>();

        void FlushResults()
        {
            if (pendingResults.Count == 0) return;
            turns.Add(("user", new { role = "user", parts = pendingResults.ToArray() }));
            pendingResults.Clear();
        }

        foreach (var msg in messages)
        {
            switch (msg.Role)
            {
                case MessageRole.System:
                    
                    systemText ??= msg.Content;
                    break;

                case MessageRole.User:
                {
                    FlushResults();
                    var userParts = new List<object> { new { text = msg.Content } };
                    
                    foreach (var media in msg.Media ?? [])
                        userParts.Add(new { inlineData = new { mimeType = media.MimeType, data = Convert.ToBase64String(media.Data) } });
                    turns.Add(("user", new { role = "user", parts = userParts.ToArray() }));
                    break;
                }

                case MessageRole.Assistant:
                {
                    FlushResults();
                    var parts = new List<object>();
                    if (!string.IsNullOrWhiteSpace(msg.Content))
                    {
                        parts.Add(new { text = msg.Content });
                    }

                    var firstCall = true;
                    foreach (var call in msg.ToolCalls ?? [])
                    {
                        object args;
                        try { args = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson).RootElement.Clone(); }
                        catch (JsonException) { args = new { }; }

                        var part = new Dictionary<string, object?> { ["functionCall"] = new { name = call.Name, args } };
                        
                        
                        if (!string.IsNullOrWhiteSpace(call.ThoughtSignature)) part["thoughtSignature"] = call.ThoughtSignature;
                        else if (firstCall) part["thoughtSignature"] = MissingThoughtSignature;
                        firstCall = false;
                        parts.Add(part);
                    }

                    if (parts.Count > 0) turns.Add(("model", new { role = "model", parts = parts.ToArray() }));
                    break;
                }

                case MessageRole.Tool:
                    pendingResults.Add(new
                    {
                        functionResponse = new
                        {
                            name = msg.ToolName ?? "action",
                            response = new { output = msg.Content }
                        }
                    });
                    break;
            }
        }

        FlushResults();

        
        while (turns.Count > 0 && turns[0].Role == "model") turns.RemoveAt(0);

        return (systemText ?? string.Empty, turns.Select(t => t.Turn).ToList());
    }

    private static List<object>? BuildGeminiTools(IEnumerable<IAgentTool>? tools)
    {
        if (tools == null) return null;

        var fnDeclarations = new List<object>();
        foreach (var tool in tools)
        {
            fnDeclarations.Add(new
            {
                name = tool.Name,
                description = tool.Description,
                parameters = tool.ParameterSchema
            });
        }

        if (fnDeclarations.Count == 0) return null;

        return
        [
            new { functionDeclarations = fnDeclarations }
        ];
    }
}
