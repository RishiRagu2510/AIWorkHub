using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AIAgent.Application.Interfaces;

namespace AIAgent.Infrastructure.Llm;


internal static class ServerSentEvents
{
    public static async IAsyncEnumerable<string> ReadDataAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var data = new StringBuilder();

        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                
                if (data.Length > 0)
                {
                    var payload = data.ToString();
                    data.Clear();
                    if (payload != "[DONE]") yield return payload;
                }
                continue;
            }

            if (line[0] == ':' || !line.StartsWith("data:", StringComparison.Ordinal)) continue;   

            
            var start = line.Length > 5 && line[5] == ' ' ? 6 : 5;
            if (data.Length > 0) data.Append('\n');
            data.Append(line, start, line.Length - start);
        }

        if (data.Length > 0 && data.ToString() != "[DONE]") yield return data.ToString();
    }
}


internal interface IStreamAccumulator
{
    
    string Add(string eventData);

    
    LlmChatResponse Finish();

    
    LlmChatResponse FinishText();
}

internal static class ModelStream
{
    
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(40);

    public static async Task<LlmChatResponse> ReadAsync(
        HttpResponseMessage response, IStreamAccumulator accumulator, Func<string, Task> onText, TimeSpan idleTimeout, CancellationToken cancellationToken)
    {
        using var idle = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        idle.CancelAfter(idleTimeout);
        var shown = false;

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(idle.Token);
            await foreach (var data in ServerSentEvents.ReadDataAsync(stream, idle.Token))
            {
                idle.CancelAfter(idleTimeout);
                var text = accumulator.Add(data);
                if (text.Length == 0) continue;
                shown = true;
                await onText(text);
            }
            return accumulator.Finish();
        }
        catch (Exception) when (shown && !cancellationToken.IsCancellationRequested)
        {
            var partial = accumulator.FinishText();
            partial.ModelNotice = "The connection to the model dropped while it was writing, so this reply may be cut short.";
            return partial;
        }
    }
}


internal sealed class ChatCompletionAccumulator : IStreamAccumulator
{
    private readonly StringBuilder _content = new();
    private readonly SortedDictionary<int, (string? Id, StringBuilder Name, StringBuilder Arguments)> _calls = [];

    public string Add(string eventData)
    {
        using var doc = JsonDocument.Parse(eventData);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() == 0) return "";
        if (!choices[0].TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object) return "";

        if (delta.TryGetProperty("tool_calls", out var calls) && calls.ValueKind == JsonValueKind.Array)
        {
            foreach (var call in calls.EnumerateArray())
            {
                var index = call.TryGetProperty("index", out var i) && i.ValueKind == JsonValueKind.Number ? i.GetInt32() : _calls.Count;
                if (!_calls.TryGetValue(index, out var entry))
                    entry = (null, new StringBuilder(), new StringBuilder());

                var id = entry.Id;
                if (call.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(idElement.GetString()))
                    id = idElement.GetString();
                if (call.TryGetProperty("function", out var fn) && fn.ValueKind == JsonValueKind.Object)
                {
                    if (fn.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String) entry.Name.Append(name.GetString());
                    if (fn.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String) entry.Arguments.Append(args.GetString());
                }
                _calls[index] = (id, entry.Name, entry.Arguments);
            }
        }

        if (delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString() ?? "";
            _content.Append(text);
            return text;
        }
        return "";
    }

    public LlmChatResponse Finish()
    {
        var response = FinishText();
        foreach (var (_, call) in _calls)
        {
            if (call.Name.Length == 0) continue;
            response.ToolCalls ??= [];
            response.ToolCalls.Add(new ToolCall
            {
                Id = call.Id ?? Guid.NewGuid().ToString("N"),
                Name = call.Name.ToString(),
                ArgumentsJson = string.IsNullOrWhiteSpace(call.Arguments.ToString()) ? "{}" : call.Arguments.ToString()
            });
        }
        return response;
    }

    public LlmChatResponse FinishText() => new() { Content = _content.Length == 0 ? null : _content.ToString() };
}


internal sealed class GeminiAccumulator : IStreamAccumulator
{
    private readonly List<string> _parts = [];
    private bool _sawCandidate;

    public string Add(string eventData)
    {
        using var doc = JsonDocument.Parse(eventData);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array || candidates.GetArrayLength() == 0) return "";
        _sawCandidate = true;
        if (!candidates[0].TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var parts) || parts.ValueKind != JsonValueKind.Array) return "";

        var text = new StringBuilder();
        foreach (var part in parts.EnumerateArray())
        {
            _parts.Add(part.GetRawText());
            var isThought = part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True;
            if (!isThought && !part.TryGetProperty("functionCall", out _) && part.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                text.Append(t.GetString());
        }
        return text.ToString();
    }

    public LlmChatResponse Finish() =>
        _sawCandidate
            ? LlmProvider.ParseChatResponse("{\"candidates\":[{\"content\":{\"parts\":[" + string.Join(",", _parts) + "]}}]}")
            : new LlmChatResponse { Content = "I am ready. How can I assist you with your workspace tasks?" };

    public LlmChatResponse FinishText()
    {
        var response = Finish();
        response.ToolCalls = null;
        return response;
    }
}
