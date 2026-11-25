using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Collaboration;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;





public sealed class AttachmentActivityContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, long tenant = 1, long user = 100, string role = "ADMIN", HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, tenant, role, SecurityFixture.Permissions));
        request.Content = content ?? (body is null ? null : JsonContent.Create(body));
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] Keys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
    private static string[] Expect(params string[] keys) => keys.Order(StringComparer.Ordinal).ToArray();
    private static MultipartFormDataContent File(byte[] bytes, string name) { var form = new MultipartFormDataContent(); form.Add(new ByteArrayContent(bytes), "file", name); return form; }

    [Fact]
    public async Task AttachmentsKeepTheirExactShapeHeadersAndErrorBodies()
    {
        var issue = (await Json(await Send("POST", "/api/issues", new { projectId = 1, summary = "Attachment shape" }))).GetProperty("id").GetInt64();
        var path = $"/api/issues/{issue}/attachments";

        var empty = await Send("POST", path, content: File([], "empty.txt"));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("Choose a file between 1 byte and 10 MB.", (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await Send("POST", "/api/issues/999999/attachments", content: File([1], "x.txt"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("GET", "/api/issues/999999/attachments")).StatusCode);

        var upload = await Send("POST", path, content: File([1, 2, 3], "..\\folder\\report.txt"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var item = await Json(upload);
        Assert.Equal(Expect("id", "tenantId", "issueId", "authorUserId", "name", "size", "createdAt"), Keys(item));
        Assert.Equal("report.txt", item.GetProperty("name").GetString());
        Assert.Equal(3, item.GetProperty("size").GetInt64());
        Assert.Equal(100, item.GetProperty("authorUserId").GetInt64());
        var id = item.GetProperty("id").GetGuid();

        var list = await Json(await Send("GET", path));
        Assert.Equal(Expect("id", "tenantId", "issueId", "authorUserId", "name", "size", "createdAt"), Keys(list.EnumerateArray().Single()));

        var download = await Send("GET", $"{path}/{id}/download");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(new byte[] { 1, 2, 3 }, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("application/octet-stream", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal("report.txt", download.Content.Headers.ContentDisposition!.FileName);
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-store", download.Headers.CacheControl!.ToString());

        Assert.Equal(HttpStatusCode.NotFound, (await Send("GET", $"{path}/{Guid.NewGuid()}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", $"{path}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"{path}/{id}")).StatusCode);
        Assert.Empty((await Json(await Send("GET", path))).EnumerateArray());
    }

    [Fact]
    public async Task ActivityHistoryAndFeedKeepTheirExactShapeAndPaging()
    {
        var issue = (await Json(await Send("POST", "/api/issues", new { projectId = 1, summary = "Activity shape" }))).GetProperty("id").GetInt64();
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
            db.ActivityEvents.Add(new ActivityEvent { EventId = Guid.NewGuid(), TenantId = 1, EntityType = "ISSUE", EntityId = issue, ProjectId = 1, ActorUserId = 100, Action = "ISSUE_SHAPE_TEST", OldValues = "{\"a\":1}", NewValues = null, OccurredAt = DateTimeOffset.UtcNow.AddMinutes(1) });
            await db.SaveChangesAsync();
        }
        var itemKeys = Expect("eventId", "tenantId", "entityType", "entityId", "projectId", "actorUserId", "action", "oldValues", "newValues", "occurredAt");
        var envelope = Expect("items", "page", "pageSize", "totalCount");

        var history = await Json(await Send("GET", $"/api/issues/{issue}/activity"));
        Assert.Equal(envelope, Keys(history));
        Assert.Equal(50, history.GetProperty("pageSize").GetInt32());
        Assert.True(history.GetProperty("totalCount").GetInt32() >= 1);
        var first = history.GetProperty("items").EnumerateArray().First();
        Assert.Equal(itemKeys, Keys(first));
        Assert.Equal("ISSUE_SHAPE_TEST", first.GetProperty("action").GetString());   
        Assert.Equal(JsonValueKind.Null, first.GetProperty("newValues").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("GET", $"/api/issues/{issue}/activity?page=0")).StatusCode);

        var feed = await Json(await Send("GET", "/api/activity"));
        Assert.Equal(envelope, Keys(feed));
        Assert.Equal(itemKeys, Keys(feed.GetProperty("items").EnumerateArray().First()));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("GET", "/api/activity?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("GET", "/api/activity?page=100001")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("GET", "/api/activity", user: 101, role: "DEVELOPER")).StatusCode);
    }
}
