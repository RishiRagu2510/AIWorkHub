using System.Text.Json;
using System.Text.Json.Nodes;
using AIAgent.Api.Controllers;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.History;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIAgent.Application.Tests;

public class ChatHistoryRulesTests
{
    private static List<JsonElement> Messages(string json) => JsonSerializer.Deserialize<List<JsonElement>>(json)!;
    private const long Now = 1_800_000_000_000;

    [Fact]
    public void KeepsRoleTextTimeAndEverythingElseInExtra_AndRebuildsItExactly()
    {
        var chat = ChatHistoryRules.TryBuild("sess_1", null, Now - 5,
            Messages("[{\"role\":\"user\",\"content\":\"show my issues\",\"time\":\"02:11 PM\"}," +
                     "{\"role\":\"assistant\",\"content\":\"Here.\",\"time\":\"02:11 PM\",\"notice\":\"slow\",\"actions\":[{\"toolName\":\"search_issues\",\"result\":{\"items\":[{\"issueKey\":\"A-1\"}]}}],\"restored\":true}]"),
            Now, out var error);

        Assert.NotNull(chat);
        Assert.Equal("", error);
        Assert.Equal("show my issues", chat!.Title);
        Assert.Equal(Now - 5, chat.UpdatedAt);
        Assert.Equal(2, chat.Messages.Count);
        Assert.Null(chat.Messages[0].ExtraJson);
        Assert.DoesNotContain("restored", chat.Messages[1].ExtraJson!);

        var back = ChatHistoryRules.ToClientMessage(chat.Messages[1]);
        Assert.Equal("assistant", (string?)back["role"]);
        Assert.Equal("Here.", (string?)back["content"]);
        Assert.Equal("slow", (string?)back["notice"]);
        Assert.Equal("A-1", (string?)back["actions"]![0]!["result"]!["items"]![0]!["issueKey"]);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"role\":\"system\",\"content\":\"x\"}]")]
    [InlineData("[{\"role\":\"tool\",\"content\":\"x\"}]")]
    [InlineData("[{\"content\":\"no role\"}]")]
    [InlineData("[\"not an object\"]")]
    public void RefusesAChatItCannotTrust(string json)
    {
        Assert.Null(ChatHistoryRules.TryBuild("c1", "t", Now, Messages(json), Now, out var error));
        Assert.NotEqual("", error);
    }

    [Fact]
    public void RefusesTooManyMessagesAndTooLongText()
    {
        var many = "[" + string.Join(",", Enumerable.Repeat("{\"role\":\"user\",\"content\":\"x\"}", ChatHistoryRules.MaxMessages + 1)) + "]";
        Assert.Null(ChatHistoryRules.TryBuild("c1", "t", Now, Messages(many), Now, out _));

        var long1 = "[{\"role\":\"user\",\"content\":\"" + new string('x', ChatHistoryRules.MaxContentChars + 1) + "\"}]";
        Assert.Null(ChatHistoryRules.TryBuild("c1", "t", Now, Messages(long1), Now, out _));
    }

    [Theory]
    [InlineData("sess_ab12")]
    [InlineData("A-b_9")]
    public void AcceptsSimpleIds(string id) => Assert.True(ChatHistoryRules.IsValidChatId(id));

    [Theory]
    [InlineData("")]
    [InlineData("a b")]
    [InlineData("../x")]
    [InlineData("a:b")]
    [InlineData("1:2:3")]
    public void RejectsIdsThatCouldReachAnotherUsersDocument(string id) => Assert.False(ChatHistoryRules.IsValidChatId(id));

    [Fact]
    public void ACloudyClockCannotPinAChatToTheTop()
    {
        var chat = ChatHistoryRules.TryBuild("c1", "t", Now + 10L * 86_400_000, Messages("[{\"role\":\"user\",\"content\":\"hi\"}]"), Now, out _);
        Assert.Equal(Now, chat!.UpdatedAt);
    }

    [Fact]
    public void TheTitleIsTheFirstQuestion_Shortened()
    {
        var chat = ChatHistoryRules.TryBuild("c1", "  ", Now, Messages("[{\"role\":\"user\",\"content\":\"" + new string('a', 300) + "\"}]"), Now, out _);
        Assert.Equal(ChatHistoryRules.MaxTitleChars, chat!.Title.Length);
    }
}

public class ChatHistoryControllerTests
{
    private sealed class MemoryStore : IChatHistoryStore
    {
        public readonly Dictionary<(long, long, string), StoredChat> Chats = new();
        public readonly Dictionary<(long, long), string> Consent = new();
        public bool Down;

        private void Check() { if (Down) throw new ChatHistoryUnavailableException("down"); }
        public Task<string?> GetConsentAsync(long t, long u, CancellationToken c = default) { Check(); return Task.FromResult(Consent.GetValueOrDefault((t, u))); }
        public Task SetConsentAsync(long t, long u, string v, CancellationToken c = default) { Check(); Consent[(t, u)] = v; return Task.CompletedTask; }
        public Task<IReadOnlyList<ChatSummary>> ListAsync(long t, long u, CancellationToken c = default)
        {
            Check();
            IReadOnlyList<ChatSummary> list = Chats.Where(k => k.Key.Item1 == t && k.Key.Item2 == u).Select(k => k.Value)
                .OrderByDescending(x => x.UpdatedAt).Select(x => new ChatSummary(x.Id, x.Title, x.UpdatedAt, x.Messages.Count)).ToList();
            return Task.FromResult(list);
        }
        public Task<StoredChat?> GetAsync(long t, long u, string id, CancellationToken c = default) { Check(); return Task.FromResult(Chats.GetValueOrDefault((t, u, id))); }
        public Task SaveAsync(long t, long u, StoredChat chat, int max, CancellationToken c = default) { Check(); Chats[(t, u, chat.Id)] = chat; return Task.CompletedTask; }
        public Task<bool> DeleteAsync(long t, long u, string id, CancellationToken c = default) { Check(); return Task.FromResult(Chats.Remove((t, u, id))); }
        public Task<long> ClearAsync(long t, long u, CancellationToken c = default)
        {
            Check();
            var keys = Chats.Keys.Where(k => k.Item1 == t && k.Item2 == u).ToList();
            keys.ForEach(k => Chats.Remove(k));
            return Task.FromResult((long)keys.Count);
        }
    }

    private sealed class Identity(VerifiedCaller? caller, bool credentials = true) : ICallerIdentityProvider
    {
        public bool HasCredentials => credentials;
        public Task<VerifiedCaller?> VerifyAsync(CancellationToken ct = default) => Task.FromResult(caller);
    }

    private static VerifiedCaller Caller(long user, long tenant = 1) => new(user, "DEVELOPER", ["DEVELOPER"], [], tenant);
    private static ChatHistoryController Controller(MemoryStore store, VerifiedCaller? caller, bool credentials = true) =>
        new(store, new Identity(caller, credentials), NullLogger<ChatHistoryController>.Instance);

    private static ChatHistoryController.SaveChatRequest Body(string text = "hello") =>
        new("T", null, JsonSerializer.Deserialize<List<JsonElement>>("[{\"role\":\"user\",\"content\":\"" + text + "\"},{\"role\":\"assistant\",\"content\":\"hi\"}]"));

    [Fact]
    public async Task SavesAndReadsBackAChat()
    {
        var store = new MemoryStore();
        var c = Controller(store, Caller(7));

        Assert.IsType<OkObjectResult>(await c.Save("sess_1", Body("show my issues"), default));
        var list = (OkObjectResult)await c.List(default);
        Assert.Contains("\"Title\":\"T\"", JsonSerializer.Serialize(list.Value));

        var get = Assert.IsType<OkObjectResult>(await c.Get("sess_1", default));
        Assert.Contains("\"role\":\"assistant\"", JsonSerializer.Serialize(get.Value));
    }

    [Fact]
    public async Task OneUserCannotSeeOrDeleteAnotherUsersChats_EvenInTheSameTenant()
    {
        var store = new MemoryStore();
        await Controller(store, Caller(7)).Save("sess_1", Body("secret salary question"), default);

        var other = Controller(store, Caller(8));
        Assert.IsType<NotFoundResult>(await other.Get("sess_1", default));
        Assert.IsType<NotFoundResult>(await other.Delete("sess_1", default));
        Assert.Equal("{\"chats\":[]}", JsonSerializer.Serialize(((OkObjectResult)await other.List(default)).Value));
        Assert.Contains("\"deleted\":0", JsonSerializer.Serialize(((OkObjectResult)await other.Clear(default)).Value));
        Assert.Single(store.Chats);
    }

    [Fact]
    public async Task ATenantsChatsAreNotVisibleInAnotherTenant_EvenWithTheSameUserId()
    {
        var store = new MemoryStore();
        await Controller(store, Caller(7, tenant: 1)).Save("sess_1", Body(), default);

        Assert.IsType<NotFoundResult>(await Controller(store, Caller(7, tenant: 2)).Get("sess_1", default));
    }

    [Fact]
    public async Task WithoutAVerifiedCaller_NothingIsReadOrWritten()
    {
        var store = new MemoryStore();

        Assert.IsType<UnauthorizedObjectResult>(await Controller(store, null, credentials: false).List(default));
        Assert.IsType<UnauthorizedObjectResult>(await Controller(store, null).Save("sess_1", Body(), default));
        Assert.IsType<UnauthorizedObjectResult>(await Controller(store, new VerifiedCaller(7, "DEVELOPER", ["DEVELOPER"], [], 0)).List(default));
        Assert.Empty(store.Chats);
    }

    [Fact]
    public async Task BadInputIsRejected()
    {
        var c = Controller(new MemoryStore(), Caller(7));

        Assert.IsType<BadRequestObjectResult>(await c.Save("../x", Body(), default));
        Assert.IsType<BadRequestObjectResult>(await c.Save("sess_1", new ChatHistoryController.SaveChatRequest("T", null, []), default));
        Assert.IsType<BadRequestObjectResult>(await c.SetConsent(new ChatHistoryController.ConsentRequest("maybe"), default));
        Assert.IsType<NotFoundResult>(await c.Get("../x", default));
    }

    [Fact]
    public async Task ConsentIsRememberedPerUser()
    {
        var store = new MemoryStore();
        var c = Controller(store, Caller(7));
        Assert.Contains("null", JsonSerializer.Serialize(((OkObjectResult)await c.GetConsent(default)).Value));

        await c.SetConsent(new ChatHistoryController.ConsentRequest("yes"), default);
        Assert.Contains("yes", JsonSerializer.Serialize(((OkObjectResult)await c.GetConsent(default)).Value));
        Assert.Contains("null", JsonSerializer.Serialize(((OkObjectResult)await Controller(store, Caller(8)).GetConsent(default)).Value));
    }

    [Fact]
    public async Task ClearRemovesOnlyTheCallersChats()
    {
        var store = new MemoryStore();
        await Controller(store, Caller(7)).Save("a", Body(), default);
        await Controller(store, Caller(7)).Save("b", Body(), default);
        await Controller(store, Caller(8)).Save("c", Body(), default);

        var result = (OkObjectResult)await Controller(store, Caller(7)).Clear(default);

        Assert.Contains("2", JsonSerializer.Serialize(result.Value));
        Assert.Single(store.Chats);
    }

    [Fact]
    public async Task WhenTheStoreIsDown_ItAnswers503WithAReason()
    {
        var c = Controller(new MemoryStore { Down = true }, Caller(7));

        var result = Assert.IsType<ObjectResult>(await c.List(default));
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task WithNoMongoConfigured_ThePlainStoreSaysSo()
    {
        var ex = await Assert.ThrowsAsync<ChatHistoryUnavailableException>(() => new UnavailableChatHistoryStore().ListAsync(1, 1));
        Assert.Contains("Mongo:ConnectionString", ex.Message);
    }
}





public class MongoChatHistoryStoreIntegrationTests
{
    [Fact]
    public async Task RoundTripsIsolatesAndTrims()
    {
        var connection = Environment.GetEnvironmentVariable("AIWORKHUB_TEST_MONGO");
        if (string.IsNullOrWhiteSpace(connection)) return;

        var store = new MongoChatHistoryStore(new ChatHistoryOptions(connection, "ai_agent_db", 90), NullLogger<MongoChatHistoryStore>.Instance);
        var tenant = 900_000_000_000L + Random.Shared.NextInt64(1, 1_000_000);
        try
        {
            StoredChat Chat(string id, long at, string text) => new(id, text, at, [new("user", text, "t", null), new("assistant", "ok", "t", "{\"notice\":\"n\",\"actions\":[{\"result\":{\"$weird.key\":1}}]}")]);

            Assert.Null(await store.GetConsentAsync(tenant, 1));
            await store.SetConsentAsync(tenant, 1, "yes");
            Assert.Equal("yes", await store.GetConsentAsync(tenant, 1));
            Assert.Null(await store.GetConsentAsync(tenant, 2));

            await store.SaveAsync(tenant, 1, Chat("a", 100, "first"), 3);
            await store.SaveAsync(tenant, 1, Chat("a", 150, "first, edited"), 3);   
            await store.SaveAsync(tenant, 1, Chat("b", 200, "second"), 3);
            await store.SaveAsync(tenant, 2, Chat("a", 300, "someone else"), 3);

            var list = await store.ListAsync(tenant, 1);
            Assert.Equal(new[] { "b", "a" }, list.Select(x => x.Id).ToArray());
            Assert.Equal("first, edited", list[1].Title);
            Assert.Equal(2, list[1].MessageCount);

            var back = await store.GetAsync(tenant, 1, "a");
            Assert.Equal(2, back!.Messages.Count);
            Assert.Contains("$weird.key", back.Messages[1].ExtraJson);
            Assert.Null(await store.GetAsync(tenant, 3, "a"));
            Assert.Equal("someone else", (await store.GetAsync(tenant, 2, "a"))!.Title);

            
            await store.SaveAsync(tenant, 1, Chat("c", 300, "third"), 3);
            await store.SaveAsync(tenant, 1, Chat("d", 400, "fourth"), 3);
            Assert.Equal(new[] { "d", "c", "b" }, (await store.ListAsync(tenant, 1)).Select(x => x.Id).ToArray());

            Assert.True(await store.DeleteAsync(tenant, 1, "d"));
            Assert.False(await store.DeleteAsync(tenant, 1, "d"));
            Assert.Equal(2, await store.ClearAsync(tenant, 1));
            Assert.Single(await store.ListAsync(tenant, 2));
        }
        finally
        {
            await store.ClearAsync(tenant, 1);
            await store.ClearAsync(tenant, 2);
        }
    }
}
