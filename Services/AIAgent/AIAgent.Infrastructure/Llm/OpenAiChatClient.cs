using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AIAgent.Application.Interfaces;
using AIAgent.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgent.Infrastructure.Llm;

public sealed class OpenAiUnavailableException(string message) : Exception(message);

public sealed class OpenAiChatClient
{
    private static readonly Regex ModelPattern = new(@"^(gpt-|chatgpt-|o\d)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly ILogger<OpenAiChatClient> _logger;
    private readonly string? _apiKey;
    private readonly string _endpoint;

    public OpenAiChatClient(HttpClient http, IConfiguration configuration, ILogger<OpenAiChatClient> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = configuration["AI:OpenAiApiKey"] ?? configuration["OPENAI_API_KEY"];
        _endpoint = (configuration["AI:OpenAiBaseUrl"] ?? "https://api.openai.com/v1").TrimEnd('/') + "/chat/completions";
    }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(45);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    
    public static bool Owns(string? model) => !string.IsNullOrWhiteSpace(model) && ModelPattern.IsMatch(model.Trim());

    public async Task<LlmChatResponse> ChatAsync(List<AgentMessage> messages, IEnumerable<IAgentTool>? tools, string model, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new OpenAiUnavailableException("No OpenAI key is configured on the server.");

        var body = new Dictionary<string, object?>
        {
            ["model"] = model.Trim(),
            ["messages"] = BuildMessages(messages, canViewImages: true)
        };
        var declared = BuildTools(tools);
        if (declared.Count > 0)
        {
            body["tools"] = declared;
            body["tool_choice"] = "auto";
        }

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = JsonContent.Create(body) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(RequestTimeout);
            response = await _http.SendAsync(request, limit.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "OpenAI request failed.");
            throw new OpenAiUnavailableException("OpenAI could not be reached.");
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("OpenAI answered {Status} for model {Model}: {Error}", (int)response.StatusCode, model, Truncate(raw));
                throw new OpenAiUnavailableException(Describe((int)response.StatusCode, model));
            }

            try { return Parse(raw); }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "OpenAI returned an unreadable reply.");
                throw new OpenAiUnavailableException("OpenAI returned a reply that could not be read.");
            }
        }
    }

    
    public async Task<LlmChatResponse> ChatStreamAsync(List<AgentMessage> messages, IEnumerable<IAgentTool>? tools, string model, Func<string, Task> onText, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new OpenAiUnavailableException("No OpenAI key is configured on the server.");

        var body = new Dictionary<string, object?>
        {
            ["model"] = model.Trim(),
            ["messages"] = OpenAiChatClient.BuildMessages(messages, canViewImages: true),
            ["stream"] = true
        };
        var declared = OpenAiChatClient.BuildTools(tools);
        if (declared.Count > 0)
        {
            body["tools"] = declared;
            body["tool_choice"] = "auto";
        }

        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = JsonContent.Create(body) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(RequestTimeout);
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (cancellationToken.IsCancellationRequested) throw;
            _logger.LogWarning(ex, "OpenAI request failed.");
            throw new OpenAiUnavailableException("OpenAI could not be reached.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("OpenAI answered {Status} for model {Model}: {Error}", (int)response.StatusCode, model, Truncate(raw));
                throw new OpenAiUnavailableException(Describe((int)response.StatusCode, model));
            }

            try { return await ModelStream.ReadAsync(response, new ChatCompletionAccumulator(), onText, ModelStream.IdleTimeout, cancellationToken); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is JsonException or HttpRequestException or IOException or OperationCanceledException)
            {
                _logger.LogWarning(ex, "OpenAI stream failed.");
                throw new OpenAiUnavailableException("OpenAI stopped answering part way.");
            }
        }
    }

    private static string Describe(int status, string model) => status switch
    {
        401 => "The OpenAI key was rejected.",
        403 => "The OpenAI key is not allowed to use this model.",
        404 => $"OpenAI does not offer the model '{model}' to this key.",
        429 => "OpenAI is rate limiting this key or its quota is used up.",
        >= 500 => "OpenAI is having a problem right now.",
        _ => $"OpenAI answered HTTP {status}."
    };

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];

    private static object UserTurn(AgentMessage msg, bool canViewImages)
    {
        if (msg.Media is not { Count: > 0 }) return new { role = "user", content = msg.Content };

        var parts = new List<object>();
        var hidden = new List<string>();
        foreach (var media in msg.Media)
        {
            if (canViewImages && media.IsImage)
                parts.Add(new { type = "image_url", image_url = new { url = $"data:{media.MimeType};base64,{Convert.ToBase64String(media.Data)}" } });
            else
                hidden.Add(media.Name);
        }

        var text = hidden.Count == 0
            ? msg.Content
            : msg.Content + $"\n[This model cannot view {string.Join(", ", hidden.Select(n => $"\"{n}\""))}. Tell the user it was not read, and suggest choosing a model that can view pictures and documents.]";

        if (parts.Count == 0) return new { role = "user", content = text };
        parts.Insert(0, new { type = "text", text });
        return new { role = "user", content = parts.ToArray() };
    }

    
    internal static List<object> BuildMessages(List<AgentMessage> messages, bool canViewImages = false)
    {
        var result = new List<object>();
        string? system = null;
        var openCalls = new HashSet<string>();

        foreach (var msg in messages)
        {
            switch (msg.Role)
            {
                case MessageRole.System:
                    system ??= msg.Content;
                    break;

                case MessageRole.User:
                    openCalls.Clear();
                    result.Add(UserTurn(msg, canViewImages));
                    break;

                case MessageRole.Assistant:
                {
                    openCalls.Clear();
                    
                    if (result.Count == 0) break;
                    var calls = (msg.ToolCalls ?? [])
                        .Where(c => !string.IsNullOrWhiteSpace(c.Id))
                        .Select(c => new
                        {
                            id = c.Id,
                            type = "function",
                            function = new { name = c.Name, arguments = string.IsNullOrWhiteSpace(c.ArgumentsJson) ? "{}" : c.ArgumentsJson }
                        })
                        .ToList();
                    foreach (var c in calls) openCalls.Add(c.id);

                    if (calls.Count > 0)
                        result.Add(new { role = "assistant", content = string.IsNullOrWhiteSpace(msg.Content) ? null : msg.Content, tool_calls = calls });
                    else if (!string.IsNullOrWhiteSpace(msg.Content))
                        result.Add(new { role = "assistant", content = msg.Content });
                    break;
                }

                case MessageRole.Tool:
                    
                    if (msg.ToolCallId != null && openCalls.Contains(msg.ToolCallId))
                        result.Add(new { role = "tool", tool_call_id = msg.ToolCallId, content = msg.Content });
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(system)) result.Insert(0, new { role = "system", content = system });
        return result;
    }

    
    internal static List<object> BuildTools(IEnumerable<IAgentTool>? tools)
    {
        var declared = new List<object>();
        foreach (var tool in tools ?? [])
        {
            var schema = JsonSerializer.SerializeToNode(tool.ParameterSchema) ?? new JsonObject();
            LowerCaseTypes(schema);
            if (schema is JsonObject o)
            {
                if (!o.ContainsKey("type")) o["type"] = "object";
                if (!o.ContainsKey("properties")) o["properties"] = new JsonObject();
            }

            declared.Add(new { type = "function", function = new { name = tool.Name, description = tool.Description, parameters = schema } });
        }

        return declared;
    }

    private static void LowerCaseTypes(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(p => p.Key).ToList())
                {
                    if (key == "type" && obj[key] is JsonValue v && v.TryGetValue<string>(out var t)) obj[key] = t.ToLowerInvariant();
                    else LowerCaseTypes(obj[key]);
                }
                break;
            case JsonArray arr:
                foreach (var item in arr) LowerCaseTypes(item);
                break;
        }
    }

    internal static LlmChatResponse Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        var response = new LlmChatResponse();

        if (message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            response.Content = content.GetString();

        if (message.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                var fn = call.GetProperty("function");
                response.ToolCalls ??= [];
                response.ToolCalls.Add(new ToolCall
                {
                    Id = call.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString()! : Guid.NewGuid().ToString("N"),
                    Name = fn.GetProperty("name").GetString() ?? string.Empty,
                    ArgumentsJson = fn.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(args.GetString()) ? args.GetString()! : "{}"
                });
            }
        }

        return response;
    }
}
