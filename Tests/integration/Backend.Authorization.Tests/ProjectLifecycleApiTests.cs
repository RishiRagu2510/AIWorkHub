using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Authorization.Tests;

public sealed class ProjectLifecycleApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method,string path,object? body = null,long user = 100,string role = "ADMIN",string[]? permissions = null,bool anonymous = false)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method),path);
        if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",fixture.Token(user,1,role,permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client, request);
        return await fixture.Client.SendAsync(request);
    }
    private async Task<long> Create(long? parent = null)
    {
        using var response = await Send("POST","/api/projects",new {key="P"+Guid.NewGuid().ToString("N")[..12],name="Lifecycle project",parentProjectId=parent});
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }
    private Task<HttpResponseMessage> Move(long project,long? parent)
        => Send("PUT",$"/api/projects/{project}/parent",new {parentProjectId=parent});

    [Fact]
    public async Task ExplicitCreateAndUpdatePermissionsRemainSeparateAndTenantScoped()
    {
        var body=new {key="EXPLICIT",name="Explicit permissions"};
        using var denied=await Send("POST","/api/projects",body,permissions:["PROJECT_UPDATE"]);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var created=await Send("POST","/api/projects",body,permissions:["PROJECT_CREATE"]);
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        using var forbidden=await Send("PUT",$"/api/projects/{id}",new{name="Changed",projectType="SOFTWARE"},permissions:["PROJECT_CREATE"]);
        Assert.Equal(HttpStatusCode.Forbidden,forbidden.StatusCode);
        using var updated=await Send("PUT",$"/api/projects/{id}",new{name="Changed",projectType="SOFTWARE"},permissions:["PROJECT_UPDATE","PROJECT_VIEW"]);
        Assert.Equal(HttpStatusCode.OK,updated.StatusCode);
        Assert.Equal(1,(await updated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("tenantId").GetInt64());
    }

    [Fact]
    public async Task AdminEditsDetailsAndStatusWithoutChangingIdentityOrParent()
    {
        var root = await Create();
        var project = await Create(root);
        using var before = await Send("GET",$"/api/projects/{project}");
        var old = await before.Content.ReadFromJsonAsync<JsonElement>();
        using var update = await Send("PUT",$"/api/projects/{project}",new {name="Lifecycle project",displayName="Updated name",description="Updated description",projectType="SERVICE",status="ARCHIVED",tenantId=2,key="ATTACK",parentProjectId=3});
        Assert.Equal(HttpStatusCode.OK,update.StatusCode);
        var json = await update.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Updated name",json.GetProperty("projectName").GetString());
        Assert.Equal("SERVICE",json.GetProperty("projectType").GetString());
        Assert.Equal("ARCHIVED",json.GetProperty("status").GetString());
        Assert.Equal(old.GetProperty("key").GetString(),json.GetProperty("key").GetString());
        Assert.Equal(root,json.GetProperty("parentProjectId").GetInt64());
        Assert.Equal(1,json.GetProperty("tenantId").GetInt64());
        using var preserve = await Send("PUT",$"/api/projects/{project}",new {name="Lifecycle project",displayName="Still archived",projectType="BUSINESS"});
        Assert.Equal("ARCHIVED",(await preserve.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task MoveSubtreeUpdatesInheritedAccessAndSupportsPromotionToRoot()
    {
        var oldRoot = await Create(); var newRoot = await Create();
        var branch = await Create(oldRoot); var leaf = await Create(branch);
        using var oldMember = await Send("POST",$"/api/projects/{oldRoot}/members",new {userId=101});
        using var newMember = await Send("POST",$"/api/projects/{newRoot}/members",new {userId=102});
        Assert.Equal(HttpStatusCode.NoContent,oldMember.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,newMember.StatusCode);
        using var move = await Move(branch,newRoot);
        Assert.Equal(HttpStatusCode.OK,move.StatusCode);
        foreach (var id in new[] {branch,leaf})
        {
            using var lost = await Send("GET",$"/api/projects/{id}",user:101,role:"DEVELOPER");
            using var gained = await Send("GET",$"/api/projects/{id}",user:102,role:"DEVELOPER");
            Assert.Equal(HttpStatusCode.NotFound,lost.StatusCode);
            Assert.Equal(HttpStatusCode.OK,gained.StatusCode);
        }
        using var promote = await Move(branch,null);
        Assert.Equal(HttpStatusCode.OK,promote.StatusCode);
        Assert.Equal(JsonValueKind.Null,(await promote.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("parentProjectId").ValueKind);
        using var detached = await Send("GET",$"/api/projects/{leaf}",user:102,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.NotFound,detached.StatusCode);
    }

    [Fact]
    public async Task CyclesAndUnavailableParentsAreRejected()
    {
        var root = await Create(); var child = await Create(root); var leaf = await Create(child);
        foreach (var parent in new[] {root,child,leaf})
        {
            using var response = await Move(root,parent);
            Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        }
        using var cross = await Move(root,3);
        Assert.Equal(HttpStatusCode.NotFound,cross.StatusCode);
        using var missing = await Move(root,99999);
        Assert.Equal(HttpStatusCode.NotFound,missing.StatusCode);
        using var archive = await Send("PUT",$"/api/projects/{child}",new {name="Lifecycle project",status="ARCHIVED"});
        Assert.Equal(HttpStatusCode.OK,archive.StatusCode);
        var other = await Create();
        using var inactive = await Move(other,child);
        Assert.Equal(HttpStatusCode.Conflict,inactive.StatusCode);
        using var omitted = await Send("PUT",$"/api/projects/{other}/parent",new {});
        Assert.Equal(HttpStatusCode.BadRequest,omitted.StatusCode);
    }

    [Fact]
    public async Task ConcurrentOppositeMovesCannotIntroduceCycle()
    {
        var first = await Create(); var second = await Create();
        var results = await Task.WhenAll(Move(first,second),Move(second,first));
        try
        {
            Assert.Single(results,r=>r.StatusCode==HttpStatusCode.OK);
            Assert.Single(results,r=>r.StatusCode==HttpStatusCode.Conflict);
        }
        finally { foreach(var response in results) response.Dispose(); }
    }

    [Fact]
    public async Task DeleteEmptyProjectButProtectChildrenIssuesAndMemberships()
    {
        var root = await Create(); var child = await Create(root);
        using var parentDelete = await Send("DELETE",$"/api/projects/{root}");
        Assert.Equal(HttpStatusCode.Conflict,parentDelete.StatusCode);
        using var delete = await Send("DELETE",$"/api/projects/{child}");
        Assert.Equal(HttpStatusCode.NoContent,delete.StatusCode);
        using var gone = await Send("GET",$"/api/projects/{child}");
        Assert.Equal(HttpStatusCode.NotFound,gone.StatusCode);
        using var deleteRoot = await Send("DELETE",$"/api/projects/{root}");
        Assert.Equal(HttpStatusCode.NoContent,deleteRoot.StatusCode);
        using var issues = await Send("DELETE","/api/projects/1");
        Assert.Equal(HttpStatusCode.Conflict,issues.StatusCode);
        var project = await Create();
        using var add = await Send("POST",$"/api/projects/{project}/members",new {userId=101});
        Assert.Equal(HttpStatusCode.NoContent,add.StatusCode);
        using var members = await Send("DELETE",$"/api/projects/{project}");
        Assert.Equal(HttpStatusCode.Conflict,members.StatusCode);
    }

    [Fact]
    public async Task DeleteProtectsUnmappedTablesEvenWhenForeignKeyWouldCascade()
    {
        var project = await Create();
        using var scope = fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE lifecycle_board_reference (id bigint PRIMARY KEY, project_id bigint REFERENCES projects(id) ON DELETE CASCADE)");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO lifecycle_board_reference VALUES (1,{project})");
        using var response = await Send("DELETE",$"/api/projects/{project}");
        Assert.Equal(HttpStatusCode.Conflict,response.StatusCode);
        Assert.True(await db.Projects.AnyAsync(p=>p.Id==project));
    }

    [Theory]
    [InlineData("PUT","/api/projects/1")]
    [InlineData("PUT","/api/projects/1/parent")]
    [InlineData("DELETE","/api/projects/1")]
    public async Task RequireAdminAndOperationPermission(string method,string path)
    {
        using var anonymous = await Send(method,path,anonymous:true);
        Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        using var developer = await Send(method,path,new {name="Denied",parentProjectId=(long?)null},101,"DEVELOPER");
        Assert.Equal(HttpStatusCode.Forbidden,developer.StatusCode);
        using var denied = await Send(method,path,permissions:[]);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }

    [Theory]
    [InlineData("PUT","/api/projects/3")]
    [InlineData("PUT","/api/projects/3/parent")]
    [InlineData("DELETE","/api/projects/3")]
    public async Task AdminCannotModifyOtherTenant(string method,string path)
    {
        using var response = await Send(method,path,new {name="Denied",parentProjectId=(long?)null});
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }
}
