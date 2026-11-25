using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;






public sealed class EmployeeSetupContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
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

    static string[] Keys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
    static string[] Expect(params string[] keys) => keys.Order(StringComparer.Ordinal).ToArray();

    static readonly string[] DepartmentKeys = Expect("id", "code", "name", "isActive", "createdAt", "updatedAt");
    static readonly string[] GradeKeys = Expect("id", "code", "family", "name", "displayOrder", "isActive", "createdAt", "updatedAt");
    static readonly string[] SkillKeys = Expect("id", "code", "name", "category", "description", "isActive", "createdAt", "updatedAt");
    static readonly string[] RoleKeys = Expect("id", "code", "name", "description", "permissionIds");
    static readonly string[] PermissionKeys = Expect("id", "code", "name", "description");

    [Fact]
    public async Task SavedRowsAndTheCatalogKeepTheirExactShape()
    {
        var department = await Read(await Send("POST", "/departments", new { name = "Shape department", code = "SHAPE_DEPT" }));
        Assert.Equal(DepartmentKeys, Keys(department));
        var grade = await Read(await Send("POST", "/grades", new { name = "Shape grade", code = "SHAPE_GRADE", family = "Eng", displayOrder = 7 }));
        Assert.Equal(GradeKeys, Keys(grade));
        Assert.Equal("Eng", grade.GetProperty("family").GetString());
        Assert.Equal(7, grade.GetProperty("displayOrder").GetInt32());
        var skill = await Read(await Send("POST", "/skills", new { name = "Shape skill", code = "SHAPE_SKILL", category = "Core", description = "Does shapes", isActive = false }));
        Assert.Equal(SkillKeys, Keys(skill));
        Assert.False(skill.GetProperty("isActive").GetBoolean());
        Assert.Equal("Core", skill.GetProperty("category").GetString());

        var departmentId = department.GetProperty("id").GetInt64();
        var gradeId = grade.GetProperty("id").GetInt64();
        var title = await Read(await Send("POST", "/titles", new { name = "Shape title", departmentId, gradeId }));
        Assert.Equal(Expect("id", "name"), Keys(title));
        var role = await Read(await Send("POST", "/roles", new { name = "Shape role", code = "SHAPE_ROLE", description = "d" }));
        Assert.Equal(RoleKeys, Keys(role));
        var permission = await Read(await Send("POST", "/permissions", new { name = "Shape permission", code = "SHAPE_PERMISSION", description = "p" }));
        Assert.Equal(PermissionKeys, Keys(permission));
        Assert.Equal("SHAPE_PERMISSION", permission.GetProperty("code").GetString());

        var catalog = await Read(await Send("GET", ""));
        Assert.Equal(Expect("skills", "departments", "grades", "titles", "roles", "permissions"), Keys(catalog));
        Assert.Equal(SkillKeys, Keys(catalog.GetProperty("skills").EnumerateArray().First(x => x.GetProperty("code").GetString() == "SHAPE_SKILL")));
        Assert.Equal(DepartmentKeys, Keys(catalog.GetProperty("departments").EnumerateArray().First(x => x.GetProperty("code").GetString() == "SHAPE_DEPT")));
        Assert.Equal(GradeKeys, Keys(catalog.GetProperty("grades").EnumerateArray().First(x => x.GetProperty("code").GetString() == "SHAPE_GRADE")));
        Assert.Equal(Expect("id", "name", "departmentId", "gradeId", "displayOrder", "isActive"), Keys(catalog.GetProperty("titles").EnumerateArray().First()));
        Assert.Equal(RoleKeys, Keys(catalog.GetProperty("roles").EnumerateArray().First()));
        Assert.Equal(PermissionKeys, Keys(catalog.GetProperty("permissions").EnumerateArray().First()));

        var users = await Read(await Send("GET", "/users"));
        Assert.Equal(Expect("totalRoleAssignments", "items", "page", "pageSize", "totalCount", "totalPages"), Keys(users));
        Assert.Equal(Expect("id", "displayName", "email", "roleIds"), Keys(users.GetProperty("items").EnumerateArray().First()));
    }

    [Fact]
    public async Task SkillsAndPermissionsFollowTheirOwnRules()
    {
        var skill = await Read(await Send("POST", "/skills", new { name = "Rule skill", code = "RULE_SKILL" }));
        var skillId = skill.GetProperty("id").GetInt64();
        
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", "/skills", new { name = "Other", code = "RULE_SKILL" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", "/skills", new { name = "RULE SKILL", code = "RULE_SKILL_2" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("PUT", $"/skills/{skillId}", new { name = "Rule skill", code = "RULE_SKILL_CHANGED" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("PUT", "/skills/999999", new { name = "Missing", code = "MISSING" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/skills", new { name = "No code" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/skills", new { code = "NO_NAME" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/skills", new { name = "Negative", code = "NEGATIVE", displayOrder = -1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/nonsense", new { name = "x", code = "X" })).StatusCode);
        var updated = await Read(await Send("PUT", $"/skills/{skillId}", new { name = "Rule skill renamed", code = "RULE_SKILL", category = "Updated" }));
        Assert.Equal("Rule skill renamed", updated.GetProperty("name").GetString());
        Assert.Equal(Expect("id", "code", "name", "category", "description", "isActive", "createdAt", "updatedAt"), Keys(updated));
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/skills/{skillId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", $"/skills/{skillId}")).StatusCode);

        var permission = await Read(await Send("POST", "/permissions", new { name = "Rule permission", code = "RULE_PERMISSION" }));
        var permissionId = permission.GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.Conflict, (await Send("POST", "/permissions", new { name = "Again", code = "RULE_PERMISSION" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/permissions", new { name = "Lower", code = "rule_permission" })).StatusCode);   
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("PUT", $"/permissions/{permissionId}", new { name = "Rule permission", code = "RULE_PERMISSION_CHANGED" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/permissions/{permissionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", $"/permissions/{permissionId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("DELETE", "/nonsense/1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("DELETE", "/nonsense")).StatusCode);
    }

    [Fact]
    public async Task RolesAndPermissionsAreLinkedBothWays()
    {
        var permissionA = (await Read(await Send("POST", "/permissions", new { name = "Link A", code = "LINK_A" }))).GetProperty("id").GetInt64();
        var permissionB = (await Read(await Send("POST", "/permissions", new { name = "Link B", code = "LINK_B" }))).GetProperty("id").GetInt64();

        var role = await Read(await Send("POST", "/roles", new { name = "Link role", code = "LINK_ROLE", permissionIds = new[] { permissionA, permissionA, 987654 } }));
        var roleId = role.GetProperty("id").GetInt64();
        
        Assert.Equal(new[] { permissionA }, role.GetProperty("permissionIds").EnumerateArray().Select(x => x.GetInt64()).ToArray());

        
        var replaced = await Read(await Send("PUT", $"/roles/{roleId}", new { name = "Link role", code = "LINK_ROLE", permissionIds = new[] { permissionB } }));
        Assert.Equal(new[] { permissionB }, replaced.GetProperty("permissionIds").EnumerateArray().Select(x => x.GetInt64()).ToArray());
        var kept = await Read(await Send("PUT", $"/roles/{roleId}", new { name = "Link role renamed", code = "LINK_ROLE" }));
        Assert.Equal(new[] { permissionB }, kept.GetProperty("permissionIds").EnumerateArray().Select(x => x.GetInt64()).ToArray());

        
        await Read(await Send("PUT", $"/permissions/{permissionA}", new { name = "Link A", code = "LINK_A", roleIds = new long[] { roleId, 3 } }));
        var catalog = await Read(await Send("GET", ""));
        var linked = catalog.GetProperty("roles").EnumerateArray().First(x => x.GetProperty("id").GetInt64() == roleId);
        
        Assert.Equal(new[] { permissionA, permissionB }.Order(), linked.GetProperty("permissionIds").EnumerateArray().Select(x => x.GetInt64()).Order().ToArray());

        
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/permissions/{permissionA}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/roles/{roleId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/permissions/{permissionB}")).StatusCode);
    }

    [Fact]
    public async Task BulkRoleChangesAddReplaceAndRemoveWithoutDroppingTheLastAdministrator()
    {
        var role = (await Read(await Send("POST", "/roles", new { name = "Bulk role", code = "BULK_ROLE" }))).GetProperty("id").GetInt64();
        static long[] Ids(JsonElement users, long id) =>
            users.GetProperty("items").EnumerateArray().First(x => x.GetProperty("id").GetInt64() == id).GetProperty("roleIds").EnumerateArray().Select(x => x.GetInt64()).Order().ToArray();

        Assert.Equal(HttpStatusCode.Forbidden, (await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 101 }, roleIds = new[] { role } }, "DEVELOPER")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/users/bulk-roles", new { userIds = Array.Empty<long>(), roleIds = new[] { role } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 101 }, roleIds = new[] { role }, mode = "BOGUS" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 101 }, roleIds = Array.Empty<long>() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 101 }, roleIds = new long[] { 3 } })).StatusCode);

        var added = await Read(await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 101, 102, 101, 200 }, roleIds = new[] { role } }));
        Assert.Equal(Expect("updatedUsers", "mode"), Keys(added));
        Assert.Equal(2, added.GetProperty("updatedUsers").GetInt32());   
        Assert.Equal("ADD", added.GetProperty("mode").GetString());
        var users = await Read(await Send("GET", "/users"));
        Assert.Contains(role, Ids(users, 101));
        Assert.Contains(role, Ids(users, 102));

        var removed = await Read(await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 101 }, roleIds = new[] { role }, mode = "remove" }));
        Assert.Equal("REMOVE", removed.GetProperty("mode").GetString());
        Assert.DoesNotContain(role, Ids(await Read(await Send("GET", "/users")), 101));

        
        await Read(await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 102 }, roleIds = Array.Empty<long>(), mode = "REPLACE" }));
        Assert.Empty(Ids(await Read(await Send("GET", "/users")), 102));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 100 }, roleIds = new long[] { 1 }, mode = "REMOVE" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/users/bulk-roles", new { userIds = new long[] { 100 }, roleIds = Array.Empty<long>(), mode = "REPLACE" })).StatusCode);
        Assert.Contains(1L, Ids(await Read(await Send("GET", "/users")), 100));
    }

    [Fact]
    public async Task UserListFiltersByRoleAndPagesInTwentyFives()
    {
        var withoutRole = await Read(await Send("GET", "/users?roleId=0"));
        Assert.All(withoutRole.GetProperty("items").EnumerateArray(), x => Assert.Empty(x.GetProperty("roleIds").EnumerateArray()));
        var admins = await Read(await Send("GET", "/users?roleId=1"));
        Assert.All(admins.GetProperty("items").EnumerateArray(), x => Assert.Contains(1L, x.GetProperty("roleIds").EnumerateArray().Select(r => r.GetInt64())));
        Assert.Equal(25, admins.GetProperty("pageSize").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("GET", "/users?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("GET", "/users?page=100001")).StatusCode);
        Assert.Empty((await Read(await Send("GET", "/users?page=50"))).GetProperty("items").EnumerateArray());
    }
}
