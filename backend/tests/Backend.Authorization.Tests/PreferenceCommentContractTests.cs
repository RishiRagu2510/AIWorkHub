using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;





public sealed class PreferenceCommentContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, uint? version = null, long tenant = 1, long user = 100, string role = "ADMIN")
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, tenant, role, SecurityFixture.Permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        if (version.HasValue) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static string[] Keys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
    private static string[] Expect(params string[] keys) => keys.Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task PreferencesKeepTheirExactShapeAndRules()
    {
        Assert.Equal(0, (await Json(await Send("GET", "/api/preferences", user: 150))).GetArrayLength());

        var saved = await Json(await Send("PUT", "/api/preferences/ui:theme", new { value = new { mode = "dark", sizes = new[] { 1, 2 } }, version = 0 }, user: 150));
        Assert.Equal(Expect("key", "value", "version"), Keys(saved));
        Assert.Equal("ui:theme", saved.GetProperty("key").GetString());
        Assert.Equal("dark", saved.GetProperty("value").GetProperty("mode").GetString());
        var version = saved.GetProperty("version").GetUInt32();

        var listed = await Json(await Send("GET", "/api/preferences", user: 150));
        var row = listed.EnumerateArray().Single();
        Assert.Equal(Expect("key", "value", "version"), Keys(row));
        Assert.Equal(version, row.GetProperty("version").GetUInt32());
        Assert.Equal(2, row.GetProperty("value").GetProperty("sizes").GetArrayLength());
        Assert.Equal(0, (await Json(await Send("GET", "/api/preferences", user: 151))).GetArrayLength());   

        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Send("PUT", "/api/preferences/ui:theme", new { value = "x", version = 0 }, user: 150)).StatusCode);   
        var updated = await Json(await Send("PUT", "/api/preferences/ui:theme", new { value = "light", version }, user: 150));
        Assert.Equal("light", updated.GetProperty("value").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("PUT", "/api/preferences/bad key!", new { value = 1, version = 0 }, user: 150)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("PUT", "/api/preferences/big", new { value = new string('x', 70000), version = 0 }, user: 150)).StatusCode);

        
        for (var i = 0; i < 99; i++) Assert.Equal(HttpStatusCode.OK, (await Send("PUT", $"/api/preferences/k{i}", new { value = i, version = 0 }, user: 150)).StatusCode);
        var tooMany = await Send("PUT", "/api/preferences/one-too-many", new { value = 1, version = 0 }, user: 150);
        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
    }

    [Fact]
    public async Task CommentEditsKeepTheirExactShapeAndRules()
    {
        await Send("POST", "/api/projects/1/members", new { userId = 101 });
        var issue = (await Json(await Send("POST", "/api/issues", new { projectId = 1, summary = "Comment edit shape", assigneeUserId = 101 }))).GetProperty("id").GetInt64();
        var comment = await Json(await Send("POST", $"/api/issues/{issue}/comments", new { body = "first" }));
        var id = comment.GetProperty("id").GetInt64();
        var path = $"/api/issues/{issue}/comments/{id}";
        var version = comment.GetProperty("version").GetUInt32();

        Assert.Equal(HttpStatusCode.BadRequest, (await Send("PUT", path, new { body = "  " }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("PUT", path, new { body = new string('x', 20001) }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("PUT", path, new { body = "hijack" }, version, user: 101, role: "DEVELOPER")).StatusCode);   
        Assert.Equal(HttpStatusCode.NotFound, (await Send("PUT", $"/api/issues/{issue}/comments/999999", new { body = "ghost" }, version)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("PUT", $"/api/issues/999999/comments/{id}", new { body = "ghost" }, version)).StatusCode);

        var edited = await Send("PUT", path, new { body = "second" }, version);
        var shape = await Json(edited);
        Assert.Equal(Expect("id", "body", "authorUserId", "createdAt", "updatedAt", "version"), Keys(shape));
        Assert.Equal("second", shape.GetProperty("body").GetString());
        Assert.Equal(id, shape.GetProperty("id").GetInt64());
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Send("PUT", path, new { body = "stale" }, version)).StatusCode);

        var current = shape.GetProperty("version").GetUInt32();
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", path, version: current)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", path, version: current)).StatusCode);
    }
}
