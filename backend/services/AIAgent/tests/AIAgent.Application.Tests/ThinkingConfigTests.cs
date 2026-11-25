using System.Text.Json;
using AIAgent.Domain.Models;
using AIAgent.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIAgent.Application.Tests;

public class ThinkingConfigTests
{
    private const string GeminiOk = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"ok\"}]}}]}";

    private static async Task<JsonElement> SentBodyAsync(Dictionary<string, string?> settings)
    {
        string? sent = null;
        var handler = new RecordingHandler(_ => Http.Json(GeminiOk));
        settings["AI:ApiKey"] = "key";
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var provider = new LlmProvider(new HttpClient(handler, disposeHandler: false), config, NullLogger<LlmProvider>.Instance);

        await provider.ChatAsync([AgentMessage.User("hi")]);

        sent = handler.Calls.Single().Body;
        Assert.NotNull(sent);
        return JsonDocument.Parse(sent!).RootElement.Clone();
    }

    [Fact]
    public async Task NothingIsSentByDefault_SoGoogleKeepsDecidingHowLongToThink()
    {
        var body = await SentBodyAsync([]);
        Assert.False(body.TryGetProperty("generationConfig", out _));
    }

    [Theory]
    [InlineData("low")]
    [InlineData("minimal")]
    [InlineData("MEDIUM")]
    public async Task ALevelIsPassedOn(string level)
    {
        var body = await SentBodyAsync(new() { ["AI:ThinkingLevel"] = level });
        Assert.Equal(level.ToLowerInvariant(), body.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
    }

    [Fact]
    public async Task ABudgetIsPassedOn_IncludingZeroForNoThinking()
    {
        var body = await SentBodyAsync(new() { ["AI:ThinkingBudget"] = "0" });
        Assert.Equal(0, body.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32());
    }

    [Fact]
    public async Task TheLevelWinsWhenBothAreSet()
    {
        var body = await SentBodyAsync(new() { ["AI:ThinkingLevel"] = "low", ["AI:ThinkingBudget"] = "512" });
        var thinking = body.GetProperty("generationConfig").GetProperty("thinkingConfig");
        Assert.True(thinking.TryGetProperty("thinkingLevel", out _));
        Assert.False(thinking.TryGetProperty("thinkingBudget", out _));
    }

    [Theory]
    [InlineData("AI:ThinkingLevel", "extreme")]
    [InlineData("AI:ThinkingLevel", "")]
    [InlineData("AI:ThinkingBudget", "lots")]
    [InlineData("AI:ThinkingBudget", "-5")]
    public async Task ANonsenseValueIsIgnoredRatherThanSentToGoogle(string key, string value)
    {
        var body = await SentBodyAsync(new() { [key] = value });
        Assert.False(body.TryGetProperty("generationConfig", out _));
    }
}
