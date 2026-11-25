using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;
public sealed class IssueWorkflowApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, long user = 100, long tenant = 1, string role = "ADMIN", string[]? permissions = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, tenant, role, permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client, request);
        return await fixture.Client.SendAsync(request);
    }
    private async Task<long> Create(long project = 1, long? parent = null, long tenant = 1, string? due = null)
    {
        using var response = await Send("POST", "/api/issues", new { projectId = project, parentIssueId = parent, summary = "Workflow integration", issueType = parent.HasValue ? "TASK" : "STORY", dueDate = due }, tenant: tenant);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }
    [Fact]
    public async Task ParentsMustBeAccessibleSameProjectAndAcyclic()
    {
        var parent = await Create(); var child = await Create(parent: parent); var otherProject = await Create(2); var otherTenant = await Create(3, tenant: 2);
        foreach (var target in new[] { child, otherProject })
        {
            using var bad = await Send("PUT", $"/api/issues/{child}", new { parentIssueId = target });
            Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        }
        using var cycle = await Send("PUT", $"/api/issues/{parent}", new { parentIssueId = child });
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        using var crossTenant = await Send("PUT", $"/api/issues/{child}", new { parentIssueId = otherTenant });
        Assert.Equal(HttpStatusCode.NotFound, crossTenant.StatusCode);
        using var inaccessible = await Send("POST", "/api/issues", new { projectId = 2, parentIssueId = otherProject, summary = "Forbidden parent" }, user: 104, role: "DEVELOPER");
        Assert.Equal(HttpStatusCode.NotFound, inaccessible.StatusCode);
        using var remove = await Send("DELETE", $"/api/issues/{otherProject}");
        using var deleted = await Send("POST", "/api/issues", new { projectId = 2, parentIssueId = otherProject, summary = "Deleted parent" });
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }
    [Fact]
    public async Task SimultaneousOppositeMovesCannotCreateACycle()
    {
        var a = await Create(); var b = await Create();
        var results = await Task.WhenAll(Send("PUT", $"/api/issues/{a}", new { parentIssueId = b }), Send("PUT", $"/api/issues/{b}", new { parentIssueId = a }));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        foreach (var result in results) result.Dispose();
    }
    [Fact]
    public async Task ExplicitClearingPreservesOmittedFieldsAndRequiresAssignmentPermission()
    {
        var parent = await Create(); var child = await Create(parent: parent, due: "2026-10-01");
        using var unchanged = await Send("PUT", $"/api/issues/{child}", new { summary = "Changed title" });
        var kept = await unchanged.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(parent, kept.GetProperty("parentIssueId").GetInt64());
        using var clear = await Send("PUT", $"/api/issues/{child}", new { clearParentIssue = true, clearDueDate = true, clearComponent = true, clearStoryPoints = true });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        var cleared = await clear.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var field in new[] { "parentIssueId", "dueDate", "componentId", "storyPoints" }) Assert.Equal(JsonValueKind.Null, cleared.GetProperty(field).ValueKind);
        using var conflict = await Send("PUT", $"/api/issues/{child}", new { clearParentIssue = true, parentIssueId = parent });
        Assert.Equal(HttpStatusCode.BadRequest, conflict.StatusCode);
        using var denied = await Send("PUT", $"/api/issues/{child}", new { clearAssignee = true }, permissions: ["ISSUE_UPDATE", "ISSUE_VIEW"]);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var membership = await Send("POST", "/api/projects/1/members", new { userId = 101 });
        Assert.Equal(HttpStatusCode.NoContent, membership.StatusCode);
        using var assigned = await Send("PUT", $"/api/issues/{child}/assignee", new { assigneeUserId = 101 });
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        using var unassign = await Send("PUT", $"/api/issues/{child}/assignee", new { clearAssignee = true });
        Assert.Equal(HttpStatusCode.OK, unassign.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await unassign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assigneeUserId").ValueKind);
        using var missing = await Send("PUT", $"/api/issues/{child}/assignee", new { });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }
    [Fact]
    public async Task SearchFiltersApplyBeforeCountsAndRemainTenantScoped()
    {
        var parent = await Create(); var child = await Create(parent: parent, due: "2026-10-01");
        using var search = await Send("GET", $"/api/issues/search?parentIssueId={parent}&dueFrom=2026-10-01&dueTo=2026-10-01&pageSize=1");
        var result = await search.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(child, result.GetProperty("items")[0].GetProperty("id").GetInt64());
        using var other = await Send("GET", $"/api/issues/search?parentIssueId={parent}", tenant: 2);
        Assert.Equal(0, (await other.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
        using var overdue = await Send("GET", $"/api/issues/search?parentIssueId={parent}&workGroup=OVERDUE&today=2026-10-02");
        Assert.Equal(1, (await overdue.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("totalCount").GetInt32());
        using var invalid = await Send("GET", "/api/issues/search?dueFrom=2026-10-02&dueTo=2026-10-01");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
}
