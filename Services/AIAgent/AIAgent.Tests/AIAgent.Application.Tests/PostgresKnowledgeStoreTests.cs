using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Infrastructure.Knowledge;
using Microsoft.Extensions.Options;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace AIAgent.Application.Tests;





public sealed class KnowledgeDatabase : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("pgvector/pgvector:pg17").Build();
    public string ConnectionString => postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        var folder = Find(new DirectoryInfo(AppContext.BaseDirectory));
        await postgres.ExecScriptAsync("CREATE EXTENSION IF NOT EXISTS vector;");
        foreach (var script in Directory.GetFiles(folder, "*.sql").Order(StringComparer.Ordinal))
        {
            var result = await postgres.ExecScriptAsync(await File.ReadAllTextAsync(script));
            Assert.True(result.ExitCode == 0, $"{Path.GetFileName(script)} failed: {result.Stderr}");
        }
    }

    public Task DisposeAsync() => postgres.DisposeAsync().AsTask();

    private static string Find(DirectoryInfo? from)
    {
        for (var dir = from; dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Services", "AIAgent", "Database", "postgres");
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException("Services/AIAgent/Database/postgres was not found above the test folder.");
    }
}

[Trait("Category", "Docker")]
public class PostgresKnowledgeStoreTests(KnowledgeDatabase db) : IClassFixture<KnowledgeDatabase>
{
    private static long nextTenant = 1000;

    private readonly long tenant = Interlocked.Increment(ref nextTenant);

    private PostgresKnowledgeStore Store() =>
        new(Options.Create(new KnowledgeBaseOptions { ConnectionString = db.ConnectionString }));

    private static NewKnowledgeDocument Doc(string title, string text, params string[] roles) =>
        new(title, title.Replace(' ', '_') + ".md", "text/markdown", roles, text, KnowledgeChunker.Split(text));

    private const string Leave =
        "# Leave policy\n\nEmployees receive 12 casual leaves each year.\n\n## Carry forward\n\nUp to 5 unused days carry over to the next year.\n\n## Notice period\n\nThe notice period for resignation is 60 days.";

    private async Task<long> Count(string sql)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    [DockerFact]
    public async Task ASavedDocument_IsFound_WithItsTitleSectionAndText()
    {
        var store = Store();
        var saved = await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave));

        Assert.Equal(KnowledgeSaveStatus.Added, saved.Status);
        var found = await store.SearchAsync(tenant, ["DEVELOPER"], false, "notice period resignation", 5);

        var top = found[0];
        Assert.Equal("Leave Policy", top.Title);
        Assert.Equal("Notice period", top.Section);
        Assert.Contains("60 days", top.Text);
        Assert.DoesNotContain("Notice period\n", top.Text);   
        Assert.Equal(1, top.Version);
    }

    [DockerFact]
    public async Task AQuestionInOrdinaryWords_StillFindsThePassage()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave));

        
        var found = await store.SearchAsync(tenant, [], false, "how many casual leaves do I get per year", 5);

        Assert.Contains(found, p => p.Text.Contains("12 casual leaves"));
    }

    [DockerFact]
    public async Task WordForms_MatchEachOther()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave));

        Assert.NotEmpty(await store.SearchAsync(tenant, [], false, "carrying forward unused day", 5));
    }

    [DockerFact]
    public async Task ASearchWithNoMatch_GivesNothing()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave));

        Assert.Empty(await store.SearchAsync(tenant, [], false, "spaceship refuelling", 5));
    }

    [DockerTheory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("the and of")]
    [InlineData("'; DROP TABLE documents; --")]
    [InlineData("a & b | !c <-> ((")]
    public async Task OddQueries_NeverFailOrReturnRubbish(string query)
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave));

        Assert.Empty(await store.SearchAsync(tenant, [], false, query, 5));
        Assert.True(await Count("SELECT count(*) FROM documents") > 0);   
    }

    [DockerFact]
    public async Task OneCompany_NeverSeesAnothersDocuments()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave));
        var other = tenant + 100_000;

        Assert.Empty(await store.SearchAsync(other, ["ADMIN"], true, "notice period", 5));
        Assert.Empty(await store.ListAsync(other));
        Assert.False(await store.DeleteAsync(other, (await store.ListAsync(tenant)).Single().Id));
        Assert.Single(await store.ListAsync(tenant));
    }

    [DockerFact]
    public async Task TwoCompanies_CanUseTheSameTitle()
    {
        var store = Store();
        var other = tenant + 100_000;

        var a = await store.SaveAsync(tenant, 5, Doc("Leave Policy", "# A\n\nCompany A gives 10 days."));
        var b = await store.SaveAsync(other, 9, Doc("Leave Policy", "# B\n\nCompany B gives 20 days."));

        Assert.Equal(KnowledgeSaveStatus.Added, a.Status);
        Assert.Equal(KnowledgeSaveStatus.Added, b.Status);
        Assert.Contains("10 days", (await store.SearchAsync(tenant, [], false, "days", 5)).Single().Text);
        Assert.Contains("20 days", (await store.SearchAsync(other, [], false, "days", 5)).Single().Text);
    }

    [DockerFact]
    public async Task ARestrictedDocument_IsReadOnlyByItsRoles_AndByAdminAndHr()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Salary Bands", "# Bands\n\nSenior engineers are in band L5.", "MANAGER", "ENGINEERING_MANAGER"));
        await store.SaveAsync(tenant, 5, Doc("Dress Code", "# Dress\n\nSmart casual band applies to all."));

        string[] Titles(IReadOnlyList<KnowledgePassage> p) => p.Select(x => x.Title).Order().ToArray();

        Assert.Equal(["Dress Code"], Titles(await store.SearchAsync(tenant, ["DEVELOPER"], false, "band", 5)));
        Assert.Equal(["Dress Code"], Titles(await store.SearchAsync(tenant, [], false, "band", 5)));
        Assert.Equal(["Dress Code", "Salary Bands"], Titles(await store.SearchAsync(tenant, ["MANAGER"], false, "band", 5)));
        Assert.Equal(["Dress Code", "Salary Bands"], Titles(await store.SearchAsync(tenant, ["developer", "engineering_manager"], false, "band", 5)));
        Assert.Equal(["Dress Code", "Salary Bands"], Titles(await store.SearchAsync(tenant, ["HUMAN_RESOURCE"], true, "band", 5)));
    }

    [DockerFact]
    public async Task TheSameTitleAgain_ReplacesTheDocument_AndOnlyTheNewTextIsSearched()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Notice", "# Notice\n\nThe notice period is 30 days."));

        var again = await store.SaveAsync(tenant, 6, Doc("notice", "# Notice\n\nThe notice period is 90 days."));   

        Assert.Equal(KnowledgeSaveStatus.Replaced, again.Status);
        Assert.Equal(2, again.Document!.Version);
        var all = await store.ListAsync(tenant);
        Assert.Equal(2, Assert.Single(all).Version);
        var found = await store.SearchAsync(tenant, [], false, "notice period", 5);
        Assert.Contains("90 days", Assert.Single(found).Text);
        Assert.Equal(2, found[0].Version);
        Assert.Equal(6, all[0].UploadedBy);
    }

    [DockerFact]
    public async Task OldVersions_KeepNoPassages_AndOnlyTheLastFewAreKept()
    {
        var store = Store();
        for (var i = 1; i <= 8; i++) await store.SaveAsync(tenant, 5, Doc("Rolling", $"# Rolling\n\nVersion number {i} of the text."));

        var id = (await store.ListAsync(tenant)).Single().Id;
        Assert.Equal(5, await Count($"SELECT count(*) FROM document_versions WHERE document_id = {id}"));
        Assert.Equal(1, await Count($"SELECT count(*) FROM document_chunks c JOIN document_versions v ON v.id = c.document_version_id WHERE v.document_id = {id}"));
    }

    [DockerFact]
    public async Task TheSameFileAndAudienceAgain_ChangesNothing()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave, "MANAGER"));

        var again = await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave, "MANAGER"));

        Assert.Equal(KnowledgeSaveStatus.Unchanged, again.Status);
        Assert.Equal(1, again.Document!.Version);
    }

    [DockerFact]
    public async Task ChangingOnlyTheAudience_TakesEffectWithoutANewVersion()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave, "MANAGER"));
        Assert.Empty(await store.SearchAsync(tenant, ["DEVELOPER"], false, "notice period", 5));

        var opened = await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave));

        Assert.Equal(KnowledgeSaveStatus.Replaced, opened.Status);
        Assert.Equal(1, opened.Document!.Version);
        Assert.NotEmpty(await store.SearchAsync(tenant, ["DEVELOPER"], false, "notice period", 5));
    }

    [DockerFact]
    public async Task Deleting_RemovesTheDocumentItsVersionsAndPassages()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave));
        await store.SaveAsync(tenant, 5, Doc("Leave Policy", Leave + "\n\n## Extra\n\nMore text."));
        var id = (await store.ListAsync(tenant)).Single().Id;

        Assert.True(await store.DeleteAsync(tenant, id));

        Assert.Empty(await store.SearchAsync(tenant, [], true, "notice period", 5));
        Assert.Empty(await store.ListAsync(tenant));
        Assert.Equal(0, await Count($"SELECT count(*) FROM document_versions WHERE document_id = {id}"));
        Assert.False(await store.DeleteAsync(tenant, id));
    }

    [DockerFact]
    public async Task TheListShowsEachDocumentOnceWithItsDetails()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("B Policy", Leave, "HR"));
        await store.SaveAsync(tenant, 5, Doc("a Policy", "# A\n\nShort."));

        var list = await store.ListAsync(tenant);

        Assert.Equal(["a Policy", "B Policy"], list.Select(d => d.Title));
        Assert.Equal(["HR"], list[1].AllowedRoles);
        Assert.Equal("B_Policy.md", list[1].FileName);
        Assert.True(list[1].Passages >= 1);
        Assert.Equal(Leave.Length, list[1].Characters);
    }

    [DockerFact]
    public async Task TheLimitOnPassages_IsRespected_AndBestComesFirst()
    {
        var store = Store();
        for (var i = 0; i < 6; i++) await store.SaveAsync(tenant, 5, Doc($"Doc {i}", $"# Topic\n\nBadge badge badge number {i}."));
        await store.SaveAsync(tenant, 5, Doc("Weak", "# Other\n\nA single badge here and lots of other words that dilute it a great deal indeed."));

        var found = await store.SearchAsync(tenant, [], false, "badge", 3);

        Assert.Equal(3, found.Count);
        Assert.DoesNotContain(found, p => p.Title == "Weak");
        Assert.True(found.Zip(found.Skip(1)).All(pair => pair.First.Rank >= pair.Second.Rank));
    }

    [DockerFact]
    public async Task TwoUploadsOfOneTitleAtOnce_MakeOneDocument()
    {
        var store = Store();

        var results = await Task.WhenAll(
            store.SaveAsync(tenant, 5, Doc("Race", "# Race\n\nFirst text about racing.")),
            store.SaveAsync(tenant, 6, Doc("Race", "# Race\n\nSecond text about racing.")));

        Assert.All(results, r => Assert.NotEqual(KnowledgeSaveStatus.Full, r.Status));
        var list = await store.ListAsync(tenant);
        Assert.Equal(2, Assert.Single(list).Version);
        Assert.Single(await store.SearchAsync(tenant, [], false, "racing", 5));
    }

    [DockerFact]
    public async Task AnUnreachableDatabase_IsReportedAsUnavailable_NotACrash()
    {
        var store = new PostgresKnowledgeStore(Options.Create(new KnowledgeBaseOptions { ConnectionString = "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x" }));

        await Assert.ThrowsAsync<KnowledgeUnavailableException>(() => store.SearchAsync(1, [], false, "policy", 5));
        await Assert.ThrowsAsync<KnowledgeUnavailableException>(() => store.ListAsync(1));
        await Assert.ThrowsAsync<KnowledgeUnavailableException>(() => store.SaveAsync(1, 1, Doc("A", "# A\n\nText.")));
        await Assert.ThrowsAsync<KnowledgeUnavailableException>(() => store.DeleteAsync(1, 1));
    }

    [DockerFact]
    public async Task APassageSavedWithoutATitle_IsStillFound()
    {
        var store = Store();
        await store.SaveAsync(tenant, 5, Doc("Plain", "Reimbursement requests must be filed within thirty days."));

        var found = await store.SearchAsync(tenant, [], false, "reimbursement", 5);

        Assert.Null(Assert.Single(found).Section);
        Assert.Contains("thirty days", found[0].Text);
    }
}

public class UnavailableKnowledgeStoreTests
{
    [Fact]
    public async Task EveryCall_SaysThatDocumentsAreNotSetUp()
    {
        var store = new UnavailableKnowledgeStore();

        var ex = await Assert.ThrowsAsync<KnowledgeUnavailableException>(() => store.SearchAsync(1, [], false, "x", 5));
        Assert.Contains("not set up", ex.Message);
        await Assert.ThrowsAsync<KnowledgeUnavailableException>(() => store.ListAsync(1));
        await Assert.ThrowsAsync<KnowledgeUnavailableException>(() => store.DeleteAsync(1, 1));
    }
}
