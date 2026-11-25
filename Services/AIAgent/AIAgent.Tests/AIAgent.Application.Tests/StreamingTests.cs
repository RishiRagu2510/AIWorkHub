using System.Net;
using System.Text;
using System.Text.Json;
using AIAgent.Api.Streaming;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Domain.Models;
using AIAgent.Infrastructure.Llm;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIAgent.Application.Tests;

internal static class Sse
{
    public static HttpResponseMessage Of(params string[] events) =>
        new(HttpStatusCode.OK) { Content = new StringContent(string.Concat(events.Select(e => $"data: {e}\n\n")), Encoding.UTF8, "text/event-stream") };

    public static string Gemini(params string[] parts) =>
        "{\"candidates\":[{\"content\":{\"role\":\"model\",\"parts\":[" + string.Join(",", parts) + "]}}]}";

    public static string Text(string text) => "{\"text\":" + JsonSerializer.Serialize(text) + "}";

    public static string Delta(string json) => "{\"choices\":[{\"index\":0,\"delta\":" + json + "}]}";
}


internal sealed class BrokenContent(string firstBytes) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new BreakingStream(Encoding.UTF8.GetBytes(firstBytes)));

    private sealed class BreakingStream(byte[] bytes) : Stream
    {
        private int _position;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= bytes.Length) throw new IOException("connection reset");
            var n = Math.Min(buffer.Length, bytes.Length - _position);
            bytes.AsMemory(_position, n).CopyTo(buffer);
            _position += n;
            return ValueTask.FromResult(n);
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}


internal sealed class StalledContent(string firstBytes) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;
    protected override bool TryComputeLength(out long length) { length = 0; return false; }
    protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult<Stream>(new StallingStream(Encoding.UTF8.GetBytes(firstBytes)));

    private sealed class StallingStream(byte[] bytes) : Stream
    {
        private int _position;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_position >= bytes.Length)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);   
                return 0;
            }
            var n = Math.Min(buffer.Length, bytes.Length - _position);
            bytes.AsMemory(_position, n).CopyTo(buffer);
            _position += n;
            return n;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

public class GeminiStreamingTests
{
    private static LlmProvider Provider(RecordingHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI:ApiKey"] = "gemini-key" }).Build();
        return new LlmProvider(new HttpClient(handler, disposeHandler: false), config, NullLogger<LlmProvider>.Instance) { RetryDelay = TimeSpan.Zero };
    }

    private sealed class Tool : IAgentTool
    {
        public string Name => "list_projects";
        public string Description => "Lists projects";
        public string Category => "Test";
        public string RequiredPermission => "";
        public object ParameterSchema => new { type = "OBJECT", properties = new { } };
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default) => Task.FromResult(new ToolResult(true, "{}"));
    }

    [Fact]
    public async Task TextArrivesPieceByPiece_AndTheFinishedReplyIsWhole()
    {
        var handler = new RecordingHandler(_ => Sse.Of(Sse.Gemini(Sse.Text("You have ")), Sse.Gemini(Sse.Text("3 days ")), Sse.Gemini(Sse.Text("left."))));
        var seen = new List<string>();

        var response = await Provider(handler).ChatStreamAsync([AgentMessage.User("leave?")], null, null, t => { seen.Add(t); return Task.CompletedTask; });

        Assert.Equal(["You have ", "3 days ", "left."], seen);
        Assert.Equal("You have 3 days left.", response.Content);
        Assert.False(response.HasToolCalls);
        Assert.Null(response.FallbackReason);
    }

    [Fact]
    public async Task AsksForTheStreamingEndpoint_WithTheKeyInAHeader()
    {
        var handler = new RecordingHandler(_ => Sse.Of(Sse.Gemini(Sse.Text("ok"))));

        await Provider(handler).ChatStreamAsync([AgentMessage.User("hi")], null, null, _ => Task.CompletedTask);

        var request = handler.Calls.Single().Request;
        Assert.Contains(":streamGenerateContent", request.RequestUri!.AbsoluteUri);
        Assert.Contains("alt=sse", request.RequestUri.Query);
        Assert.DoesNotContain("gemini-key", request.RequestUri.AbsoluteUri);
        Assert.Equal("gemini-key", request.Headers.GetValues("x-goog-api-key").Single());
    }

    [Fact]
    public async Task AToolCallIsAssembledAndNoTextIsShown()
    {
        var call = "{\"functionCall\":{\"name\":\"list_projects\",\"args\":{\"search\":\"goo\"}},\"thoughtSignature\":\"sig-1\"}";
        var handler = new RecordingHandler(_ => Sse.Of(Sse.Gemini(call)));
        var seen = new List<string>();

        var response = await Provider(handler).ChatStreamAsync([AgentMessage.User("projects")], [new Tool()], null, t => { seen.Add(t); return Task.CompletedTask; });

        Assert.Empty(seen);
        var toolCall = Assert.Single(response.ToolCalls!);
        Assert.Equal("list_projects", toolCall.Name);
        Assert.Contains("goo", toolCall.ArgumentsJson);
        Assert.Equal("sig-1", toolCall.ThoughtSignature);
    }

    [Fact]
    public async Task TheModelsPrivateThinkingIsNeverShown()
    {
        var handler = new RecordingHandler(_ => Sse.Of(
            Sse.Gemini("{\"text\":\"Let me reason about this.\",\"thought\":true}"),
            Sse.Gemini(Sse.Text("The answer."))));
        var seen = new List<string>();

        var response = await Provider(handler).ChatStreamAsync([AgentMessage.User("q")], null, null, t => { seen.Add(t); return Task.CompletedTask; });

        Assert.Equal(["The answer."], seen);
        Assert.Equal("The answer.", response.Content);
    }

    [Fact]
    public async Task AnErrorFromGoogle_FallsBackAndShowsNothing()
    {
        var handler = new RecordingHandler(_ => Http.Json("{}", HttpStatusCode.Forbidden));
        var seen = new List<string>();

        var response = await Provider(handler).ChatStreamAsync([AgentMessage.User("hi")], null, null, t => { seen.Add(t); return Task.CompletedTask; });

        Assert.Empty(seen);
        Assert.NotNull(response.FallbackReason);
    }

    [Fact]
    public async Task AConnectionThatDropsAfterSomeText_KeepsWhatWasWritten_AndSaysSo()
    {
        var first = "data: " + Sse.Gemini(Sse.Text("Your leave balance is ")) + "\n\n";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new BrokenContent(first) });
        var seen = new List<string>();

        var response = await Provider(handler).ChatStreamAsync([AgentMessage.User("leave?")], null, null, t => { seen.Add(t); return Task.CompletedTask; });

        Assert.Equal(["Your leave balance is "], seen);
        Assert.Equal("Your leave balance is", response.Content);
        Assert.Null(response.FallbackReason);   
        Assert.Contains("cut short", response.ModelNotice);
    }

    [Fact]
    public async Task AModelThatGoesQuietMidAnswer_IsGivenUpOn_AndTheTextSoFarIsKept()
    {
        var first = "data: " + Sse.Gemini(Sse.Text("Working on")) + "\n\n";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StalledContent(first) });
        var provider = Provider(handler);
        provider.StreamIdleTimeout = TimeSpan.FromMilliseconds(150);

        var response = await provider.ChatStreamAsync([AgentMessage.User("hi")], null, null, _ => Task.CompletedTask).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("Working on", response.Content);
        Assert.Contains("cut short", response.ModelNotice);
    }

    [Fact]
    public async Task AConnectionThatDropsBeforeAnyText_FallsBack()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new BrokenContent("") });

        var response = await Provider(handler).ChatStreamAsync([AgentMessage.User("hi")], null, null, _ => Task.CompletedTask);

        Assert.NotNull(response.FallbackReason);
    }

    [Fact]
    public async Task APersonWhoLeaves_StopsTheCall_InsteadOfGettingAFallbackAnswer()
    {
        using var cts = new CancellationTokenSource();
        var handler = new RecordingHandler(_ => { cts.Cancel(); return Sse.Of(Sse.Gemini(Sse.Text("never read"))); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Provider(handler).ChatStreamAsync([AgentMessage.User("hi")], null, null, _ => Task.CompletedTask, cts.Token));
    }

    [Fact]
    public async Task EventsSplitOverSeveralLinesAndCommentsAreRead()
    {
        var body = ": a comment\n\ndata: {\"candidates\":[{\"content\":{\"parts\":[{\"text\":\n"
                   + "data: \"split\"}]}}]}\n\n";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/event-stream") });

        var response = await Provider(handler).ChatStreamAsync([AgentMessage.User("hi")], null, null, _ => Task.CompletedTask);

        Assert.Equal("split", response.Content);
    }
}

public class OpenAiStreamingTests
{
    private static OpenAiChatClient OpenAi(RecordingHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OPENAI_API_KEY"] = "sk-test" }).Build();
        return new OpenAiChatClient(new HttpClient(handler, disposeHandler: false), config, NullLogger<OpenAiChatClient>.Instance);
    }

    private static GroqChatClient Groq(RecordingHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GROQ_API_KEY"] = "gsk-test" }).Build();
        return new GroqChatClient(new HttpClient(handler, disposeHandler: false), config, NullLogger<GroqChatClient>.Instance);
    }

    private static HttpResponseMessage Words() => Sse.Of(
        Sse.Delta("{\"role\":\"assistant\",\"content\":\"\"}"),
        Sse.Delta("{\"content\":\"Hello\"}"),
        Sse.Delta("{\"content\":\" there\"}"),
        "{\"choices\":[{\"index\":0,\"delta\":{},\"finish_reason\":\"stop\"}]}",
        "[DONE]");

    [Fact]
    public async Task OpenAi_StreamsText_AndAsksForAStream()
    {
        var handler = new RecordingHandler(_ => Words());
        var seen = new List<string>();

        var response = await OpenAi(handler).ChatStreamAsync([AgentMessage.User("hi")], null, "gpt-4.1", t => { seen.Add(t); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal(["Hello", " there"], seen);
        Assert.Equal("Hello there", response.Content);
        Assert.Contains("\"stream\":true", handler.Calls.Single().Body);
        Assert.Equal("Bearer sk-test", handler.Calls.Single().Request.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Groq_StreamsText()
    {
        var handler = new RecordingHandler(_ => Words());
        var seen = new List<string>();

        var response = await Groq(handler).ChatStreamAsync([AgentMessage.User("hi")], null, "llama-3.3-70b-versatile", t => { seen.Add(t); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Equal("Hello there", string.Concat(seen));
        Assert.Equal("Hello there", response.Content);
    }

    [Fact]
    public async Task ToolCallsArrivingInFragmentsAreJoinedByIndex()
    {
        var handler = new RecordingHandler(_ => Sse.Of(
            Sse.Delta("{\"tool_calls\":[{\"index\":0,\"id\":\"call_a\",\"type\":\"function\",\"function\":{\"name\":\"list_projects\",\"arguments\":\"\"}}]}"),
            Sse.Delta("{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"{\\\"search\\\":\"}}]}"),
            Sse.Delta("{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"\\\"goo\\\"}\"}}]}"),
            Sse.Delta("{\"tool_calls\":[{\"index\":1,\"id\":\"call_b\",\"function\":{\"name\":\"get_issue\",\"arguments\":\"{}\"}}]}"),
            "[DONE]"));
        var seen = new List<string>();

        var response = await OpenAi(handler).ChatStreamAsync([AgentMessage.User("go")], null, "gpt-4.1", t => { seen.Add(t); return Task.CompletedTask; }, CancellationToken.None);

        Assert.Empty(seen);
        Assert.Equal(2, response.ToolCalls!.Count);
        Assert.Equal("call_a", response.ToolCalls[0].Id);
        Assert.Equal("list_projects", response.ToolCalls[0].Name);
        Assert.Equal("{\"search\":\"goo\"}", response.ToolCalls[0].ArgumentsJson);
        Assert.Equal("get_issue", response.ToolCalls[1].Name);
    }

    [Fact]
    public async Task ARefusalFromTheProvider_IsReportedAsUnavailable()
    {
        var handler = new RecordingHandler(_ => Http.Json("{}", HttpStatusCode.TooManyRequests));

        var ex = await Assert.ThrowsAsync<OpenAiUnavailableException>(() =>
            OpenAi(handler).ChatStreamAsync([AgentMessage.User("hi")], null, "gpt-4.1", _ => Task.CompletedTask, CancellationToken.None));

        Assert.Contains("rate limiting", ex.Message);
        Assert.DoesNotContain("sk-test", ex.Message);
    }

    [Fact]
    public async Task ADropBeforeAnyText_IsReportedAsUnavailable_SoTheRouterCanUseGemini()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new BrokenContent("") });

        await Assert.ThrowsAsync<GroqUnavailableException>(() =>
            Groq(handler).ChatStreamAsync([AgentMessage.User("hi")], null, "llama-3.3-70b-versatile", _ => Task.CompletedTask, CancellationToken.None));
    }

    [Fact]
    public async Task ADropAfterSomeText_KeepsTheText()
    {
        var first = "data: " + Sse.Delta("{\"content\":\"Half an ans\"}") + "\n\n";
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new BrokenContent(first) });

        var response = await OpenAi(handler).ChatStreamAsync([AgentMessage.User("hi")], null, "gpt-4.1", _ => Task.CompletedTask, CancellationToken.None);

        Assert.Equal("Half an ans", response.Content);
        Assert.Contains("cut short", response.ModelNotice);
    }
}

public class StreamingRouterTests
{
    private static ModelRoutingLlmProvider Router(RecordingHandler gemini, RecordingHandler openAi, RecordingHandler groq)
    {
        var geminiConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI:ApiKey"] = "g" }).Build();
        var openAiConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OPENAI_API_KEY"] = "sk" }).Build();
        var groqConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GROQ_API_KEY"] = "gsk" }).Build();
        return new ModelRoutingLlmProvider(
            new LlmProvider(new HttpClient(gemini, false), geminiConfig, NullLogger<LlmProvider>.Instance) { RetryDelay = TimeSpan.Zero },
            new OpenAiChatClient(new HttpClient(openAi, false), openAiConfig, NullLogger<OpenAiChatClient>.Instance),
            new GroqChatClient(new HttpClient(groq, false), groqConfig, NullLogger<GroqChatClient>.Instance),
            NullLogger<ModelRoutingLlmProvider>.Instance);
    }

    private static readonly RecordingHandler Unused = new(_ => throw new InvalidOperationException("should not be called"));

    [Fact]
    public async Task GeminiModels_Stream()
    {
        var gemini = new RecordingHandler(_ => Sse.Of(Sse.Gemini(Sse.Text("hi "))));
        var seen = new List<string>();

        await Router(gemini, Unused, Unused).ChatStreamAsync([AgentMessage.User("x")], null, "gemini-flash-latest", t => { seen.Add(t); return Task.CompletedTask; });

        Assert.Equal(["hi "], seen);
    }

    [Fact]
    public async Task OpenAiModels_Stream()
    {
        var openAi = new RecordingHandler(_ => Sse.Of(Sse.Delta("{\"content\":\"hey\"}")));
        var seen = new List<string>();

        await Router(Unused, openAi, Unused).ChatStreamAsync([AgentMessage.User("x")], null, "gpt-4.1", t => { seen.Add(t); return Task.CompletedTask; });

        Assert.Equal(["hey"], seen);
    }

    [Fact]
    public async Task WhenTheChosenModelCannotAnswer_GeminiAnswersAndSaysSo()
    {
        var gemini = new RecordingHandler(_ => Sse.Of(Sse.Gemini(Sse.Text("from gemini"))));
        var groq = new RecordingHandler(_ => Http.Json("{}", HttpStatusCode.TooManyRequests));
        var seen = new List<string>();

        var response = await Router(gemini, Unused, groq).ChatStreamAsync([AgentMessage.User("x")], null, "llama-3.3-70b-versatile", t => { seen.Add(t); return Task.CompletedTask; });

        Assert.Equal("from gemini", string.Concat(seen));
        Assert.Contains("llama-3.3-70b-versatile could not answer", response.ModelNotice);
    }

    [Fact]
    public async Task ChatAsync_IsStillNotStreamed()
    {
        var gemini = new RecordingHandler(_ => Http.Json("{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"whole\"}]}}]}"));

        var response = await Router(gemini, Unused, Unused).ChatAsync([AgentMessage.User("x")], null, "gemini-flash-latest");

        Assert.Equal("whole", response.Content);
        Assert.Contains(":generateContent", gemini.Calls.Single().Request.RequestUri!.AbsoluteUri);
    }
}

public class ChatProgressTests
{
    private sealed class Recorder : IAgentProgress
    {
        public List<string> Events { get; } = [];
        public Task StatusAsync(string text) { Events.Add("status:" + text); return Task.CompletedTask; }
        public Task TextAsync(string delta) { Events.Add("text:" + delta); return Task.CompletedTask; }
        public Task DiscardTextAsync() { Events.Add("discard"); return Task.CompletedTask; }
    }

    
    private sealed class Streaming(params (string[] Pieces, LlmChatResponse Reply)[] turns) : ILlmProvider
    {
        private int _turn;
        public int PlainCalls { get; private set; }

        public Task<LlmChatResponse> ChatAsync(List<AgentMessage> messages, IEnumerable<IAgentTool>? availableTools = null, string? model = null, CancellationToken cancellationToken = default)
        {
            PlainCalls++;
            return Task.FromResult(turns[_turn++].Reply);
        }

        public async Task<LlmChatResponse> ChatStreamAsync(List<AgentMessage> messages, IEnumerable<IAgentTool>? availableTools, string? model, Func<string, Task> onText, CancellationToken cancellationToken = default)
        {
            var (pieces, reply) = turns[_turn++];
            foreach (var piece in pieces) await onText(piece);
            return reply;
        }

        public Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) => Task.FromResult("");
    }

    private sealed class Lookup(string name) : IAgentTool
    {
        public string Name => name;
        public string Description => name;
        public string Category => "Test";
        public string RequiredPermission => "";
        public object ParameterSchema => new { };
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default) => Task.FromResult(new ToolResult(true, "{\"ok\":true}"));
    }

    private static LlmChatResponse CallTo(string tool) => new() { ToolCalls = [new ToolCall { Id = "c1", Name = tool, ArgumentsJson = "{}" }] };

    [Fact]
    public async Task ASimpleAnswerReportsThinkingThenItsText()
    {
        var progress = new Recorder();
        var llm = new Streaming((["Hello ", "there."], new LlmChatResponse { Content = "Hello there." }));

        var response = await new AthennaAgentOrchestrator(llm, []).ExecuteChatAsync(new AgentChatRequest("hi"), progress: progress);

        Assert.Equal(["status:Thinking", "text:Hello ", "text:there."], progress.Events);
        Assert.Equal("Hello there.", response.Message);
    }

    [Fact]
    public async Task EachToolIsAnnounced_AndTextBeforeAToolCallIsDiscarded()
    {
        var progress = new Recorder();
        var llm = new Streaming(
            (["Let me check. "], CallTo("get_leave_balance")),
            (["You have 4 days."], new LlmChatResponse { Content = "You have 4 days." }));

        var response = await new AthennaAgentOrchestrator(llm, [new Lookup("get_leave_balance")]).ExecuteChatAsync(new AgentChatRequest("leave?"), progress: progress);

        Assert.Equal(
            ["status:Thinking", "text:Let me check. ", "discard", "status:Looking up leave balance", "status:Working out the answer", "text:You have 4 days."],
            progress.Events);
        Assert.Equal("You have 4 days.", response.Message);
    }

    [Fact]
    public async Task AToolTheCallerMayNotUse_IsNotAnnounced()
    {
        var progress = new Recorder();
        var llm = new Streaming(([], CallTo("approve_leave_request")), ([], new LlmChatResponse { Content = "Not allowed." }));
        var restricted = new GatedTool("approve_leave_request", "LEAVE_APPROVE");

        await new AthennaAgentOrchestrator(llm, [restricted]).ExecuteChatAsync(new AgentChatRequest("approve", ExtraContext: new() { ["role"] = "DEVELOPER" }), progress: progress);

        Assert.DoesNotContain(progress.Events, e => e.Contains("Approving"));
    }

    private sealed class GatedTool(string name, string permission) : IAgentTool
    {
        public string Name => name;
        public string Description => name;
        public string Category => "Test";
        public string RequiredPermission => permission;
        public object ParameterSchema => new { };
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default) => Task.FromResult(new ToolResult(true, "{}"));
    }

    [Fact]
    public async Task WithoutAProgressListener_TheModelIsAskedInOnePiece()
    {
        var llm = new Streaming(([], new LlmChatResponse { Content = "plain" }));

        var response = await new AthennaAgentOrchestrator(llm, []).ExecuteChatAsync(new AgentChatRequest("hi"));

        Assert.Equal(1, llm.PlainCalls);
        Assert.Equal("plain", response.Message);
    }

    [Theory]
    [InlineData("get_leave_balance", "Looking up leave balance")]
    [InlineData("search_employees", "Searching employees")]
    [InlineData("prepare_leave", "Preparing leave")]
    [InlineData("read_attachment", "Reading your files")]
    [InlineData("transition_issue", "Moving issue")]
    [InlineData("something_new", "Running something new")]
    [InlineData("", "Working on it")]
    public void ToolNamesBecomeSentences(string tool, string expected) => Assert.Equal(expected, ToolStatus.For(tool));
}

public class SseWriterTests
{
    private static (DefaultHttpContext Context, MemoryStream Body) NewContext()
    {
        var body = new MemoryStream();
        var context = new DefaultHttpContext();
        context.Response.Body = body;
        return (context, body);
    }

    [Fact]
    public async Task WritesNamedEventsWithJsonData()
    {
        var (context, body) = NewContext();
        await using (var sse = new SseAgentProgress(context.Response, CancellationToken.None) { KeepAliveEvery = TimeSpan.FromHours(1) })
        {
            await sse.StartAsync();
            await sse.StatusAsync("Thinking");
            await sse.TextAsync("Hel\"lo\nthere");
            await sse.DiscardTextAsync();
            await sse.SendAsync("done", new AgentChatResponse("s1", "Hi", null, true));
        }

        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.StartsWith(": open\n\n", text);
        Assert.Contains("event: status\ndata: {\"text\":\"Thinking\"}\n\n", text);
        Assert.Contains("event: delta\ndata: {\"text\":\"Hel\\u0022lo\\nthere\"}\n\n", text);   
        Assert.Contains("event: reset\ndata: {}\n\n", text);
        Assert.Contains("event: done\ndata: {\"sessionId\":\"s1\",\"message\":\"Hi\"", text);
        Assert.StartsWith("text/event-stream", context.Response.ContentType);
        Assert.Equal("no-cache, no-transform", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task SendsAKeepAliveWhileTheModelIsQuiet()
    {
        var (context, body) = NewContext();
        await using var sse = new SseAgentProgress(context.Response, CancellationToken.None) { KeepAliveEvery = TimeSpan.FromMilliseconds(20) };
        await sse.StartAsync();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!Encoding.UTF8.GetString(body.ToArray()).Contains(": keep-alive") && DateTime.UtcNow < deadline) await Task.Delay(20);

        Assert.Contains(": keep-alive\n\n", Encoding.UTF8.GetString(body.ToArray()));
    }

    [Fact]
    public async Task OnceTheClientHasGone_WritingStops()
    {
        var (context, _) = NewContext();
        using var gone = new CancellationTokenSource();
        await using var sse = new SseAgentProgress(context.Response, gone.Token) { KeepAliveEvery = TimeSpan.FromHours(1) };
        await sse.StartAsync();
        gone.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sse.TextAsync("late"));
    }
}
