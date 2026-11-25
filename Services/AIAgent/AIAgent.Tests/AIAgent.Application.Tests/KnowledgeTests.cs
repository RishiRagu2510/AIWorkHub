using System.Text;
using System.Text.Json;
using AIAgent.Api.Controllers;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Infrastructure.Knowledge;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AIAgent.Application.Tests;

public class KnowledgeChunkerTests
{
    private static string[] Words(string text) =>
        text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Select(w => w.Trim('#')).Where(w => w.Length > 0).ToArray();

    private static void AssertNothingLost(string input, IReadOnlyList<KnowledgeChunk> chunks)
    {
        var kept = new HashSet<string>(chunks.SelectMany(c => Words(KnowledgeChunker.Stored(c))));
        Assert.All(Words(input), w => Assert.Contains(w, kept));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    public void NoText_GivesNoPassages(string? text) => Assert.Empty(KnowledgeChunker.Split(text));

    [Fact]
    public void MarkdownHeadings_LabelThePassagesUnderThem()
    {
        var chunks = KnowledgeChunker.Split("# Leave Policy\n\nEmployees get 18 days of leave a year.\n\n## Carry forward\n\nUp to 5 days carry over.");

        Assert.Equal(2, chunks.Count);
        Assert.Equal("Leave Policy", chunks[0].Heading);
        Assert.Contains("18 days", chunks[0].Text);
        Assert.Equal("Carry forward", chunks[1].Heading);
        Assert.Contains("5 days", chunks[1].Text);
    }

    [Fact]
    public void ATitleWithItsTextRightUnderneath_IsStillATitle()
    {
        var chunks = KnowledgeChunker.Split("## Notice period\nThe notice period is 60 days.\n## Probation\nProbation lasts 3 months.");

        Assert.Equal(["Notice period", "Probation"], chunks.Select(c => c.Heading));
        Assert.Equal("The notice period is 60 days.", chunks[0].Text);
    }

    [Fact]
    public void AllCapsAndNumberedLines_AreTitles_ButSentencesAreNot()
    {
        var chunks = KnowledgeChunker.Split("TRAVEL POLICY\nFlights are booked by the travel desk.\n2.1 Hotel limits\nHotels are capped at 6000 a night.\nApproved by HR.");

        Assert.Equal(["TRAVEL POLICY", "2.1 Hotel limits"], chunks.Select(c => c.Heading));
        Assert.Contains("Approved by HR.", chunks[1].Text);
    }

    [Fact]
    public void APassage_NeverCrossesATitle()
    {
        var chunks = KnowledgeChunker.Split("# One\n\nAlpha beta.\n\n# Two\n\nGamma delta.");

        Assert.DoesNotContain(chunks, c => c.Text.Contains("Alpha") && c.Text.Contains("Gamma"));
    }

    [Fact]
    public void ALongDocument_IsCutIntoPassages_OfASensibleSize_WithNothingLost()
    {
        var paragraph = string.Join(" ", Enumerable.Range(1, 60).Select(i => $"Sentence number {i} says something useful about the policy."));
        var text = "# Handbook\n\n" + string.Join("\n\n", Enumerable.Repeat(paragraph, 6));

        var chunks = KnowledgeChunker.Split(text);

        Assert.True(chunks.Count > 6);
        Assert.All(chunks, c => Assert.InRange(c.Text.Length, 1, KnowledgeChunker.Hard));
        AssertNothingLost(text, chunks);
    }

    [Fact]
    public void ALongRunWithNoSpaces_IsStillCut()
    {
        var chunks = KnowledgeChunker.Split(new string('x', 5000));

        Assert.All(chunks, c => Assert.True(c.Text.Length <= KnowledgeChunker.Hard));
        Assert.Equal(5000, chunks.Sum(c => c.Text.Length));
    }

    [Fact]
    public void TitlesWithNothingUnderThem_KeepTheirWords()
    {
        var text = "1. Overview\n\n1.1 Scope\n\n2. Steps\n3. Approval";

        AssertNothingLost(text, KnowledgeChunker.Split(text));
    }

    [Fact]
    public void WindowsLineEndings_AreHandled()
    {
        var chunks = KnowledgeChunker.Split("# Policy\r\n\r\nFirst rule.\r\n\r\nSecond rule.");

        Assert.Equal("Policy", Assert.Single(chunks).Heading);
        Assert.Contains("Second rule.", chunks[0].Text);
    }

    [Fact]
    public void AStoredPassage_StartsWithItsTitle_SoASearchForTheTopicFindsIt() =>
        Assert.Equal("Notice period\nSixty days.", KnowledgeChunker.Stored(new KnowledgeChunk("Notice period", "Sixty days.")));

    [Fact]
    public void APassageWithNoTitle_IsStoredAsItIs() =>
        Assert.Equal("Sixty days.", KnowledgeChunker.Stored(new KnowledgeChunk(null, "Sixty days.")));
}

public class KnowledgeRulesTests
{
    [Theory]
    [InlineData("ADMIN", true)]
    [InlineData("hr", true)]
    [InlineData("HUMAN_RESOURCE", true)]
    [InlineData("HR_ADMIN", true)]
    [InlineData("MANAGER", false)]
    [InlineData("DEVELOPER", false)]
    [InlineData("TALENT_ACQUISITION", false)]
    public void OnlyAdminAndHr_CanManageDocuments(string role, bool expected) =>
        Assert.Equal(expected, KnowledgeRules.CanManage([role]));

    [Fact]
    public void NoRoles_CannotManage() => Assert.False(KnowledgeRules.CanManage([]));

    [Fact]
    public void ARoleList_BecomesCleanUpperCaseCodes()
    {
        var (roles, problem) = KnowledgeRules.NormalizeRoles(["hr, manager", "Manager", "engineering_manager"]);

        Assert.Null(problem);
        Assert.Equal(["HR", "MANAGER", "ENGINEERING_MANAGER"], roles);
    }

    [Theory]
    [InlineData("everyone")]
    [InlineData("ALL")]
    public void Everyone_MeansNoRestriction(string word)
    {
        var (roles, problem) = KnowledgeRules.NormalizeRoles(["HR", word]);

        Assert.Null(problem);
        Assert.Empty(roles);
    }

    [Fact]
    public void NoAudienceGiven_MeansEveryone()
    {
        Assert.Empty(KnowledgeRules.NormalizeRoles(null).Roles);
        Assert.Empty(KnowledgeRules.NormalizeRoles([]).Roles);
    }

    [Theory]
    [InlineData("x; DROP TABLE documents")]
    [InlineData("HR!")]
    [InlineData("1ADMIN")]
    public void ASillyRoleName_IsRefused(string role) => Assert.NotNull(KnowledgeRules.NormalizeRoles([role]).Problem);

    [Fact]
    public void TooManyRoles_AreRefused() =>
        Assert.NotNull(KnowledgeRules.NormalizeRoles(Enumerable.Range(1, 11).Select(i => $"ROLE_{i}")).Problem);

    [Theory]
    [InlineData("Leave Policy", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void ATitleIsNeeded(string? title, bool ok) => Assert.Equal(ok, KnowledgeRules.TitleProblem(title) is null);

    [Fact]
    public void ATooLongOrControlCharacterTitle_IsRefused()
    {
        Assert.NotNull(KnowledgeRules.TitleProblem(new string('a', KnowledgeRules.MaxTitleChars + 1)));
        Assert.NotNull(KnowledgeRules.TitleProblem("Policy\u0007"));
    }

    [Theory]
    [InlineData("policy.pdf", true)]
    [InlineData("Policy.DOCX", true)]
    [InlineData("notes.md", true)]
    [InlineData("notes.txt", true)]
    [InlineData("sheet.xlsx", false)]
    [InlineData("page.html", false)]
    [InlineData("run.exe", false)]
    [InlineData("noextension", false)]
    public void OnlyDocumentTypes_CanBeUploaded(string name, bool ok) => Assert.Equal(ok, KnowledgeRules.ExtensionAllowed(name));
}

public class SearchCompanyDocsToolTests
{
    private static Dictionary<string, object?> Args(string query, string roles = "DEVELOPER", long tenant = 1) =>
        IssueVisibility.ArgumentsFor(new Dictionary<string, object?> { ["query"] = query }, roles.Split(','), 7, tenant);

    private static KnowledgePassage Passage(string title = "Leave Policy", string? section = "Casual leave", string text = "Employees get 12 casual leaves a year.") =>
        new(1, title, section, text, 0.4, 2, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public void ItIsALookup_NotAnAction() =>
        Assert.Equal(ToolActivityKind.Read, ToolActivity.Classify("search_company_docs"));

    [Fact]
    public void AnyoneSignedIn_MayUseIt() => Assert.Equal("", new SearchCompanyDocsTool(new FakeKnowledgeStore()).RequiredPermission);

    [Fact]
    public async Task PassagesComeBack_WithTheirDocumentSectionAndDate()
    {
        var store = new FakeKnowledgeStore { Passages = { Passage() } };

        var result = await new SearchCompanyDocsTool(store).ExecuteAsync(Args("casual leave"));

        Assert.True(result.Success);
        using var json = JsonDocument.Parse((string)result.Data!);
        var first = json.RootElement.GetProperty("passages")[0];
        Assert.Equal(1, json.RootElement.GetProperty("found").GetInt32());
        Assert.Equal("Leave Policy", first.GetProperty("document").GetString());
        Assert.Equal("Casual leave", first.GetProperty("section").GetString());
        Assert.Equal("2026-10-01", first.GetProperty("updated").GetString());
        Assert.Contains("12 casual leaves", first.GetProperty("text").GetString());
    }

    [Fact]
    public async Task NothingFound_SaysSo_AndTellsTheModelNotToGuess()
    {
        var result = await new SearchCompanyDocsTool(new FakeKnowledgeStore()).ExecuteAsync(Args("moon base"));

        Assert.True(result.Success);
        Assert.Contains("\"found\":0", (string)result.Data!);
        Assert.Contains("do not guess", (string)result.Data!);
    }

    [Fact]
    public async Task TheSearch_IsForTheVerifiedTenantAndRoles_NotWhatTheModelSays()
    {
        var store = new FakeKnowledgeStore();
        var given = new Dictionary<string, object?>
        {
            ["query"] = "salary bands",
            [IssueVisibility.CallerRolesArgument] = "ADMIN",
            [IssueVisibility.CallerTenantIdArgument] = 99L
        };
        var args = IssueVisibility.ArgumentsFor(given, ["DEVELOPER"], 7, 1);

        await new SearchCompanyDocsTool(store).ExecuteAsync(args);

        var search = store.LastSearch!.Value;
        Assert.Equal(1, search.Tenant);
        Assert.Equal(new[] { "DEVELOPER" }, search.Roles.ToArray());
        Assert.False(search.SeesAll);
    }

    [Theory]
    [InlineData("ADMIN")]
    [InlineData("HUMAN_RESOURCE")]
    [InlineData("MANAGER,HR")]
    public async Task AdminAndHr_SearchEverything(string roles)
    {
        var store = new FakeKnowledgeStore();

        await new SearchCompanyDocsTool(store).ExecuteAsync(Args("policy", roles));

        Assert.True(store.LastSearch!.Value.SeesAll);
    }

    [Theory]
    [InlineData("MANAGER")]
    [InlineData("DEVELOPER,TALENT_ACQUISITION")]
    public async Task EveryoneElse_SearchesOnlyWhatTheirRolesMayRead(string roles)
    {
        var store = new FakeKnowledgeStore();

        await new SearchCompanyDocsTool(store).ExecuteAsync(Args("policy", roles));

        Assert.False(store.LastSearch!.Value.SeesAll);
        Assert.Equal(roles.Split(','), store.LastSearch!.Value.Roles.ToArray());
    }

    [Fact]
    public async Task WithoutAKnownTenant_NothingIsSearched()
    {
        var store = new FakeKnowledgeStore { Passages = { Passage() } };

        var result = await new SearchCompanyDocsTool(store).ExecuteAsync(Args("policy", tenant: 0));

        Assert.False(result.Success);
        Assert.Equal(0, store.Searches);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnEmptyQuestion_IsRefused_WithoutSearching(string query)
    {
        var store = new FakeKnowledgeStore();

        var result = await new SearchCompanyDocsTool(store).ExecuteAsync(Args(query));

        Assert.False(result.Success);
        Assert.Equal(0, store.Searches);
    }

    [Fact]
    public async Task AStoreThatIsDown_GivesAPlainMessage()
    {
        var result = await new SearchCompanyDocsTool(new FakeKnowledgeStore { Down = true }).ExecuteAsync(Args("policy"));

        Assert.False(result.Success);
        Assert.Contains("can't be reached", result.Error);
    }

    [Fact]
    public async Task TheNumberOfPassages_IsCapped()
    {
        var store = new FakeKnowledgeStore();

        await new SearchCompanyDocsTool(store).ExecuteAsync(Args("policy"));

        Assert.Equal(KnowledgeRules.MaxPassages, store.LastSearch!.Value.Limit);
    }
}

public class KnowledgeControllerTests
{
    private sealed class Identity(VerifiedCaller? caller, bool credentials = true) : ICallerIdentityProvider
    {
        public bool HasCredentials => credentials;
        public Task<VerifiedCaller?> VerifyAsync(CancellationToken cancellationToken = default) => Task.FromResult(caller);
    }

    private static readonly VerifiedCaller Hr = new(5, "HUMAN_RESOURCE", ["HUMAN_RESOURCE"], [], 3);
    private static readonly VerifiedCaller Admin = new(1, "ADMIN", ["ADMIN"], [], 3);
    private static readonly VerifiedCaller Dev = new(7, "DEVELOPER", ["DEVELOPER"], [], 3);

    private static KnowledgeController Make(FakeKnowledgeStore store, VerifiedCaller? caller, bool credentials = true) =>
        new(store, new Identity(caller, credentials), NullLogger<KnowledgeController>.Instance);

    private static IFormFile File(string name, string content) => File(name, Encoding.UTF8.GetBytes(content));

    private static IFormFile File(string name, byte[] bytes) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);

    private const string Policy = "# Notice period\n\nThe notice period is 60 days for all employees.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NobodyUnconfirmed_UsesAnything(bool credentials)
    {
        var store = new FakeKnowledgeStore();
        var controller = Make(store, null, credentials);

        Assert.IsType<UnauthorizedObjectResult>(await controller.List(default));
        Assert.IsType<UnauthorizedObjectResult>(await controller.Upload(File("a.txt", Policy), "A", null, default));
        Assert.IsType<UnauthorizedObjectResult>(await controller.Delete(1, default));
        Assert.Empty(store.Saved);
        Assert.Empty(store.Deleted);
    }

    [Fact]
    public async Task APersonWhoIsNotAdminOrHr_IsRefusedEverything()
    {
        var store = new FakeKnowledgeStore();
        var controller = Make(store, Dev);

        foreach (var result in new[]
        {
            await controller.List(default),
            await controller.Upload(File("a.txt", Policy), "A", null, default),
            await controller.Delete(1, default)
        })
            Assert.Equal(403, Assert.IsType<ObjectResult>(result).StatusCode);

        Assert.Empty(store.Saved);
        Assert.Empty(store.Deleted);
    }

    [Theory]
    [InlineData("HR")]
    [InlineData("ADMIN")]
    public async Task AdminAndHr_CanList(string who)
    {
        var store = new FakeKnowledgeStore();
        store.Documents.Add(new KnowledgeDocument(4, "Leave Policy", "leave.pdf", ["MANAGER"], 5, DateTimeOffset.UtcNow, 2, 9, 4000));

        var ok = Assert.IsType<OkObjectResult>(await Make(store, who == "HR" ? Hr : Admin).List(default));
        var json = JsonSerializer.Serialize(ok.Value);

        Assert.Contains("Leave Policy", json);
        Assert.Contains("\"roles\":[\"MANAGER\"]", json);
        Assert.Contains("\"everyone\":false", json);
        Assert.Contains($"\"limit\":{KnowledgeRules.MaxDocuments}", json);
    }

    [Fact]
    public async Task AnUpload_IsSavedUnderTheVerifiedTenantAndUser_WithItsAudience()
    {
        var store = new FakeKnowledgeStore();

        var created = Assert.IsType<ObjectResult>(await Make(store, Hr).Upload(File("notice.md", Policy), "Notice Policy", ["manager, hr"], default));

        Assert.Equal(201, created.StatusCode);
        var (tenant, user, document) = Assert.Single(store.Saved);
        Assert.Equal(3, tenant);
        Assert.Equal(5, user);
        Assert.Equal("Notice Policy", document.Title);
        Assert.Equal("notice.md", document.FileName);
        Assert.Equal(["MANAGER", "HR"], document.AllowedRoles);
        Assert.Contains("60 days", document.Text);
        Assert.Equal("Notice period", document.Chunks[0].Heading);
    }

    [Fact]
    public async Task NoAudienceChosen_MeansEveryone()
    {
        var store = new FakeKnowledgeStore();

        await Make(store, Admin).Upload(File("notice.txt", Policy), "Notice", null, default);

        Assert.Empty(store.Saved[0].Document.AllowedRoles);
    }

    [Fact]
    public async Task WithoutATitle_TheFileNameBecomesIt()
    {
        var store = new FakeKnowledgeStore();

        await Make(store, Admin).Upload(File("Travel_policy-2026.txt", Policy), null, null, default);

        Assert.Equal("Travel policy 2026", store.Saved[0].Document.Title);
    }

    [Fact]
    public async Task UploadingTheSameTitleAgain_IsReportedAsAReplacement()
    {
        var store = new FakeKnowledgeStore { SaveStatus = KnowledgeSaveStatus.Replaced };

        var ok = Assert.IsType<OkObjectResult>(await Make(store, Hr).Upload(File("n.txt", Policy), "Notice", null, default));

        Assert.Contains("\"status\":\"replaced\"", JsonSerializer.Serialize(ok.Value));
    }

    [Fact]
    public async Task TheLimit_IsReportedAsAConflict()
    {
        var store = new FakeKnowledgeStore { SaveStatus = KnowledgeSaveStatus.Full };

        Assert.IsType<ConflictObjectResult>(await Make(store, Hr).Upload(File("n.txt", Policy), "Notice", null, default));
    }

    [Fact]
    public async Task NoFile_IsRefused() =>
        Assert.IsType<BadRequestObjectResult>(await Make(new FakeKnowledgeStore(), Hr).Upload(null, "Notice", null, default));

    [Theory]
    [InlineData("run.exe")]
    [InlineData("page.html")]
    [InlineData("sheet.xlsx")]
    [InlineData("noextension")]
    public async Task OnlyDocumentTypes_AreAccepted(string name)
    {
        var store = new FakeKnowledgeStore();

        Assert.IsType<BadRequestObjectResult>(await Make(store, Hr).Upload(File(name, Policy), "X", null, default));
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task APdfThatIsReallyText_IsRefused()
    {
        var store = new FakeKnowledgeStore();

        Assert.IsType<BadRequestObjectResult>(await Make(store, Hr).Upload(File("fake.pdf", Policy), "Fake", null, default));
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task AnEmptyFile_IsRefused() =>
        Assert.IsType<BadRequestObjectResult>(await Make(new FakeKnowledgeStore(), Hr).Upload(File("a.txt", ""), "A", null, default));

    [Fact]
    public async Task AFileWithOnlyBlankSpace_HasNoReadableText()
    {
        var store = new FakeKnowledgeStore();

        var bad = Assert.IsType<BadRequestObjectResult>(await Make(store, Hr).Upload(File("a.txt", "   \n\n   "), "A", null, default));

        Assert.Contains("read any text", JsonSerializer.Serialize(bad.Value));
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task ABadRoleName_IsRefused_AndNothingIsSaved()
    {
        var store = new FakeKnowledgeStore();

        Assert.IsType<BadRequestObjectResult>(await Make(store, Hr).Upload(File("a.txt", Policy), "A", ["x; DROP TABLE documents"], default));
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task ATooLongTitle_IsRefused() =>
        Assert.IsType<BadRequestObjectResult>(await Make(new FakeKnowledgeStore(), Hr).Upload(File("a.txt", Policy), new string('a', 500), null, default));

    [Fact]
    public async Task AStoreThatIsDown_Is503_OnEveryCall()
    {
        var controller = Make(new FakeKnowledgeStore { Down = true }, Hr);

        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.List(default)).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.Upload(File("a.txt", Policy), "A", null, default)).StatusCode);
        Assert.Equal(503, Assert.IsType<ObjectResult>(await controller.Delete(1, default)).StatusCode);
    }

    [Fact]
    public async Task Removing_UsesTheVerifiedTenant_AndAnUnknownIdIs404()
    {
        var store = new FakeKnowledgeStore();
        store.Documents.Add(new KnowledgeDocument(4, "Leave", "l.txt", [], 5, DateTimeOffset.UtcNow, 1, 1, 10));
        var controller = Make(store, Hr);

        Assert.IsType<NoContentResult>(await controller.Delete(4, default));
        Assert.IsType<NotFoundResult>(await controller.Delete(4, default));
        Assert.All(store.Deleted, d => Assert.Equal(3, d.Tenant));
    }

    [Fact]
    public async Task AWordFile_IsReadAndSaved()
    {
        var store = new FakeKnowledgeStore();
        var docx = WordFile.Make("Dress code", "Smart casual is expected on all weekdays.");

        var created = Assert.IsType<ObjectResult>(await Make(store, Hr).Upload(File("dress.docx", docx), "Dress code", null, default));

        Assert.Equal(201, created.StatusCode);
        Assert.Contains("Smart casual", store.Saved[0].Document.Text);
    }

    [Fact]
    public async Task APdf_IsReadAndSaved()
    {
        var store = new FakeKnowledgeStore();
        var pdf = PdfFile.Make("The probation period is three months for new employees.");

        var created = Assert.IsType<ObjectResult>(await Make(store, Hr).Upload(File("probation.pdf", pdf), "Probation", null, default));

        Assert.Equal(201, created.StatusCode);
        Assert.Contains("probation period is three months", store.Saved[0].Document.Text);
    }
}

public class KnowledgeTextExtractorTests
{
    [Fact]
    public void PdfText_IsRead() =>
        Assert.Contains("Holidays are listed in the calendar", KnowledgeTextExtractor.Extract(PdfFile.Make("Holidays are listed in the calendar."), "h.pdf"));

    [Fact]
    public void ANotAPdf_GivesNothing_NotACrash() =>
        Assert.Null(KnowledgeTextExtractor.Extract(Encoding.UTF8.GetBytes("this is not a pdf"), "x.pdf"));

    [Fact]
    public void ATruncatedPdf_GivesNothing_NotACrash()
    {
        var pdf = PdfFile.Make("Some text.");

        Assert.Null(KnowledgeTextExtractor.Extract(pdf[..(pdf.Length / 3)], "x.pdf"));
    }

    [Fact]
    public void APdfWithNoText_GivesNothing() =>
        Assert.Null(KnowledgeTextExtractor.Extract(PdfFile.Make(""), "scan.pdf"));

    [Fact]
    public void PlainTextAndMarkdown_AreRead()
    {
        Assert.Equal("Hello", KnowledgeTextExtractor.Extract("Hello\n"u8.ToArray(), "a.txt"));
        Assert.Equal("# Hi", KnowledgeTextExtractor.Extract("# Hi"u8.ToArray(), "a.md"));
    }

    [Fact]
    public void AWordFile_IsRead() =>
        Assert.Contains("Smart casual", KnowledgeTextExtractor.Extract(WordFile.Make("Dress code", "Smart casual is expected."), "d.docx"));

    [Fact]
    public void ABrokenWordFile_GivesNothing_NotACrash() =>
        Assert.Null(KnowledgeTextExtractor.Extract([0x50, 0x4B, 0x03, 0x04, 1, 2, 3], "d.docx"));

    [Fact]
    public void ABlankFile_GivesNothing() => Assert.Null(KnowledgeTextExtractor.Extract("   \n  "u8.ToArray(), "a.txt"));
}


internal static class PdfFile
{
    public static byte[] Make(string text)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        if (text.Length > 0) page.AddText(text, 11, new PdfPoint(40, 780), font);
        return builder.Build();
    }
}

internal static class WordFile
{
    public static byte[] Make(params string[] paragraphs)
    {
        using var output = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>");
            foreach (var p in paragraphs) writer.Write($"<w:p><w:r><w:t>{System.Security.SecurityElement.Escape(p)}</w:t></w:r></w:p>");
            writer.Write("</w:body></w:document>");
        }
        return output.ToArray();
    }
}
