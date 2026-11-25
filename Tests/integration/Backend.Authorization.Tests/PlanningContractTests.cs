using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;





public sealed class PlanningContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
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

    private static readonly string[] ComponentKeys = Expect("id", "tenantId", "projectId", "name", "description", "createdAt", "updatedAt", "version");
    private static readonly string[] VersionKeys = Expect("id", "tenantId", "projectId", "name", "releaseVersion", "description", "status", "releaseDate", "createdAt", "updatedAt", "version");
    private static readonly string[] SprintKeys = Expect("id", "tenantId", "projectId", "boardId", "name", "goal", "status", "startDate", "endDate", "createdAt", "updatedAt", "version");
    private static readonly string[] BoardKeys = Expect("id", "tenantId", "projectId", "name", "boardType", "filterDefinition", "createdAt", "updatedAt");

    [Fact]
    public async Task ComponentsAndVersionsKeepTheirExactShapeAndRules()
    {
        var created = await Send("POST", "/api/projects/1/components", new { name = " Shape component ", description = "d" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/projects/1/components", created.Headers.Location!.OriginalString);
        var component = await Json(created);
        Assert.Equal(ComponentKeys, Keys(component));
        Assert.Equal("Shape component", component.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", "/api/projects/1/components", new { name = "Shape component" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("POST", "/api/projects/99/components", new { name = "Nowhere" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("POST", "/api/projects/1/components", new { name = "Dev" }, user: 101, role: "DEVELOPER")).StatusCode);
        var componentId = component.GetProperty("id").GetInt64();
        var renamed = await Json(await Send("PUT", $"/api/projects/1/components/{componentId}", new { name = "Shape component 2", description = "e" }, component.GetProperty("version").GetUInt32()));
        Assert.Equal(ComponentKeys, Keys(renamed));
        Assert.Equal((HttpStatusCode)428, (await Send("PUT", $"/api/projects/1/components/{componentId}", new { name = "No version" })).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed, (await Send("PUT", $"/api/projects/1/components/{componentId}", new { name = "Stale" }, component.GetProperty("version").GetUInt32())).StatusCode);
        Assert.Equal(ComponentKeys, Keys((await Json(await Send("GET", "/api/projects/1/components"))).EnumerateArray().First()));

        var made = await Send("POST", "/api/projects/1/versions", new { name = "shape-1.0", description = "v", releaseDate = "2026-12-01" });
        Assert.Equal(HttpStatusCode.Created, made.StatusCode);
        Assert.Equal("/api/projects/1/versions", made.Headers.Location!.OriginalString);
        var release = await Json(made);
        Assert.Equal(VersionKeys, Keys(release));
        Assert.Equal("UNRELEASED", release.GetProperty("status").GetString());
        Assert.Equal("shape-1.0", release.GetProperty("releaseVersion").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", "/api/projects/1/versions", new { name = "shape-1.0" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/api/projects/1/versions", new { name = "bad-status", status = "NOPE" })).StatusCode);
        var releaseId = release.GetProperty("id").GetInt64();
        var released = await Json(await Send("PUT", $"/api/projects/1/versions/{releaseId}", new { name = "shape-1.1", status = "RELEASED" }, release.GetProperty("version").GetUInt32()));
        Assert.Equal(VersionKeys, Keys(released));
        Assert.Equal("RELEASED", released.GetProperty("status").GetString());
        Assert.Equal(VersionKeys, Keys((await Json(await Send("GET", "/api/projects/1/versions"))).EnumerateArray().First()));

        await Json(await Send("POST", "/api/issues", new { projectId = 1, summary = "Labelled", issueType = "TASK", labels = new[] { "zeta", "alpha" } }));
        var labels = await Json(await Send("GET", "/api/projects/1/labels"));
        Assert.Equal(new[] { "alpha", "zeta" }, labels.EnumerateArray().Select(x => x.GetString()!).Where(x => x is "alpha" or "zeta").ToArray());
        Assert.Equal(HttpStatusCode.NotFound, (await Send("GET", "/api/projects/1/labels", tenant: 2, user: 200)).StatusCode);
    }

    [Fact]
    public async Task BoardsSprintsAndBacklogKeepTheirExactShapeAndRules()
    {
        var kanban = await Json(await Send("POST", "/api/boards", new { projectId = 1, name = "Shape kanban" }));
        Assert.Equal(Expect("id"), Keys(kanban));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", $"/api/boards/{kanban.GetProperty("id").GetInt64()}/sprints", new { name = "Not scrum" })).StatusCode);
        var scrum = (await Json(await Send("POST", "/api/boards", new { projectId = 1, name = "Shape scrum", boardType = "SCRUM" }))).GetProperty("id").GetInt64();

        var boards = await Json(await Send("GET", "/api/boards?projectId=1"));
        Assert.Equal(BoardKeys, Keys(boards.EnumerateArray().First()));
        Assert.Equal(2, boards.GetArrayLength());
        Assert.Equal(0, (await Json(await Send("GET", "/api/boards?projectId=3", tenant: 1))).GetArrayLength());

        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", $"/api/boards/{scrum}/sprints", new { name = "Backwards", startDate = "2026-10-14", endDate = "2026-10-01" })).StatusCode);
        var created = await Send("POST", $"/api/boards/{scrum}/sprints", new { name = " Iteration A ", goal = "g", startDate = "2026-10-01", endDate = "2026-10-14" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/boards/{scrum}/sprints", created.Headers.Location!.OriginalString);
        var sprint = await Json(created);
        Assert.Equal(SprintKeys, Keys(sprint));
        Assert.Equal("PLANNED", sprint.GetProperty("status").GetString());
        Assert.Equal("Iteration A", sprint.GetProperty("name").GetString());
        Assert.Equal(SprintKeys, Keys((await Json(await Send("GET", $"/api/boards/{scrum}/sprints"))).EnumerateArray().First()));
        var sprintId = sprint.GetProperty("id").GetInt64();

        var undated = await Json(await Send("POST", $"/api/boards/{scrum}/sprints", new { name = "Undated" }));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", $"/api/sprints/{undated.GetProperty("id").GetInt64()}/start", version: undated.GetProperty("version").GetUInt32())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", $"/api/sprints/{sprintId}/complete", version: sprint.GetProperty("version").GetUInt32())).StatusCode);   

        var issue = await Json(await Send("POST", "/api/issues", new { projectId = 1, summary = "Sprint shape work", issueType = "TASK" }));
        var issueId = issue.GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.NotFound, (await Send("PUT", $"/api/boards/{scrum}/issues/{issueId}/placement", new { sprintId = 999999, position = 1 }, issue.GetProperty("version").GetUInt32())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("PUT", $"/api/boards/{scrum}/issues/{issueId}/placement", new { sprintId, position = 5 }, issue.GetProperty("version").GetUInt32())).StatusCode);

        var inSprint = await Json(await Send("GET", $"/api/boards/{scrum}/backlog?sprintId={sprintId}"));
        Assert.Equal(Expect("items", "page", "pageSize", "totalCount"), Keys(inSprint));
        Assert.Equal(Expect("id", "issueKey", "summary", "statusCode", "version", "backlogOrder"), Keys(inSprint.GetProperty("items").EnumerateArray().First()));
        Assert.Equal(50, inSprint.GetProperty("pageSize").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("GET", $"/api/boards/{scrum}/backlog?page=0")).StatusCode);

        var started = await Json(await Send("POST", $"/api/sprints/{sprintId}/start", version: sprint.GetProperty("version").GetUInt32()));
        Assert.Equal(SprintKeys, Keys(started));
        Assert.Equal("ACTIVE", started.GetProperty("status").GetString());
        
        var second = await Json(await Send("POST", $"/api/boards/{scrum}/sprints", new { name = "Iteration B", startDate = "2026-10-15", endDate = "2026-10-28" }));
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", $"/api/sprints/{second.GetProperty("id").GetInt64()}/start", version: second.GetProperty("version").GetUInt32())).StatusCode);
        var completed = await Json(await Send("POST", $"/api/sprints/{sprintId}/complete", version: started.GetProperty("version").GetUInt32()));
        Assert.Equal(SprintKeys, Keys(completed));
        Assert.Equal("COMPLETED", completed.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await Send("GET", $"/api/boards/{scrum}/sprints", tenant: 2, user: 200)).StatusCode);
    }
}
