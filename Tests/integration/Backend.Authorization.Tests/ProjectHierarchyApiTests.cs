using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Backend.Authorization.Tests;

public sealed class ProjectHierarchyApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method,string path,object? body = null,long user = 100,string role = "ADMIN",string[]? permissions = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method),path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",fixture.Token(user,1,role,permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client, request);
        return await fixture.Client.SendAsync(request);
    }
    private async Task<long> Create(string name,long? parent = null)
    {
        using var response = await Send("POST", parent is null ? "/api/projects" : $"/api/projects/{parent}/subprojects",
            new {key="P"+Guid.NewGuid().ToString("N")[..12],name,tenantId=999});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1,project.GetProperty("tenantId").GetInt32());
        Assert.Equal(parent,project.GetProperty("parentProjectId").ValueKind == JsonValueKind.Null ? null : project.GetProperty("parentProjectId").GetInt64());
        return project.GetProperty("id").GetInt64();
    }

    [Theory]
    [InlineData(13)]
    [InlineData(1000)]
    public async Task ProjectKeysAllowSpacesUpTo1000Characters(int length)
    {
        var key = "Service First" + new string('X', length - 13);
        using var response = await Send("POST", "/api/projects", new { key, name = "Key validation" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(key.ToUpperInvariant(), created.GetProperty("key").GetString());
    }

    [Fact]
    public async Task ProjectKeysRejectMoreThan1000Characters()
    {
        using var response = await Send("POST", "/api/projects", new { key = new string('A', 1001), name = "Too long" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatesParentAndNamedSubprojectsWithoutMixingTheirIssues()
    {
        var root = await Create("Green Dot");
        var migration = await Create("rapid! Azure Migration",root);
        var maintenance = await Create("Acquired Brands Applications Maintenance",root);
        using var list = await Send("GET",$"/api/projects/{root}/subprojects");
        var children = (await list.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
        Assert.Equal(new[] {migration,maintenance}.Order(),children.Select(c=>c.GetProperty("id").GetInt64()).Order());
        using var issue = await Send("POST","/api/issues",new {projectId=migration,summary="Migrate application"});
        Assert.Equal(HttpStatusCode.Created,issue.StatusCode);
        using var siblingIssues = await Send("GET",$"/api/issues/project/{maintenance}");
        Assert.Empty((await siblingIssues.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
        using var parentIssues = await Send("GET",$"/api/issues/project/{root}");
        Assert.Empty((await parentIssues.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray());
    }

    [Fact]
    public async Task MembershipFlowsDownMultipleLevelsButDoesNotBroadenDeveloperIssueAccess()
    {
        var root = await Create("Parent");
        var child = await Create("Child",root);
        var grandchild = await Create("Grandchild",child);
        var sibling = await Create("Sibling",root);
        using var add = await Send("POST",$"/api/projects/{root}/members",new {userId=101});
        Assert.Equal(HttpStatusCode.NoContent,add.StatusCode);
        foreach (var id in new[] {root,child,grandchild,sibling})
        {
            using var visible = await Send("GET",$"/api/projects/{id}",user:101,role:"DEVELOPER");
            Assert.Equal(HttpStatusCode.OK,visible.StatusCode);
        }
        using var targetMember = await Send("POST",$"/api/projects/{root}/members",new {userId=102});
        Assert.Equal(HttpStatusCode.NoContent,targetMember.StatusCode);
        using var created = await Send("POST","/api/issues",new {projectId=grandchild,summary="Other user's work",assigneeUserId=102});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var issueId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        using var hidden = await Send("GET",$"/api/issues/{issueId}",user:101,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.NotFound,hidden.StatusCode);
        using var remove = await Send("DELETE",$"/api/projects/{root}/members/101");
        Assert.Equal(HttpStatusCode.NoContent,remove.StatusCode);
        using var revoked = await Send("GET",$"/api/projects/{grandchild}",user:101,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.NotFound,revoked.StatusCode);
        using var direct = await Send("POST",$"/api/projects/{child}/members",new {userId=101});
        Assert.Equal(HttpStatusCode.NoContent,direct.StatusCode);
        using var descendant = await Send("GET",$"/api/projects/{grandchild}",user:101,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.OK,descendant.StatusCode);
        foreach (var id in new[] {root,sibling,3L})
        {
            using var denied = await Send("GET",$"/api/projects/{id}",user:101,role:"DEVELOPER");
            Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        }
    }

    [Theory]
    [InlineData(3)]
    [InlineData(99999)]
    public async Task CannotUseAnotherTenantOrMissingParent(long parent)
    {
        using var response = await Send("POST",$"/api/projects/{parent}/subprojects",new {key="BAD",name="Bad"});
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
        using var list = await Send("GET",$"/api/projects/{parent}/subprojects");
        Assert.Equal(HttpStatusCode.NotFound,list.StatusCode);
    }

    [Fact]
    public async Task DuplicateKeysAndInvalidHierarchyRequestsAreControlled()
    {
        using var duplicate = await Send("POST","/api/projects",new {key="A",name="Duplicate"});
        Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        using var mismatch = await Send("POST","/api/projects/1/subprojects",new {key="NEW",name="Mismatch",parentProjectId=2});
        Assert.Equal(HttpStatusCode.BadRequest,mismatch.StatusCode);
        using var invalid = await Send("POST","/api/projects",new {key="INVALID",name="Bad",parentProjectId=-1});
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        using var role = await Send("POST","/api/projects",new {key="NEW",name="Denied"},101,"DEVELOPER");
        Assert.Equal(HttpStatusCode.Forbidden,role.StatusCode);
        using var permission = await Send("POST","/api/projects/1/subprojects",new {key="NEW",name="Denied"},permissions:[]);
        Assert.Equal(HttpStatusCode.Forbidden,permission.StatusCode);
    }

    [Fact]
    public async Task MigrationPreservesExistingProjectsAndEnforcesSameTenantParents()
    {
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync();
        async Task Execute(string sql) { await using var command = new NpgsqlCommand(sql,connection); await command.ExecuteNonQueryAsync(); }
        await Execute("CREATE SCHEMA hierarchy_upgrade; SET search_path TO hierarchy_upgrade; CREATE TABLE projects (id BIGINT PRIMARY KEY, tenant_id BIGINT NOT NULL); INSERT INTO projects VALUES (1,1),(2,2),(3,1)");
        var migration = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"011_project_hierarchy.sql"));
        await Execute(migration);
        await Execute(migration);
        await using var count = new NpgsqlCommand("SELECT COUNT(*) FROM projects WHERE parent_project_id IS NULL",connection);
        Assert.Equal(3L,await count.ExecuteScalarAsync());
        await Execute("UPDATE projects SET parent_project_id=1 WHERE id=3");
        var cross = await Assert.ThrowsAsync<PostgresException>(()=>Execute("UPDATE projects SET parent_project_id=2 WHERE id=3"));
        Assert.Equal("23503",cross.SqlState);
        var self = await Assert.ThrowsAsync<PostgresException>(()=>Execute("UPDATE projects SET parent_project_id=1 WHERE id=1"));
        Assert.Equal("23514",self.SqlState);
    }
}
