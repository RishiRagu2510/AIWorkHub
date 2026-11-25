using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;
public sealed class EmployeeSetupTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    async Task<HttpResponseMessage> Send(string method, string path, object? body = null, string role = "ADMIN", long tenant = 1, long? user = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/employees/setup" + path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user ?? (tenant == 1 ? 100 : 200), tenant, role, SecurityFixture.Permissions));
        if (body != null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }
    static async Task<JsonElement> Read(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact]
    public async Task AdminCanMaintainCatalogAndRolesWhileOtherRolesCannot()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/employees/setup")).StatusCode);
        foreach (var role in new[] {"MANAGER", "DEVELOPER"})
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await Send("GET", "", role: role)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send("POST", "/grades", new {name="Forbidden",code="FORBIDDEN"}, role)).StatusCode);
        }
        var department = await Read(await Send("POST", "/departments", new {name="Setup test department",code="SETUP_DEPARTMENT"}));
        var grade = await Read(await Send("POST", "/grades", new {name="Setup test grade",code="SETUP_GRADE",displayOrder=20}));
        var departmentId = department.GetProperty("id").GetInt64();
        var gradeId = grade.GetProperty("id").GetInt64();
        var title = await Read(await Send("POST", "/titles", new {name="Test Engineer",departmentId,gradeId}));
        var titleId = title.GetProperty("id").GetInt64();
        await Read(await Send("PUT", $"/titles/{titleId}", new {name="Senior Test Engineer",departmentId,gradeId}));
        await Read(await Send("PUT", $"/departments/{departmentId}", new {name="Renamed department",code="SETUP_DEPARTMENT"}));
        await Read(await Send("PUT", $"/grades/{gradeId}", new {name="Renamed grade",code="SETUP_GRADE"}));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/titles", new {name="Duplicate",departmentId,gradeId})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/grades", new {name="Duplicate",code="SETUP_GRADE"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/titles", new {name="Invalid",departmentId=-1,gradeId})).StatusCode);
        var roleRow = await Read(await Send("POST", "/roles", new {name="Setup Role",code="SETUP_ROLE"}));
        var roleId = roleRow.GetProperty("id").GetInt64();
        await Read(await Send("PUT", $"/roles/{roleId}", new {name="Renamed role",code="SETUP_ROLE",description="Updated description"}));
        Assert.Equal(HttpStatusCode.NotFound, (await Send("PUT", $"/roles/{roleId}", new {name="Other tenant",code="SETUP_ROLE"}, tenant:2)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("PUT", $"/roles/{roleId}", new {name="Changed code",code="ADMIN"})).StatusCode);
        var rows = await Read(await Send("GET", ""));
        Assert.Contains(rows.GetProperty("titles").EnumerateArray(), x => x.GetProperty("id").GetInt64() == titleId && x.GetProperty("name").GetString() == "Senior Test Engineer");
        Assert.DoesNotContain((await Read(await Send("GET", "",tenant:2))).GetProperty("roles").EnumerateArray(), x => x.GetProperty("id").GetInt64() == roleId);
        Assert.Equal(HttpStatusCode.Forbidden,(await Send("DELETE",$"/titles/{titleId}",role:"DEVELOPER")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send("DELETE",$"/departments/{departmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await Send("DELETE",$"/titles/{titleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await Send("DELETE",$"/departments/{departmentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send("DELETE",$"/roles/{roleId}",tenant:2)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await Send("DELETE",$"/roles/{roleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send("DELETE","/roles/1")).StatusCode);

    }
    [Fact]
    public async Task HrCanManageSetupWithTenantIsolation()
    {
        var role = await Read(await Send("POST", "/roles", new {name="Human Resources",code="HR"}));
        var roleId = role.GetProperty("id").GetInt64();
        await Read(await Send("POST", "/users/102/roles", new {roleIds=new[]{roleId}}));
        await Read(await Send("GET", "", role:"HR", user:102));
        var department = await Read(await Send("POST", "/departments", new {name="HR managed department",code="HR_MANAGED"}, role:"HR", user:102));
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/departments/{department.GetProperty("id").GetInt64()}", role:"HR", user:102)).StatusCode);
        await Read(await Send("POST", "/users/101/roles", new {roleIds=new[]{roleId}}, role:"HR", user:102));
        Assert.Equal(HttpStatusCode.NotFound, (await Send("POST", "/users/200/roles", new {roleIds=new[]{roleId}}, role:"HR", user:102)).StatusCode);
    }

    [Fact]
    public async Task HumanResourceRoleCodeManagesSetupLikeHr()
    {
        
        var role = await Read(await Send("POST", "/roles", new {name="Human Resource",code="HUMAN_RESOURCE"}));
        var roleId = role.GetProperty("id").GetInt64();
        await Read(await Send("POST", "/users/102/roles", new {roleIds=new[]{roleId}}));
        await Read(await Send("GET", "", role:"HUMAN_RESOURCE", user:102));
        var department = await Read(await Send("POST", "/departments", new {name="HR code department",code="HR_CODE_DEPARTMENT"}, role:"HUMAN_RESOURCE", user:102));
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/departments/{department.GetProperty("id").GetInt64()}", role:"HUMAN_RESOURCE", user:102)).StatusCode);
        await Read(await Send("POST", "/users/101/roles", new {roleIds=new[]{roleId}}, role:"HUMAN_RESOURCE", user:102));
        Assert.Equal(HttpStatusCode.NotFound, (await Send("POST", "/users/200/roles", new {roleIds=new[]{roleId}}, role:"HUMAN_RESOURCE", user:102)).StatusCode);
        
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("GET", "", role:"TALENT_ACQUISITION", user:102)).StatusCode);
    }

    [Fact]
    public async Task UserRoleAssignmentsAreAdditiveIdempotentAndTenantScoped()
    {
        await Read(await Send("POST","/users/101/roles",new{roleIds=new long[]{2}}));
        var role = await Read(await Send("POST", "/roles", new {name="People Operations",code="PEOPLE_OPS"}));
        var roleId = role.GetProperty("id").GetInt64();
        foreach (var denied in new[]{"MANAGER","DEVELOPER"})
            Assert.Equal(HttpStatusCode.Forbidden,(await Send("POST","/users/101/roles",new{roleIds=new[]{roleId}},denied)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await Send("POST","/users/200/roles",new{roleIds=new[]{roleId}})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST","/users/101/roles",new{roleIds=new long[]{3}})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send("POST","/users/101/roles",new{roleIds=Array.Empty<long>()})).StatusCode);
        await Read(await Send("POST","/users/101/roles",new{roleIds=new[]{roleId,roleId}}));
        var assigned=await Read(await Send("POST","/users/101/roles",new{roleIds=new[]{roleId}}));
        var ids=assigned.GetProperty("roleIds").EnumerateArray().Select(x=>x.GetInt64()).ToArray();
        Assert.Contains(2L,ids);
        Assert.Single(ids,x=>x==roleId);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT","/users/100/roles",new{roleIds=new long[]{2},expectedRoleIds=new long[]{1}})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await Send("PUT","/users/101/roles",new{roleIds=new[]{roleId},expectedRoleIds=Array.Empty<long>()})).StatusCode);
        var replaced=await Read(await Send("PUT","/users/101/roles",new{roleIds=new[]{roleId},expectedRoleIds=ids}));
        Assert.Equal(new[]{roleId},replaced.GetProperty("roleIds").EnumerateArray().Select(x=>x.GetInt64()).ToArray());

        Assert.Equal(HttpStatusCode.BadRequest,(await Send("DELETE",$"/roles/{roleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Send("PUT","/users/100/roles",new{roleIds=Array.Empty<long>(),expectedRoleIds=new long[]{1}})).StatusCode);
        var cleared=await Read(await Send("PUT","/users/101/roles",new{roleIds=Array.Empty<long>(),expectedRoleIds=new[]{roleId}}));
        Assert.Empty(cleared.GetProperty("roleIds").EnumerateArray());
        var users=await Read(await Send("GET","/users?search=user101"));
        Assert.Single(users.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain((await Read(await Send("GET","/users",tenant:2))).GetProperty("items").EnumerateArray(),x=>x.GetProperty("id").GetInt64()==101);
    }

    [Fact]
    public async Task BulkDeletionStaysInCategoryAndPreservesAdminAndLinks()
    {
        var d=await Read(await Send("POST","/departments",new{name="Bulk department",code="BULK_DEPT"}));
        var g=await Read(await Send("POST","/grades",new{name="Bulk grade",code="BULK_GRADE"}));
        await Read(await Send("POST","/titles",new{name="Bulk title",departmentId=d.GetProperty("id").GetInt64(),gradeId=g.GetProperty("id").GetInt64()}));
        Assert.Equal(HttpStatusCode.Forbidden,(await Send("DELETE","/titles",role:"DEVELOPER")).StatusCode);
        var blocked=await Read(await Send("DELETE","/departments"));
        Assert.True(blocked.GetProperty("skipped").GetInt32()>0);
        await Read(await Send("DELETE","/titles"));
        var setup=await Read(await Send("GET",""));
        Assert.Contains(setup.GetProperty("departments").EnumerateArray(),x=>x.GetProperty("code").GetString()=="BULK_DEPT");
        Assert.Contains(setup.GetProperty("grades").EnumerateArray(),x=>x.GetProperty("code").GetString()=="BULK_GRADE");
        await Read(await Send("DELETE","/users"));
        var users=await Read(await Send("GET","/users?search=user100"));
        Assert.Contains(users.GetProperty("items")[0].GetProperty("roleIds").EnumerateArray(),x=>x.GetInt64()==1);
        var other=await Read(await Send("GET","/users",tenant:2));
        Assert.Contains(other.GetProperty("items")[0].GetProperty("roleIds").EnumerateArray(),x=>x.GetInt64()==3);
        Assert.Contains((await Read(await Send("GET",""))).GetProperty("roles").EnumerateArray(),x=>x.GetProperty("id").GetInt64()==2);
    }

}
