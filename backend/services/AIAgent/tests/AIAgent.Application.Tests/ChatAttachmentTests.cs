using System.Text;
using AIAgent.Api.Controllers;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Attachments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AIAgent.Application.Tests;

public class ChatAttachmentRulesTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F'];
    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n");
    private static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0, 0, 0];
    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private const long Max = 10 * 1024 * 1024;

    [Theory]
    [InlineData("photo.png")]
    [InlineData("PHOTO.PNG")]
    public void AcceptsAPictureWhoseContentsMatch(string name) => Assert.Null(ChatAttachmentRules.Validate(name, 100, Png, Max));

    [Fact]
    public void AcceptsTheDocumentTypesPeopleAttach()
    {
        Assert.Null(ChatAttachmentRules.Validate("a.jpg", 10, Jpeg, Max));
        Assert.Null(ChatAttachmentRules.Validate("a.pdf", 10, Pdf, Max));
        Assert.Null(ChatAttachmentRules.Validate("a.docx", 10, Zip, Max));
        Assert.Null(ChatAttachmentRules.Validate("a.xlsx", 10, Zip, Max));
        Assert.Null(ChatAttachmentRules.Validate("a.pptx", 10, Zip, Max));
        Assert.Null(ChatAttachmentRules.Validate("a.doc", 10, Ole, Max));
        Assert.Null(ChatAttachmentRules.Validate("notes.txt", 10, Encoding.UTF8.GetBytes("hello"), Max));
        Assert.Null(ChatAttachmentRules.Validate("data.csv", 10, Encoding.UTF8.GetBytes("a,b\n1,2"), Max));
    }

    [Theory]
    [InlineData("page.html")]
    [InlineData("image.svg")]
    [InlineData("run.exe")]
    [InlineData("script.js")]
    [InlineData("bundle.zip")]
    [InlineData("noextension")]
    [InlineData("archive.png.exe")]
    public void RefusesTypesABrowserCouldRunOrAPersonCouldLaunch(string name)
    {
        var problem = ChatAttachmentRules.Validate(name, 100, Png, Max);
        Assert.Contains("not allowed", problem);
    }

    [Fact]
    public void RefusesAFileWhoseContentsDoNotMatchItsName()
    {
        Assert.Contains("do not match", ChatAttachmentRules.Validate("fake.png", 10, Pdf, Max));
        Assert.Contains("do not match", ChatAttachmentRules.Validate("fake.pdf", 10, Png, Max));
        Assert.Contains("do not match", ChatAttachmentRules.Validate("fake.docx", 10, Pdf, Max));
        Assert.Contains("do not match", ChatAttachmentRules.Validate("fake.txt", 10, Png, Max));   
    }

    [Fact]
    public void RefusesEmptyAndOversizedFiles()
    {
        Assert.Contains("empty", ChatAttachmentRules.Validate("a.png", 0, Png, Max));
        Assert.Contains("too large", ChatAttachmentRules.Validate("a.png", Max + 1, Png, Max));
        Assert.Null(ChatAttachmentRules.Validate("a.png", Max, Png, Max));
    }

    [Theory]
    [InlineData("../../etc/passwd.txt", "passwd.txt")]
    [InlineData("C:\\Users\\me\\report.pdf", "report.pdf")]
    [InlineData("a\"b<c>.png", "abc.png")]
    [InlineData("  .hidden.txt  ", "hidden.txt")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    public void NamesAreStrippedOfFoldersAndTrouble(string? raw, string expected) => Assert.Equal(expected, ChatAttachmentRules.SafeName(raw));

    [Fact]
    public void LongNamesAreShortenedButKeepTheirExtension()
    {
        var name = ChatAttachmentRules.SafeName(new string('a', 300) + ".pdf");
        Assert.Equal(ChatAttachmentRules.MaxNameChars, name.Length);
        Assert.EndsWith(".pdf", name);
    }

    [Fact]
    public void IdsAreThirtyTwoHexCharacters()
    {
        Assert.True(ChatAttachmentRules.IsValidId(ChatAttachmentRules.NewId()));
        Assert.False(ChatAttachmentRules.IsValidId("../1/2/3"));
        Assert.False(ChatAttachmentRules.IsValidId("ABCDEF0123456789ABCDEF0123456789"));
        Assert.False(ChatAttachmentRules.IsValidId(null));
    }
}

public class ChatAttachmentsControllerTests
{
    private sealed class MemoryStore : IChatAttachmentStore
    {
        public readonly Dictionary<(long, long, string), (ChatAttachment Info, byte[] Bytes)> Files = new();
        public bool Down;

        public Task<ChatAttachment> SaveAsync(long t, long u, string name, string type, Stream content, long size, CancellationToken c = default)
        {
            if (Down) throw new ChatAttachmentUnavailableException("down");
            using var copy = new MemoryStream();
            content.CopyTo(copy);
            var info = new ChatAttachment(ChatAttachmentRules.NewId(), name, type, size);
            Files[(t, u, info.Id)] = (info, copy.ToArray());
            return Task.FromResult(info);
        }

        public Task<(ChatAttachment Info, Stream Content)?> OpenAsync(long t, long u, string id, CancellationToken c = default)
        {
            if (Down) throw new ChatAttachmentUnavailableException("down");
            (ChatAttachment, Stream)? found = Files.TryGetValue((t, u, id), out var f) ? (f.Info, new MemoryStream(f.Bytes)) : null;
            return Task.FromResult(found);
        }

        public Task<bool> DeleteAsync(long t, long u, string id, CancellationToken c = default) => Task.FromResult(Files.Remove((t, u, id)));
    }

    private sealed class Identity(VerifiedCaller? caller, bool credentials = true) : ICallerIdentityProvider
    {
        public bool HasCredentials => credentials;
        public Task<VerifiedCaller?> VerifyAsync(CancellationToken ct = default) => Task.FromResult(caller);
    }

    private static VerifiedCaller Caller(long user, long tenant = 1) => new(user, "DEVELOPER", ["DEVELOPER"], [], tenant);

    private static ChatAttachmentsController Controller(MemoryStore store, VerifiedCaller? caller, bool credentials = true, string? maxBytes = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(maxBytes is null ? [] : [new("Attachments:MaxBytes", maxBytes)]).Build();
        return new ChatAttachmentsController(store, new Identity(caller, credentials), config, NullLogger<ChatAttachmentsController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static IFormFile Upload(string name, byte[] bytes) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name);

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    [Fact]
    public async Task StoresAFileUnderTheCallerOnly_AndReturnsItsDetails()
    {
        var store = new MemoryStore();

        var result = Assert.IsType<ObjectResult>(await Controller(store, Caller(7, tenant: 3)).Upload(Upload("../../shot.png", Png), default));

        Assert.Equal(201, result.StatusCode);
        var saved = Assert.Single(store.Files);
        Assert.Equal((3L, 7L), (saved.Key.Item1, saved.Key.Item2));
        Assert.Equal("shot.png", saved.Value.Info.Name);
        Assert.Equal("image/png", saved.Value.Info.ContentType);
        Assert.Equal(Png, saved.Value.Bytes);
    }

    [Fact]
    public async Task TheStoredTypeComesFromTheName_NotFromWhatTheBrowserClaims()
    {
        var store = new MemoryStore();
        var file = new FormFile(new MemoryStream(Png), 0, Png.Length, "file", "shot.png") { Headers = new HeaderDictionary(), ContentType = "text/html" };

        await Controller(store, Caller(7)).Upload(file, default);

        Assert.Equal("image/png", Assert.Single(store.Files).Value.Info.ContentType);
    }

    [Fact]
    public async Task OneUserCannotReadOrRemoveAnotherUsersFile_EvenInTheSameTenant()
    {
        var store = new MemoryStore();
        await Controller(store, Caller(7)).Upload(Upload("a.png", Png), default);
        var id = store.Files.Single().Key.Item3;

        var other = Controller(store, Caller(8));
        Assert.IsType<NotFoundObjectResult>(await other.Download(id, default));
        Assert.IsType<NotFoundResult>(await other.Remove(id, default));
        Assert.Single(store.Files);

        Assert.IsType<FileStreamResult>(await Controller(store, Caller(7)).Download(id, default));
    }

    [Fact]
    public async Task ATenantsFilesAreNotVisibleInAnotherTenant_EvenWithTheSameUserId()
    {
        var store = new MemoryStore();
        await Controller(store, Caller(7, tenant: 1)).Upload(Upload("a.png", Png), default);

        Assert.IsType<NotFoundObjectResult>(await Controller(store, Caller(7, tenant: 2)).Download(store.Files.Single().Key.Item3, default));
    }

    [Fact]
    public async Task PicturesAreShownInline_OtherFilesAreDownloads_AndNothingIsSniffedOrRun()
    {
        var store = new MemoryStore();
        var c = Controller(store, Caller(7));
        await c.Upload(Upload("a.png", Png), default);
        await c.Upload(Upload("notes.txt", Encoding.UTF8.GetBytes("hello")), default);

        foreach (var (key, expected) in store.Files.Select(f => (f.Key.Item3, f.Value.Info.ContentType == "image/png" ? "inline" : "attachment")))
        {
            var reader = Controller(store, Caller(7));
            Assert.IsType<FileStreamResult>(await reader.Download(key, default));
            Assert.StartsWith(expected, reader.Response.Headers["Content-Disposition"].ToString());
            Assert.Equal("nosniff", reader.Response.Headers["X-Content-Type-Options"].ToString());
            Assert.Contains("sandbox", reader.Response.Headers["Content-Security-Policy"].ToString());
        }
    }

    [Fact]
    public async Task RefusesWhatIsNotAllowed_WithAReasonTheScreenCanShow()
    {
        var store = new MemoryStore();
        var c = Controller(store, Caller(7));

        var html = Assert.IsType<BadRequestObjectResult>(await c.Upload(Upload("x.html", Encoding.UTF8.GetBytes("<script>")), default));
        Assert.Contains("not allowed", html.Value!.ToString());
        Assert.IsType<BadRequestObjectResult>(await c.Upload(Upload("fake.png", Encoding.UTF8.GetBytes("not a picture")), default));
        Assert.IsType<BadRequestObjectResult>(await c.Upload(Upload("empty.png", []), default));
        Assert.IsType<BadRequestObjectResult>(await c.Upload(null, default));
        Assert.IsType<BadRequestObjectResult>(await Controller(store, Caller(7), maxBytes: "8").Upload(Upload("big.png", Png), default));
        Assert.Empty(store.Files);
    }

    [Fact]
    public async Task WithoutAVerifiedCaller_NothingIsStoredOrRead()
    {
        var store = new MemoryStore();

        Assert.IsType<UnauthorizedObjectResult>(await Controller(store, null, credentials: false).Upload(Upload("a.png", Png), default));
        Assert.IsType<UnauthorizedObjectResult>(await Controller(store, null).Download(ChatAttachmentRules.NewId(), default));
        Assert.IsType<UnauthorizedObjectResult>(await Controller(store, new VerifiedCaller(7, "DEVELOPER", ["DEVELOPER"], [], 0)).Upload(Upload("a.png", Png), default));
        Assert.Empty(store.Files);
    }

    [Fact]
    public async Task WhenTheFileStoreIsDown_ItSaysSoWithA503()
    {
        var store = new MemoryStore { Down = true };

        var result = Assert.IsType<ObjectResult>(await Controller(store, Caller(7)).Upload(Upload("a.png", Png), default));

        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public async Task AUserCanRemoveTheirOwnFile()
    {
        var store = new MemoryStore();
        await Controller(store, Caller(7)).Upload(Upload("a.png", Png), default);

        Assert.IsType<NoContentResult>(await Controller(store, Caller(7)).Remove(store.Files.Single().Key.Item3, default));
        Assert.Empty(store.Files);
    }
}





public class MinioChatAttachmentStoreIntegrationTests
{
    [Fact]
    public async Task StoresReadsBackIsolatesAndRemoves()
    {
        var setting = Environment.GetEnvironmentVariable("AIWORKHUB_TEST_MINIO");
        if (string.IsNullOrWhiteSpace(setting)) return;
        var parts = setting.Split(';');
        var store = new MinioChatAttachmentStore(new ChatAttachmentOptions(parts[0], parts[1], parts[2], parts[3]), NullLogger<MinioChatAttachmentStore>.Instance);
        var tenant = 900_000_000_000L + Random.Shared.NextInt64(1, 1_000_000);
        var bytes = Encoding.UTF8.GetBytes("quarterly numbers, 100% confidential");
        string? id = null;
        try
        {
            var saved = await store.SaveAsync(tenant, 1, "Q3 report (final).txt", "text/plain", new MemoryStream(bytes), bytes.Length);
            id = saved.Id;

            var back = await store.OpenAsync(tenant, 1, id);
            Assert.NotNull(back);            Assert.Equal("Q3 report (final).txt", back!.Value.Info.Name);
            Assert.Equal("text/plain", back.Value.Info.ContentType);
            Assert.Equal(bytes.Length, back.Value.Info.Size);
            using var copy = new MemoryStream();
            await back.Value.Content.CopyToAsync(copy);
            Assert.Equal(bytes, copy.ToArray());

            Assert.Null(await store.OpenAsync(tenant, 2, id));            
            Assert.Null(await store.OpenAsync(tenant + 1, 1, id));        
            Assert.Null(await store.OpenAsync(tenant, 1, "../../x"));     
            Assert.False(await store.DeleteAsync(tenant, 2, id));

            Assert.True(await store.DeleteAsync(tenant, 1, id));
            Assert.Null(await store.OpenAsync(tenant, 1, id));
            id = null;
        }
        finally
        {
            if (id is not null) await store.DeleteAsync(tenant, 1, id);
        }
    }
}
