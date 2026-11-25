using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Backend.Authorization.Tests;

public sealed class ProjectMembershipApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method,string path,long user = 100,string role = "ADMIN", object? body = null,string[]? permissions = null,bool anonymous = false)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method),path);
        if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",fixture.Token(user,1,role,permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client, request);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task AdminListsOnlyActiveDevelopersInOwnTenant()
    {
        using var response = await Send("GET","/api/users?role=DEVELOPER&tenantId=2");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = page.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(new long[] {101,102},items.Select(i=>i.GetProperty("id").GetInt64()).Order());
        Assert.All(items,i=>Assert.Equal(1,i.GetProperty("tenantId").GetInt64()));
        using var other = await Send("GET","/api/users/200");
        Assert.Equal(HttpStatusCode.NotFound,other.StatusCode);
    }

    [Fact]
    public async Task AdminCanAddListAndRemoveDeveloperWithoutWideningIssueScope()
    {
        for (var i=0;i<2;i++)
        {
            using var add = await Send("POST","/api/projects/2/members",body:new {userId=101,tenantId=2,role="ADMIN"});
            Assert.Equal(HttpStatusCode.NoContent,add.StatusCode);
        }
        using var listed = await Send("GET","/api/projects/2/members");
        var members = (await listed.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToArray();
        var member = Assert.Single(members,m=>m.GetProperty("userId").GetInt64()==101);
        Assert.Equal("DEVELOPER",member.GetProperty("role").GetString());
        using var project = await Send("GET","/api/projects/2",101,"DEVELOPER");
        Assert.Equal(HttpStatusCode.OK,project.StatusCode);
        using var issue = await Send("GET","/api/issues/12",101,"DEVELOPER");
        Assert.Equal(HttpStatusCode.NotFound,issue.StatusCode);
        using var remove = await Send("DELETE","/api/projects/2/members/101");
        Assert.Equal(HttpStatusCode.NoContent,remove.StatusCode);
        using var after = await Send("GET","/api/projects/2",101,"DEVELOPER");
        Assert.Equal(HttpStatusCode.NotFound,after.StatusCode);
    }

    [Theory]
    [InlineData(3,101)]
    [InlineData(2,200)]
    [InlineData(2,100)]
    [InlineData(2,103)]
    [InlineData(2,999)]
    public async Task CannotAssignCrossTenantNonDeveloperOrMissingUser(long project,long user)
    {
        using var response = await Send("POST",$"/api/projects/{project}/members",body:new {userId=user});
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }

    [Theory]
    [InlineData("GET","/api/users")]
    [InlineData("GET","/api/users/101")]
    [InlineData("GET","/api/projects/2/members")]
    [InlineData("POST","/api/projects/2/members")]
    [InlineData("DELETE","/api/projects/2/members/104")]
    public async Task RequiresAuthenticatedAdminAndPermission(string method,string path)
    {
        using var anonymous = await Send(method,path,anonymous:true);
        Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        using var developer = await Send(method,path,101,"DEVELOPER",new {userId=101});
        Assert.Equal(HttpStatusCode.Forbidden,developer.StatusCode);
        using var manager = await Send(method,path,105,"PROJECT_MANAGER",new {userId=101});
        Assert.Equal(HttpStatusCode.Forbidden,manager.StatusCode);
        using var noPermission = await Send(method,path,permissions:[]);
        Assert.Equal(HttpStatusCode.Forbidden,noPermission.StatusCode);
    }

    [Theory]
    [InlineData("GET","/api/projects/3/members")]
    [InlineData("DELETE","/api/projects/3/members/104")]
    public async Task OtherTenantMembershipsAreInaccessible(string method,string path)
    {
        using var response = await Send(method,path);
        Assert.Equal(HttpStatusCode.NotFound,response.StatusCode);
    }
}
