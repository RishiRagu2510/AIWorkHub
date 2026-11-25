using Xunit;
using System.Net;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Domain.Models;
using AIAgent.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIAgent.Application.Tests;

public class OpenAiProviderTests
{
    private const string Key = "sk-test-secret-key";
    private const string GeminiOk = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"from gemini\"}]}}]}";
    private const string OpenAiText = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"from openai\"}}]}";

    private sealed class SchemaTool : IAgentTool
    {
        public string Name => "list_projects";
        public string Description => "Lists projects";
        public string Category => "GridHub";
        public string RequiredPermission => "";
        public object ParameterSchema => new
        {
            type = "OBJECT",
            properties = new Dictionary<string, object> { ["search"] = new { type = "STRING", description = "text" }, ["limit"] = new { type = "INTEGER" } }
        };
        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default) => Task.FromResult(new ToolResult(true, "{}"));
    }

    private static OpenAiChatClient Client(RecordingHandler handler, string? key = Key)
    {
        var settings = new Dictionary<string, string?>();
        if (key != null) settings["OPENAI_API_KEY"] = key;
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new OpenAiChatClient(new HttpClient(handler, disposeHandler: false), config, NullLogger<OpenAiChatClient>.Instance);
    }

    private static LlmProvider Gemini(RecordingHandler handler)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI:ApiKey"] = "gemini-key" }).Build();
        return new LlmProvider(new HttpClient(handler, disposeHandler: false), config, NullLogger<LlmProvider>.Instance) { RetryDelay = TimeSpan.Zero };
    }

    private static GroqChatClient GroqClient(RecordingHandler handler, string? key = "gsk-test-secret")
    {
        var settings = new Dictionary<string, string?>();
        if (key != null) settings["GROQ_API_KEY"] = key;
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new GroqChatClient(new HttpClient(handler, disposeHandler: false), config, NullLogger<GroqChatClient>.Instance);
    }

    private static ModelRoutingLlmProvider Router(RecordingHandler openAi, RecordingHandler gemini, string? key = Key, RecordingHandler? groq = null, string? groqKey = "gsk-test-secret") =>
        new(Gemini(gemini), Client(openAi, key), GroqClient(groq ?? new RecordingHandler(_ => Http.Json(OpenAiText)), groqKey), NullLogger<ModelRoutingLlmProvider>.Instance);

    [Theory]
    [InlineData("llama-3.3-70b-versatile", true)]
    [InlineData("openai/gpt-oss-120b", true)]
    [InlineData("meta-llama/llama-4-scout-17b-16e-instruct", true)]
    [InlineData("gpt-4.1", false)]
    [InlineData("gemini-flash-latest", false)]
    [InlineData(null, false)]
    public void OnlyGroqModelIds_AreRoutedToGroq(string? model, bool expected) =>
        Assert.Equal(expected, GroqChatClient.Owns(model));

    [Fact]
    public async Task GroqModel_IsAnsweredByGroq_WithTheBearerKey_AndNeverTouchesOtherProviders()
    {
        var openAi = new RecordingHandler(_ => Http.Json(OpenAiText));
        var gemini = new RecordingHandler(_ => Http.Json(GeminiOk));
        var groq = new RecordingHandler(_ => Http.Json("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"from groq\"}}]}"));

        var response = await Router(openAi, gemini, groq: groq).ChatAsync([AgentMessage.User("hi")], [new SchemaTool()], "llama-3.3-70b-versatile");

        Assert.Equal("from groq", response.Content);
        Assert.Null(response.ModelNotice);
        Assert.Empty(openAi.Calls);
        Assert.Empty(gemini.Calls);
        var call = Assert.Single(groq.Calls);
        Assert.Equal("api.groq.com", call.Request.RequestUri!.Host);
        Assert.Equal("Bearer gsk-test-secret", call.Request.Headers.Authorization!.ToString());
        Assert.Contains("\"type\":\"string\"", call.Body!);
    }

    [Fact]
    public async Task GroqModel_FallsBackToGemini_AndSaysSo_WhenGroqRefusesTheKey()
    {
        var openAi = new RecordingHandler(_ => Http.Json(OpenAiText));
        var gemini = new RecordingHandler(_ => Http.Json(GeminiOk));
        var groq = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"error\":\"bad gsk-test-secret\"}") });

        var response = await Router(openAi, gemini, groq: groq).ChatAsync([AgentMessage.User("hi")], null, "llama-3.3-70b-versatile");

        Assert.Equal("from gemini", response.Content);
        Assert.Contains("The Groq key was rejected", response.ModelNotice);
        Assert.DoesNotContain("gsk-test-secret", response.ModelNotice);
    }

    [Fact]
    public async Task GroqModel_WithoutAKey_AnswersWithGemini_WithoutCallingGroq()
    {
        var gemini = new RecordingHandler(_ => Http.Json(GeminiOk));
        var groq = new RecordingHandler(_ => Http.Json(OpenAiText));

        var response = await Router(new RecordingHandler(_ => Http.Json(OpenAiText)), gemini, groq: groq, groqKey: null).ChatAsync([AgentMessage.User("hi")], null, "openai/gpt-oss-120b");

        Assert.Equal("from gemini", response.Content);
        Assert.Contains("No Groq key is configured", response.ModelNotice);
        Assert.Empty(groq.Calls);
    }

    [Theory]
    [InlineData("gpt-4.1", true)]
    [InlineData("gpt-5", true)]
    [InlineData("o3-mini", true)]
    [InlineData("gemini-flash-latest", false)]
    [InlineData(null, false)]
    public void OnlyOpenAiModelIds_AreRoutedToOpenAi(string? model, bool expected) =>
        Assert.Equal(expected, OpenAiChatClient.Owns(model));

    [Fact]
    public async Task Request_UsesTheBearerKey_StandardSchemaTypes_AndPairsToolResults()
    {
        var handler = new RecordingHandler(_ => Http.Json(OpenAiText));
        var messages = new List<AgentMessage>
        {
            AgentMessage.System("be helpful"),
            new() { Role = MessageRole.Assistant, Content = "stale reply with nothing before it" },
            AgentMessage.User("create an issue"),
            new() { Role = MessageRole.Assistant, ToolCalls = [new AgentToolCall { Id = "c1", Name = "list_projects", ArgumentsJson = "{}" }] },
            AgentMessage.Tool("list_projects", "c1", "{\"projects\":[]}"),
            AgentMessage.Tool("list_projects", "orphan", "{}")
        };

        var response = await Client(handler).ChatAsync(messages, [new SchemaTool()], "gpt-4.1", CancellationToken.None);

        Assert.Equal("from openai", response.Content);
        var call = handler.Calls.Single();
        Assert.Equal("Bearer", call.Request.Headers.Authorization!.Scheme);
        Assert.Equal(Key, call.Request.Headers.Authorization!.Parameter);
        Assert.DoesNotContain(Key, call.Body!);

        using var body = JsonDocument.Parse(call.Body!);
        var sent = body.RootElement.GetProperty("messages");
        Assert.Equal(["system", "user", "assistant", "tool"], sent.EnumerateArray().Select(m => m.GetProperty("role").GetString()!).ToArray());
        Assert.Equal("c1", sent[2].GetProperty("tool_calls")[0].GetProperty("id").GetString());
        Assert.Equal("c1", sent[3].GetProperty("tool_call_id").GetString());

        var parameters = body.RootElement.GetProperty("tools")[0].GetProperty("function").GetProperty("parameters");
        Assert.Equal("object", parameters.GetProperty("type").GetString());
        Assert.Equal("string", parameters.GetProperty("properties").GetProperty("search").GetProperty("type").GetString());
        Assert.Equal("integer", parameters.GetProperty("properties").GetProperty("limit").GetProperty("type").GetString());
    }

    [Fact]
    public async Task ToolCalls_AreReadBack()
    {
        var handler = new RecordingHandler(_ => Http.Json("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":null,\"tool_calls\":[{\"id\":\"call_9\",\"type\":\"function\",\"function\":{\"name\":\"list_projects\",\"arguments\":\"{\\\"search\\\":\\\"goo\\\"}\"}}]}}]}"));

        var response = await Client(handler).ChatAsync([AgentMessage.User("projects")], [new SchemaTool()], "gpt-4.1", CancellationToken.None);

        Assert.True(response.HasToolCalls);
        Assert.Equal("call_9", response.ToolCalls![0].Id);
        Assert.Equal("list_projects", response.ToolCalls[0].Name);
        Assert.Equal("{\"search\":\"goo\"}", response.ToolCalls[0].ArgumentsJson);
    }

    [Fact]
    public async Task AGeminiModel_NeverTouchesOpenAi()
    {
        var openAi = new RecordingHandler(_ => Http.Json(OpenAiText));
        var gemini = new RecordingHandler(_ => Http.Json(GeminiOk));

        var response = await Router(openAi, gemini).ChatAsync([AgentMessage.User("hi")], null, "gemini-flash-latest");

        Assert.Equal("from gemini", response.Content);
        Assert.Empty(openAi.Calls);
        Assert.Null(response.ModelNotice);
    }

    [Fact]
    public async Task AnOpenAiModel_IsAnsweredByOpenAi()
    {
        var openAi = new RecordingHandler(_ => Http.Json(OpenAiText));
        var gemini = new RecordingHandler(_ => Http.Json(GeminiOk));

        var response = await Router(openAi, gemini).ChatAsync([AgentMessage.User("hi")], null, "gpt-4.1");

        Assert.Equal("from openai", response.Content);
        Assert.Empty(gemini.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "key was rejected")]
    [InlineData(HttpStatusCode.TooManyRequests, "rate limiting")]
    [InlineData(HttpStatusCode.NotFound, "does not offer")]
    public async Task WhenOpenAiFails_GeminiAnswers_AndTheUserIsToldWhy(HttpStatusCode status, string reason)
    {
        var openAi = new RecordingHandler(_ => Http.Json("{\"error\":{\"message\":\"Incorrect API key provided: " + Key + "\"}}", status));
        var gemini = new RecordingHandler(_ => Http.Json(GeminiOk));

        var response = await Router(openAi, gemini).ChatAsync([AgentMessage.User("hi")], null, "gpt-4.1");

        Assert.Equal("from gemini", response.Content);
        Assert.Contains(reason, response.ModelNotice);
        Assert.Contains("gpt-4.1", response.ModelNotice);
        Assert.DoesNotContain(Key, response.ModelNotice);
    }

    [Fact]
    public async Task WithoutAnOpenAiKey_GeminiAnswers_AndTheNoticeSaysSo()
    {
        var openAi = new RecordingHandler(_ => Http.Json(OpenAiText));
        var gemini = new RecordingHandler(_ => Http.Json(GeminiOk));

        var response = await Router(openAi, gemini, key: null).ChatAsync([AgentMessage.User("hi")], null, "gpt-4.1");

        Assert.Equal("from gemini", response.Content);
        Assert.Contains("No OpenAI key is configured", response.ModelNotice);
        Assert.Empty(openAi.Calls);
    }
}

public class ModelAvailabilityTests
{
    private static IReadOnlyList<ProviderStatus> Describe(Dictionary<string, string?> settings)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var openAi = new OpenAiChatClient(new HttpClient(), config, NullLogger<OpenAiChatClient>.Instance);
        var groq = new GroqChatClient(new HttpClient(), config, NullLogger<GroqChatClient>.Instance);
        return ProviderAvailability.Describe(config, openAi, groq);
    }

    [Fact]
    public void ReportsWhichProvidersHaveAKey_NeverTheKeyItself()
    {
        var providers = Describe(new() { ["AI:ApiKey"] = "gemini-secret", ["OPENAI_API_KEY"] = "sk-secret", ["GROQ_API_KEY"] = "gsk-secret" });

        Assert.All(providers, p => Assert.True(p.Available));
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(providers));
    }

    [Fact]
    public void AProviderWithoutAKey_IsUnavailable_WithAReason()
    {
        var providers = Describe(new() { ["AI:ApiKey"] = "gemini-secret" });

        var openAi = providers.Single(p => p.Name == "OpenAI");
        Assert.False(openAi.Available);
        Assert.Contains("OPENAI_API_KEY", openAi.Reason);
        Assert.True(providers.Single(p => p.Name == "Google").Available);
        var groq = providers.Single(p => p.Name == "Groq");
        Assert.False(groq.Available);
        Assert.Contains("GROQ_API_KEY", groq.Reason);
    }
}
