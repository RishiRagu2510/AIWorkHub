using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityTenant.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Authorization.Tests;

public sealed class EffectiveMembershipApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null,
        long user = 100, string role = "ADMIN", string[]? permissions = null, bool anonymous = false, string? version = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, 1, role, permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        if (version is not null) request.Headers.TryAddWithoutValidation("If-Match",version);
        else await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client, request);
        return await fixture.Client.SendAsync(request);
    }
    private async Task<long> Project(long? parent = null)
    {
        using var response = await Send("POST", "/api/projects", new { key = "P" + Guid.NewGuid().ToString("N"), name = "Membership test", parentProjectId = parent });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }
    private async Task Add(long project, long user)
    {
        using var response = await Send("POST", $"/api/projects/{project}/members", new { userId = user });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
    private async Task<long> Issue(long project)
    {
        using var response = await Send("POST", "/api/issues", new { projectId = project, summary = "Assignment test" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task EffectiveMembersDeduplicateUsersAndIdentifyDirectInheritedAndLeadSources()
    {
        var root = await Project(); var child = await Project(root); var leaf = await Project(child);
        await Add(root, 101); await Add(leaf, 101); await Add(child, 102);
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE projects SET project_lead_user_id = {102L} WHERE id = {root}");
        using var response = await Send("GET", $"/api/projects/{leaf}/members/effective");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var members = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
        Assert.Equal(new long[] {101,102}, members.Select(m => m.GetProperty("userId").GetInt64()));
        var sources = members[0].GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal(2, sources.Length);
        Assert.False(sources[0].GetProperty("isInherited").GetBoolean());
        Assert.Equal(root, sources[1].GetProperty("projectId").GetInt64());
        Assert.Contains(members[1].GetProperty("sources").EnumerateArray(), s => s.GetProperty("role").GetString() == "PROJECT_LEAD");
        using var direct = await Send("GET", $"/api/projects/{leaf}/members");
        Assert.Single((await direct.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task InheritedAssignmentWorksForNonAdminAndStopsAfterMove()
    {
        var root = await Project(); var leaf = await Project(root); var other = await Project();
        await Add(root,101); await Add(root,102);
        using var create = await Send("POST", "/api/issues", new {projectId=leaf,summary="Developer creation",assigneeUserId=102},101,"DEVELOPER",["ISSUE_CREATE","ISSUE_ASSIGN"]);
        Assert.Equal(HttpStatusCode.Created,create.StatusCode);
        var id = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        using var assign = await Send("PUT",$"/api/issues/{id}/assignee",new {assigneeUserId=101},101,"DEVELOPER",["ISSUE_ASSIGN"], version:create.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.OK,assign.StatusCode);
        using var move = await Send("PUT",$"/api/projects/{leaf}/parent",new {parentProjectId=other});
        Assert.Equal(HttpStatusCode.OK,move.StatusCode);
        using var rejected = await Send("PUT",$"/api/issues/{id}/assignee",new {assigneeUserId=102});
        Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);
        using var effective = await Send("GET",$"/api/projects/{leaf}/members/effective");
        Assert.Empty((await effective.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Theory]
    [InlineData(102)] 
    [InlineData(200)] 
    [InlineData(999)] 
    [InlineData(104)] 
    [InlineData(0)]
    public async Task EveryAssignmentPathRejectsIneligibleTargetsWithoutPartialUpdates(long target)
    {
        var project = await Project(); var id = await Issue(project);
        using var create = await Send("POST","/api/issues",new {projectId=project,summary="Rejected",assigneeUserId=target});
        Assert.Equal(HttpStatusCode.BadRequest,create.StatusCode);
        using var update = await Send("PUT",$"/api/issues/{id}",new {summary="Must roll back",assigneeUserId=target});
        Assert.Equal(HttpStatusCode.BadRequest,update.StatusCode);
        using var assign = await Send("PUT",$"/api/issues/{id}/assignee",new {assigneeUserId=target});
        Assert.Equal(HttpStatusCode.BadRequest,assign.StatusCode);
        using var read = await Send("GET",$"/api/issues/{id}");
        var issue = await read.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Assignment test",issue.GetProperty("summary").GetString());
        Assert.Equal(JsonValueKind.Null,issue.GetProperty("assigneeUserId").ValueKind);
        using var list = await Send("GET",$"/api/issues/project/{project}");
        Assert.Single((await list.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task SuspendedMemberIsRejectedAndDirectMembershipRemovalTakesEffect()
    {
        var project = await Project(); await Add(project,101); var id = await Issue(project);
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
            var user = await db.Users.SingleAsync(u=>u.Id==101); user.Suspend(); await db.SaveChangesAsync();
        }
        try
        {
            using var rejected = await Send("PUT",$"/api/issues/{id}/assignee",new {assigneeUserId=101});
            Assert.Equal(HttpStatusCode.BadRequest,rejected.StatusCode);
        }
        finally
        {
            using var scope = fixture.App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
            var user = await db.Users.SingleAsync(u=>u.Id==101); user.Activate(); await db.SaveChangesAsync();
        }
        using var allowed = await Send("PUT",$"/api/issues/{id}",new {assigneeUserId=101});
        Assert.Equal(HttpStatusCode.OK,allowed.StatusCode);
        using var remove = await Send("DELETE",$"/api/projects/{project}/members/101");
        Assert.Equal(HttpStatusCode.NoContent,remove.StatusCode);
        using var denied = await Send("PUT",$"/api/issues/{id}/assignee",new {assigneeUserId=101});
        Assert.Equal(HttpStatusCode.BadRequest,denied.StatusCode);
    }

    [Fact]
    public async Task ActiveTenantAdminAndInheritedProjectLeadCanBeAssigned()
    {
        var root = await Project(); var child = await Project(root); var id = await Issue(child);
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE projects SET project_lead_user_id = {101L} WHERE id = {root}");
        foreach (var user in new long[] {100,101})
        {
            using var response = await Send("PUT",$"/api/issues/{id}/assignee",new {assigneeUserId=user});
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        }
    }

    [Fact]
    public async Task CreationWithAssigneeRequiresAssignmentPermissionButUnassignedCreationDoesNot()
    {
        var project = await Project();
        using var denied = await Send("POST","/api/issues",new {projectId=project,summary="Forbidden",assigneeUserId=100},permissions:["ISSUE_CREATE"]);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var allowed = await Send("POST","/api/issues",new {projectId=project,summary="Allowed"},permissions:["ISSUE_CREATE"]);
        Assert.Equal(HttpStatusCode.Created,allowed.StatusCode);
    }

    [Theory]
    [InlineData("/api/projects/1/members/effective")]
    [InlineData("/api/users/101/assignment-eligibility")]
    public async Task NewEndpointsRequireAuthenticationAndPermission(string path)
    {
        using var anonymous = await Send("GET",path,anonymous:true);
        Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        using var denied = await Send("GET",path,permissions:[]);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }

    [Fact]
    public async Task EffectiveMembersRemainAdminOnlyAndTenantScoped()
    {
        using var developer = await Send("GET","/api/projects/1/members/effective",user:101,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.Forbidden,developer.StatusCode);
        using var other = await Send("GET","/api/projects/3/members/effective");
        Assert.Equal(HttpStatusCode.NotFound,other.StatusCode);
        using var otherUser = await Send("GET","/api/users/200/assignment-eligibility");
        Assert.Equal(HttpStatusCode.NotFound,otherUser.StatusCode);
    }

    [Fact]
    public async Task DeveloperCanFindCandidatesAndAssignWithoutAdministrativePermissions()
    {
        var root=await Project();var child=await Project(root);
        await Add(root,101);await Add(root,102);
        string[] permissions=["ISSUE_CREATE","ISSUE_VIEW","ISSUE_ASSIGN"];
        using var candidates=await Send("GET",$"/api/projects/{child}/assignment-members",user:101,role:"DEVELOPER",permissions:permissions);
        Assert.Equal(HttpStatusCode.OK,candidates.StatusCode);
        var members=(await candidates.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
        Assert.Equal(new long[]{101,102},members.Select(m=>m.GetProperty("userId").GetInt64()));
        Assert.All(members,m=>Assert.Single(m.EnumerateObject()));
        using var create=await Send("POST","/api/issues",new{projectId=child,summary="Developer can assign"},101,"DEVELOPER",permissions);
        Assert.Equal(HttpStatusCode.Created,create.StatusCode);
        var issue=(await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        using var assign=await Send("PUT",$"/api/issues/{issue}/assignee",new{assigneeUserId=102},101,"DEVELOPER",permissions,version:create.Headers.ETag!.Tag);
        Assert.Equal(HttpStatusCode.OK,assign.StatusCode);
        Assert.Equal(102,(await assign.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("assigneeUserId").GetInt64());
        using var denied=await Send("GET",$"/api/projects/{child}/assignment-members",user:101,role:"DEVELOPER",permissions:[]);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var foreign=await Send("GET","/api/projects/3/assignment-members",user:101,role:"DEVELOPER",permissions:permissions);
        Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
        using var adminOnly=await Send("GET",$"/api/projects/{child}/members/effective",user:101,role:"DEVELOPER",permissions:permissions);
        Assert.Equal(HttpStatusCode.Forbidden,adminOnly.StatusCode);
    }
}
