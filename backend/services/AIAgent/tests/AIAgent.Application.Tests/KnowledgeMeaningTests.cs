using System.Net;
using System.Text;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Infrastructure.Knowledge;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace AIAgent.Application.Tests;


internal sealed class ConceptEmbedder : IKnowledgeEmbedder
{
    
    private static readonly string[][] Groups =
    [
        ["resign", "quit", "notice", "leaving", "exit"],
        ["leave", "vacation", "holiday", "casual"],
        ["expense", "travel", "reimburse", "claim"],
        ["laptop", "device", "hardware", "equipment"]
    ];

    public bool Fails;
    public int PassageCalls;
    public bool Enabled => true;
    public string Model => "concepts";
    public int Dimensions => Groups.Length + 1;

    public Task<float[]?> EmbedQueryAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(Fails ? null : Vector(text));

    public Task<IReadOnlyList<float[]?>> EmbedPassagesAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        PassageCalls++;
        return Task.FromResult<IReadOnlyList<float[]?>>(texts.Select(t => Fails ? null : Vector(t)).ToArray());
    }

    private static float[] Vector(string text)
    {
        var lower = text.ToLowerInvariant();
        var v = new float[Groups.Length + 1];
        for (var g = 0; g < Groups.Length; g++) v[g] = Groups[g].Count(w => lower.Contains(w));
        if (v.All(x => x == 0)) v[^1] = 1;   
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        return v.Select(x => x / norm).ToArray();
    }
}

[Trait("Category", "Docker")]
public class KnowledgeMeaningTests(KnowledgeDatabase db) : IClassFixture<KnowledgeDatabase>
{
    private static long nextTenant = 5000;
    private readonly long tenant = Interlocked.Increment(ref nextTenant);

    private PostgresKnowledgeStore Store(IKnowledgeEmbedder embedder) =>
        new(Options.Create(new KnowledgeBaseOptions { ConnectionString = db.ConnectionString }), embedder);

    private static NewKnowledgeDocument Doc(string title, string text, params string[] roles) =>
        new(title, title.Replace(' ', '_') + ".md", "text/markdown", roles, text, KnowledgeChunker.Split(text));

    
    private const string Exit = "# Exit terms\n\nAn employee who resigns must give sixty days of warning before the last working day.";

    [DockerFact]
    public async Task AQuestionInOtherWords_FindsThePassage_ByMeaning()
    {
        var embedder = new ConceptEmbedder();
        var store = Store(embedder);
        await store.SaveAsync(tenant, 5, Doc("Exit terms", Exit));

        Assert.Empty(await store.SearchAsync(tenant, [], false, "how long before I can quit", 5));   
        Assert.Equal(1, await store.IndexPendingAsync(100));

        var found = await store.SearchAsync(tenant, [], false, "how long before I can quit", 5);
        Assert.Equal("Exit terms", Assert.Single(found).Title);
    }

    [DockerFact]
    public async Task IndexingPendingPassages_IsRepeatable_AndDoesNothingTwice()
    {
        var embedder = new ConceptEmbedder();
        var store = Store(embedder);
        await store.SaveAsync(tenant, 5, Doc("Exit terms", Exit));

        Assert.Equal(1, await store.IndexPendingAsync(100));
        Assert.Equal(0, await store.IndexPendingAsync(100));
        Assert.Equal(1, embedder.PassageCalls);
    }

    [DockerFact]
    public async Task WhenTheEmbeddingServiceFails_KeywordSearchStillAnswers_AndIndexingRetriesLater()
    {
        var embedder = new ConceptEmbedder { Fails = true };
        var store = Store(embedder);
        await store.SaveAsync(tenant, 5, Doc("Exit terms", Exit));

        Assert.Equal(0, await store.IndexPendingAsync(100));
        var byWord = await store.SearchAsync(tenant, [], false, "sixty days warning", 5);
        Assert.Single(byWord);

        embedder.Fails = false;
        Assert.Equal(1, await store.IndexPendingAsync(100));
        Assert.Single(await store.SearchAsync(tenant, [], false, "can I quit", 5));
    }

    [DockerFact]
    public async Task SearchByMeaning_KeepsTheRoleRule()
    {
        var store = Store(new ConceptEmbedder());
        await store.SaveAsync(tenant, 5, Doc("Exit terms", Exit, "HR"));
        await store.IndexPendingAsync(100);

        Assert.Empty(await store.SearchAsync(tenant, ["DEVELOPER"], false, "can I quit", 5));
        Assert.Single(await store.SearchAsync(tenant, ["HR"], false, "can I quit", 5));
        Assert.Single(await store.SearchAsync(tenant, [], true, "can I quit", 5));
    }

    [DockerFact]
    public async Task SearchByMeaning_NeverCrossesCompanies()
    {
        var store = Store(new ConceptEmbedder());
        await store.SaveAsync(tenant, 5, Doc("Exit terms", Exit));
        await store.IndexPendingAsync(100);

        Assert.Empty(await store.SearchAsync(tenant + 1, [], true, "can I quit", 5));
    }

    [DockerFact]
    public async Task APassageAboutSomethingElse_IsNotReturned_ForAQuestionItDoesNotAnswer()
    {
        var store = Store(new ConceptEmbedder());
        await store.SaveAsync(tenant, 5, Doc("Exit terms", Exit));
        await store.IndexPendingAsync(100);

        Assert.Empty(await store.SearchAsync(tenant, [], false, "which laptop do I get", 5));
    }

    [DockerFact]
    public async Task ARemovedOrReplacedDocument_LeavesNoVectorsBehind()
    {
        var embedder = new ConceptEmbedder();
        var store = Store(embedder);
        var saved = await store.SaveAsync(tenant, 5, Doc("Exit terms", Exit));
        await store.IndexPendingAsync(100);
        await store.DeleteAsync(tenant, saved.Document!.Id);

        Assert.Empty(await store.SearchAsync(tenant, [], true, "can I quit", 5));
        Assert.Equal(0, await store.IndexPendingAsync(100));
    }

    [Fact]
    public void Fusion_PutsAPassageBothSearchesLikedFirst()
    {
        KnowledgePassage P(long id, string text) => new(id, "T" + id, null, text, 0, 1, DateTimeOffset.UnixEpoch);
        var keyword = new[] { P(1, "a"), P(2, "b"), P(3, "c") };
        var meaning = new[] { P(3, "c"), P(4, "d") };

        var fused = PostgresKnowledgeStore.Fuse(keyword, meaning);

        Assert.Equal([3L, 1L, 2L, 4L], fused.Select(p => p.DocumentId).ToArray());
    }

    [Fact]
    public void Fusion_WithOnlyOneList_KeepsItsOrder()
    {
        KnowledgePassage P(long id) => new(id, "T", null, "x" + id, 0, 1, DateTimeOffset.UnixEpoch);
        Assert.Equal([5L, 6L], PostgresKnowledgeStore.Fuse([P(5), P(6)], []).Select(p => p.DocumentId).ToArray());
    }
}

public class GeminiKnowledgeEmbedderTests
{
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls;
        public string? LastUrl;
        public string? LastKeyHeader;
        public string? LastBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUrl = request.RequestUri!.ToString();
            LastKeyHeader = request.Headers.TryGetValues("x-goog-api-key", out var v) ? v.First() : null;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private static GeminiKnowledgeEmbedder Embedder(Handler handler, string? key = "secret-test-key", int dimensions = 3) =>
        new(new HttpClient(handler),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:ApiKey"] = key, ["KnowledgeBase:EmbeddingDimensions"] = dimensions.ToString()
            }).Build(),
            NullLogger<GeminiKnowledgeEmbedder>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Vectors_AreReturnedInOrder_AndMadeUnitLength()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.OK, """{"embeddings":[{"values":[3,4,0]},{"values":[0,0,2]}]}"""));
        var embedder = Embedder(handler);

        var result = await embedder.EmbedPassagesAsync(["one", "two"]);

        Assert.Equal(0.6f, result[0]![0], 3);
        Assert.Equal(0.8f, result[0]![1], 3);
        Assert.Equal(1f, result[1]![2], 3);
    }

    [Fact]
    public async Task TheKey_GoesInAHeader_NeverTheUrl()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.OK, """{"embeddings":[{"values":[1,0,0]}]}"""));
        await Embedder(handler).EmbedQueryAsync("hello");

        Assert.Equal("secret-test-key", handler.LastKeyHeader);
        Assert.DoesNotContain("secret-test-key", handler.LastUrl);
        Assert.Contains("RETRIEVAL_QUERY", handler.LastBody);
    }

    [Fact]
    public async Task WhenOverQuota_ItReturnsNothing_AndStopsAskingForAWhile()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.TooManyRequests, """{"error":{"message":"quota"}}"""));
        var embedder = Embedder(handler);

        Assert.Null(await embedder.EmbedQueryAsync("a question"));
        Assert.Null(await embedder.EmbedQueryAsync("another"));
        Assert.All(await embedder.EmbedPassagesAsync(["x", "y"]), v => Assert.Null(v));

        Assert.Equal(1, handler.Calls);   
    }

    [Fact]
    public async Task AWrongSizedAnswer_IsIgnored()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.OK, """{"embeddings":[{"values":[1,0]}]}"""));
        Assert.Null(await Embedder(handler).EmbedQueryAsync("hello"));
    }

    [Fact]
    public async Task WithoutAKey_ItIsOff_AndMakesNoCalls()
    {
        var handler = new Handler(_ => Json(HttpStatusCode.OK, "{}"));
        var embedder = Embedder(handler, key: null);

        Assert.False(embedder.Enabled);
        Assert.Null(await embedder.EmbedQueryAsync("hello"));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ANetworkFailure_IsNotAnError()
    {
        var handler = new Handler(_ => throw new HttpRequestException("offline"));
        Assert.Null(await Embedder(handler).EmbedQueryAsync("hello"));
    }
}
