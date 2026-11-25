using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AIAgent.Application.Interfaces;
using AIAgent.Domain.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AIAgent.Infrastructure.Llm;


public sealed class GroqUnavailableException(string message) : Exception(message);

public sealed class GroqChatClient
{
    private static readonly Regex ModelPattern = new(@"^(llama-|llama3|meta-llama/|openai/gpt-oss|qwen/|moonshotai/|groq/|gemma2-)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _http;
    private readonly ILogger<GroqChatClient> _logger;
    private readonly string? _apiKey;
    private readonly string _endpoint;

    public GroqChatClient(HttpClient http, IConfiguration configuration, ILogger<GroqChatClient> logger)
    {
        _http = http;
        _logger = logger;
        _apiKey = configuration["AI:GroqApiKey"] ?? configuration["GROQ_API_KEY"];
        _endpoint = (configuration["AI:GroqBaseUrl"] ?? "https://api.groq.com/openai/v1").TrimEnd('/') + "/chat/completions";
    }

    
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(45);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    
    public static bool Owns(string? model) => !string.IsNullOrWhiteSpace(model) && ModelPattern.IsMatch(model.Trim());

    public async Task<LlmChatResponse> ChatAsync(List<AgentMessage> messages, IEnumerable<IAgentTool>? tools, string model, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new GroqUnavailableException("No Groq key is configured on the server.");

        var body = new Dictionary<string, object?>
        {
            ["model"] = model.Trim(),
            ["messages"] = OpenAiChatClient.BuildMessages(messages)
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
            response = await _http.SendAsync(request, limit.Token);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Groq request failed.");
            throw new GroqUnavailableException("Groq could not be reached.");
        }

        using (response)
        {
            var raw = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Groq answered {Status} for model {Model}: {Error}", (int)response.StatusCode, model, Truncate(raw));
                throw new GroqUnavailableException(Describe((int)response.StatusCode, model));
            }

            try { return OpenAiChatClient.Parse(raw); }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogWarning(ex, "Groq returned an unreadable reply.");
                throw new GroqUnavailableException("Groq returned a reply that could not be read.");
            }
        }
    }

    
    public async Task<LlmChatResponse> ChatStreamAsync(List<AgentMessage> messages, IEnumerable<IAgentTool>? tools, string model, Func<string, Task> onText, CancellationToken cancellationToken)
    {
        if (!IsConfigured) throw new GroqUnavailableException("No Groq key is configured on the server.");

        var body = new Dictionary<string, object?>
        {
            ["model"] = model.Trim(),
            ["messages"] = OpenAiChatClient.BuildMessages(messages),
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
            _logger.LogWarning(ex, "Groq request failed.");
            throw new GroqUnavailableException("Groq could not be reached.");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var raw = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Groq answered {Status} for model {Model}: {Error}", (int)response.StatusCode, model, Truncate(raw));
                throw new GroqUnavailableException(Describe((int)response.StatusCode, model));
            }

            try { return await ModelStream.ReadAsync(response, new ChatCompletionAccumulator(), onText, ModelStream.IdleTimeout, cancellationToken); }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is JsonException or HttpRequestException or IOException or OperationCanceledException)
            {
                _logger.LogWarning(ex, "Groq stream failed.");
                throw new GroqUnavailableException("Groq stopped answering part way.");
            }
        }
    }

    private static string Describe(int status, string model) => status switch
    {
        400 => $"Groq could not run the model '{model}' for this request.",
        401 => "The Groq key was rejected.",
        403 => "The Groq key is not allowed to use this model.",
        404 => $"Groq does not offer the model '{model}' to this key.",
        413 => "The request was too large for Groq.",
        429 => "Groq is rate limiting this key or its quota is used up.",
        >= 500 => "Groq is having a problem right now.",
        _ => $"Groq answered HTTP {status}."
    };

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300];
}
