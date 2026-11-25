using Xunit;
using System.Net;
using AIAgent.Application.Interfaces;
using AIAgent.Domain.Models;
using AIAgent.Infrastructure.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIAgent.Application.Tests;

public class ModelTimeoutTests
{
    private const string GeminiOk = "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"from flash\"}]}}]}";

    
    private sealed class HangingHandler(params string[] hanging) : HttpMessageHandler
    {
        public List<string> Models { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var model = request.RequestUri!.AbsolutePath.Split('/').Last().Split(':')[0];
            Models.Add(model);
            if (hanging.Contains(model)) await Task.Delay(Timeout.Infinite, cancellationToken);
            return Http.Json(GeminiOk);
        }
    }

    private static LlmProvider Provider(HttpMessageHandler handler, TimeSpan attempt, TimeSpan budget)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["AI:ApiKey"] = "key" }).Build();
        return new LlmProvider(new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromMinutes(5) }, config, NullLogger<LlmProvider>.Instance)
        {
            RetryDelay = TimeSpan.Zero,
            AttemptTimeout = attempt,
            Budget = budget
        };
    }

    [Fact]
    public async Task AModelThatHangs_IsGivenUpOn_AndTheNextModelAnswersWithoutRetryingTheSlowOne()
    {
        var handler = new HangingHandler("gemini-pro-latest");
        var provider = Provider(handler, TimeSpan.FromMilliseconds(150), TimeSpan.FromSeconds(10));

        var started = DateTime.UtcNow;
        var response = await provider.ChatAsync([AgentMessage.User("hi")], null, "gemini-pro-latest");

        Assert.Equal("from flash", response.Content);
        Assert.Null(response.FallbackReason);
        Assert.Equal(new[] { "gemini-pro-latest", "gemini-flash-latest" }, handler.Models);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task WhenEveryModelHangs_TheChatStopsWithinTheBudget_AndSaysTheModelWasTooSlow()
    {
        var handler = new HangingHandler("gemini-pro-latest", "gemini-flash-latest", "gemini-flash-lite-latest");
        var provider = Provider(handler, TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(400));

        var started = DateTime.UtcNow;
        var response = await provider.ChatAsync([AgentMessage.User("hi")], null, "gemini-pro-latest");

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(5));
        Assert.Contains("too long", response.FallbackReason);
    }

    [Fact]
    public async Task ACallerWhoCancels_StillCancels_InsteadOfFallingBack()
    {
        var handler = new HangingHandler("gemini-pro-latest");
        var provider = Provider(handler, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var response = await provider.ChatAsync([AgentMessage.User("hi")], null, "gemini-pro-latest", cts.Token);

        Assert.NotNull(response.FallbackReason);
        Assert.Single(handler.Models);
    }
}
