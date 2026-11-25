using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;
public sealed class AccountHierarchyEndToEndTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method,string path,object? body=null,uint? version=null,long tenant=1,long user=100,string role="ADMIN") {
        using var request=new HttpRequestMessage(new HttpMethod(method),path);
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,role));
        if(version.HasValue)request.Headers.TryAddWithoutValidation("If-Match",$"\"{version}\"");
        if(body!=null)request.Content=JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }
    private static async Task<JsonElement> Created(HttpResponseMessage response) {
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact]
    public async Task CompleteAccountProjectIssueHierarchyAndLifecycle() {
        var name="Hierarchy "+Guid.NewGuid().ToString("N");
        var account=await Created(await Send("POST","/api/projects/accounts",new {name}));
        var aid=account.GetProperty("id").GetInt64();var version=account.GetProperty("version").GetUInt32();
        using var duplicate=await Send("POST","/api/projects/accounts",new {name=name.ToUpperInvariant()});
        Assert.Equal(HttpStatusCode.Conflict,duplicate.StatusCode);
        var project=await Created(await Send("POST","/api/projects",new {accountId=aid,accountName=name,projectName="H"+Guid.NewGuid().ToString("N"),displayName="Migration"}));
        var pid=project.GetProperty("id").GetInt64();
        Assert.Equal("Migration",project.GetProperty("projectName").GetString());
        long? parent=null;
        foreach(var type in new[]{"EPIC","STORY","TASK","SUBTASK"}) {
            var issue=await Created(await Send("POST","/api/issues",new {projectId=pid,issueType=type,parentIssueId=parent,summary=type}));
            parent=issue.GetProperty("id").GetInt64();
        }
        using var invalid=await Send("POST","/api/issues",new {projectId=pid,issueType="SUBTASK",summary="No parent"});
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
        using var roots=await Send("GET",$"/api/issues/search?ProjectId={pid}&RootOnly=true");
        var result=await roots.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(result.GetProperty("items").EnumerateArray());
        using var renamed=await Send("PUT",$"/api/projects/accounts/{aid}",new {name=name+" renamed",status="ACTIVE"},version);
        Assert.Equal(HttpStatusCode.OK,renamed.StatusCode);
        var updated=await renamed.Content.ReadFromJsonAsync<JsonElement>();
        using var stale=await Send("PUT",$"/api/projects/accounts/{aid}",new {name,status="ACTIVE"},version);
        Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);
        version=updated.GetProperty("version").GetUInt32();
        using var deletion=await Send("DELETE",$"/api/projects/accounts/{aid}",version:version);
        Assert.Equal(HttpStatusCode.Conflict,deletion.StatusCode);
        using var archived=await Send("PUT",$"/api/projects/accounts/{aid}",new {name,status="ARCHIVED"},version);
        Assert.Equal(HttpStatusCode.OK,archived.StatusCode);
        using var forbiddenProject=await Send("POST","/api/projects",new {accountId=aid,accountName=name,projectName="BLOCKED"});
        Assert.Equal(HttpStatusCode.NotFound,forbiddenProject.StatusCode);
    }
    [Fact]
    public async Task AccountsEnforceRoleTenantAndVersionAndDeleteEmpty() {
        using var denied=await Send("POST","/api/projects/accounts",new {name="No"},user:101,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        var account=await Created(await Send("POST","/api/projects/accounts",new {name="Empty "+Guid.NewGuid()}));
        var id=account.GetProperty("id").GetInt64();var version=account.GetProperty("version").GetUInt32();
        using var noVersion=await Send("DELETE",$"/api/projects/accounts/{id}");
        Assert.Equal((HttpStatusCode)428,noVersion.StatusCode);
        using var foreign=await Send("DELETE",$"/api/projects/accounts/{id}",version:version,tenant:2,user:200);
        Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);
        using var removed=await Send("DELETE",$"/api/projects/accounts/{id}",version:version);
        Assert.Equal(HttpStatusCode.NoContent,removed.StatusCode);
    }

    [Theory]
    [InlineData("EPIC","EPIC")]
    [InlineData("TASK","STORY")]
    [InlineData("TASK","TASK")]
    [InlineData("STORY","SUBTASK")]
    [InlineData("BUG","EPIC")]
    public async Task InvalidTypeEdgesAreRejected(string parentType,string childType) {
        var parent=await Created(await Send("POST","/api/issues",new {projectId=1,issueType=parentType,summary="Parent"}));
        using var invalid=await Send("POST","/api/issues",new {projectId=1,issueType=childType,parentIssueId=parent.GetProperty("id").GetInt64(),summary="Invalid child"});
        Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
    }
    [Fact]
    public async Task CrossAccountSubprojectIsRejectedAndLongProjectKeysCanCreateIssues() {
        var first=await Created(await Send("POST","/api/projects",new {accountName="Account A "+Guid.NewGuid(),projectName="L"+new string('X',999)}));
        var second=await Created(await Send("POST","/api/projects/accounts",new {name="Account B "+Guid.NewGuid()}));
        using var invalid=await Send("POST","/api/projects",new {accountId=second.GetProperty("id").GetInt64(),accountName="B",projectName="C"+Guid.NewGuid().ToString("N"),parentProjectId=first.GetProperty("id").GetInt64()});
        Assert.Equal(HttpStatusCode.Conflict,invalid.StatusCode);
        var issue=await Created(await Send("POST","/api/issues",new {projectId=first.GetProperty("id").GetInt64(),summary="Long project key"}));
        Assert.InRange(issue.GetProperty("issueKey").GetString()!.Length,1,50);
    }
}
