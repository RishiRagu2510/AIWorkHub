using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;





public sealed class EmployeeApiContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, uint? version = null, long user = 100, long tenant = 1)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, tenant, user == 100 ? "ADMIN" : "DEVELOPER", SecurityFixture.Permissions));
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

    private static readonly string[] ProfileKeys = Expect("roles", "id", "userId", "employeeCode", "firstName", "lastName", "preferredName", "workEmail", "workAddress",
        "phone", "aadharNumber", "permanentAddress", "emergencyContactName", "emergencyContactPhone", "emergencyContactRelation", "profilePhotoUrl",
        "departmentId", "disciplineId", "jobTitleId", "designationId", "gradeId", "careerProfileId", "careerLevelId", "locationId", "managerEmployeeId",
        "employmentType", "employmentStatus", "joiningDate", "endDate", "createdAt", "updatedAt", "version");

    private static readonly string[] ReferenceKeys = Expect("id", "tenantId", "version", "kind", "name");

    [Fact]
    public async Task ProfilesListsAndOptionsKeepTheirExactShape()
    {
        var created = await Send("POST", "/api/employees", new { userId = 100, employeeCode = "SHAPE100", firstName = "Shape", lastName = "Admin", joiningDate = "2025-01-01", phone = "123" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Matches("^\"\\d+\"$", created.Headers.ETag!.Tag);
        var admin = await Json(created);
        Assert.Equal(created.Headers.Location!.OriginalString, $"/api/employees/{admin.GetProperty("id").GetInt64()}");
        Assert.Equal(ProfileKeys, Keys(admin));
        Assert.Equal(JsonValueKind.Null, admin.GetProperty("roles").ValueKind);   
        Assert.Equal("123", admin.GetProperty("phone").GetString());
        var adminId = admin.GetProperty("id").GetInt64();

        var second = await Json(await Send("POST", "/api/employees", new { userId = 101, employeeCode = "SHAPE101", firstName = "Shape", lastName = "Dev", joiningDate = "2025-02-01", managerEmployeeId = adminId }));
        var secondId = second.GetProperty("id").GetInt64();

        var one = await Send("GET", $"/api/employees/{secondId}");
        Assert.Matches("^\"\\d+\"$", one.Headers.ETag!.Tag);
        var profile = await Json(one);
        Assert.Equal(ProfileKeys, Keys(profile));
        Assert.Equal(JsonValueKind.Array, profile.GetProperty("roles").ValueKind);
        Assert.Equal(adminId, profile.GetProperty("managerEmployeeId").GetInt64());
        var me = await Send("GET", "/api/employees/me");
        Assert.NotNull(me.Headers.ETag);
        Assert.Equal(ProfileKeys, Keys(await Json(me)));

        var updated = await Send("PUT", $"/api/employees/{secondId}", new { userId = 101, employeeCode = "SHAPE101", firstName = "Shape", lastName = "Developer", joiningDate = "2025-02-01", managerEmployeeId = adminId }, profile.GetProperty("version").GetUInt32());
        Assert.NotNull(updated.Headers.ETag);
        var updatedBody = await Json(updated);
        Assert.Equal(ProfileKeys, Keys(updatedBody));
        Assert.Equal("Developer", updatedBody.GetProperty("lastName").GetString());

        var self = await Send("PUT", "/api/employees/me/profile", new { preferredName = "Dev", phone = "555", locationId = (long?)null }, updatedBody.GetProperty("version").GetUInt32(), user: 101);
        Assert.Equal(ProfileKeys, Keys(await Json(self)));

        var list = await Json(await Send("GET", "/api/employees?pageSize=10"));
        Assert.Equal(Expect("items", "page", "pageSize", "totalCount", "totalPages", "maxEmployeeNumber"), Keys(list));
        Assert.Equal(ProfileKeys, Keys(list.GetProperty("items").EnumerateArray().First()));
        Assert.Equal(10, list.GetProperty("pageSize").GetInt32());
        
        Assert.Equal(JsonValueKind.Null, list.GetProperty("items").EnumerateArray().First(x => x.GetProperty("id").GetInt64() == adminId).GetProperty("phone").ValueKind);
        Assert.Equal("123", (await Json(await Send("GET", $"/api/employees/{adminId}"))).GetProperty("phone").GetString());

        var options = await Json(await Send("GET", "/api/employees/manager-options"));
        Assert.Equal(Expect("id", "userId", "firstName", "lastName", "employeeCode", "workEmail"), Keys(options.EnumerateArray().First()));
        
        Assert.DoesNotContain((await Json(await Send("GET", $"/api/employees/manager-options?employeeId={adminId}"))).EnumerateArray(), x => x.GetProperty("id").GetInt64() == secondId);

        var users = await Json(await Send("GET", "/api/employees/login-users"));
        Assert.Equal(Expect("items", "page", "pageSize", "totalCount", "totalPages"), Keys(users));
        Assert.Equal(Expect("id", "displayName", "email"), Keys(users.GetProperty("items").EnumerateArray().First()));

        var chart = await Json(await Send("GET", "/api/employees/org-chart"));
        Assert.Equal(Expect("items", "page", "pageSize", "totalCount"), Keys(chart));
        var node = chart.GetProperty("items").EnumerateArray().First(x => x.GetProperty("id").GetInt64() == adminId);
        Assert.Equal(Expect("id", "firstName", "lastName", "employeeCode", "managerEmployeeId", "jobTitleId", "departmentId", "locationId", "profilePhotoUrl", "careerTitle", "directReportCount", "hasReports"), Keys(node));
        Assert.Equal(1, node.GetProperty("directReportCount").GetInt32());
        Assert.True(node.GetProperty("hasReports").GetBoolean());
        Assert.Equal(1, (await Json(await Send("GET", "/api/employees/org-chart?roleLevel=ADMIN"))).GetProperty("items").GetArrayLength());

        var history = await Json(await Send("GET", $"/api/employees/{adminId}/career-history"));
        Assert.Equal(Expect("id", "effectiveDate", "recordedAt", "recordedBy", "changeType", "details", "note"), Keys(history.EnumerateArray().First()));
        Assert.Equal("INITIAL", history.EnumerateArray().First().GetProperty("changeType").GetString());
    }

    [Fact]
    public async Task CareerCatalogAndReferencesKeepTheirExactShape()
    {
        async Task<JsonElement> Setup(string kind, object body)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/employees/setup/{kind}") { Content = JsonContent.Create(body) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(100, 1, "ADMIN", SecurityFixture.Permissions));
            return await Json(await fixture.Client.SendAsync(request));
        }
        var department = await Setup("departments", new { name = "Catalog dept", code = "CATALOG_DEPT" });
        var grade = await Setup("grades", new { name = "Catalog grade", code = "CATALOG_GRADE" });
        await Setup("titles", new { name = "Catalog title", departmentId = department.GetProperty("id").GetInt64(), gradeId = grade.GetProperty("id").GetInt64() });
        await Setup("skills", new { name = "Catalog skill", code = "CATALOG_SKILL", category = "Core" });

        var catalog = await Json(await Send("GET", "/api/employees/career-catalog"));
        Assert.Equal(Expect("skills", "grades", "profiles", "levels"), Keys(catalog));
        Assert.Equal(Expect("id", "name", "category", "description"), Keys(catalog.GetProperty("skills").EnumerateArray().First()));
        Assert.Equal(Expect("id", "code", "name", "family"), Keys(catalog.GetProperty("grades").EnumerateArray().First()));
        Assert.Equal(Expect("id", "code", "name"), Keys(catalog.GetProperty("profiles").EnumerateArray().First()));
        Assert.Equal(Expect("id", "careerProfileId", "gradeId", "grade", "careerTitle"), Keys(catalog.GetProperty("levels").EnumerateArray().First()));

        var added = await Send("POST", "/api/employees/references", new { kind = "LOCATION", name = "Chennai" });
        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal("/api/employees/references", added.Headers.Location!.OriginalString);
        var reference = await Json(added);
        Assert.Equal(ReferenceKeys, Keys(reference));
        var id = reference.GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/api/employees/references", new { kind = "NOPE", name = "x" })).StatusCode);

        var renamed = await Json(await Send("PUT", $"/api/employees/references/{id}", new { kind = "LOCATION", name = "Chennai HQ" }, reference.GetProperty("version").GetUInt32()));
        Assert.Equal(ReferenceKeys, Keys(renamed));
        Assert.Equal("Chennai HQ", renamed.GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await Send("PUT", $"/api/employees/references/{id}", new { kind = "LOCATION", name = "No version" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("PUT", $"/api/employees/references/{id}", new { kind = "LOCATION", name = "Stale" }, 0)).StatusCode);

        var all = await Json(await Send("GET", "/api/employees/references"));
        Assert.Equal(ReferenceKeys, Keys(all.EnumerateArray().First()));
        Assert.Contains(all.EnumerateArray(), x => x.GetProperty("name").GetString() == "Chennai HQ");
    }

    [Fact]
    public async Task DeletingAnEmployeeNeedsAVersionAndKeepsTheirReportsInMind()
    {
        var boss = await Json(await Send("POST", "/api/employees", new { userId = 102, employeeCode = "BOSS102", firstName = "Boss", lastName = "One", joiningDate = "2025-01-01" }));
        var report = await Json(await Send("POST", "/api/employees", new { userId = 103, employeeCode = "REP103", firstName = "Rep", lastName = "One", joiningDate = "2025-01-01", managerEmployeeId = (long?)null }));
        var bossId = boss.GetProperty("id").GetInt64();
        var reportId = report.GetProperty("id").GetInt64();
        
        Assert.Equal(HttpStatusCode.PreconditionRequired, (await Send("DELETE", $"/api/employees/{reportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send("DELETE", $"/api/employees/{reportId}", version: 0)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/api/employees/{reportId}", version: report.GetProperty("version").GetUInt32())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send("GET", $"/api/employees/{reportId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/api/employees/{bossId}", version: boss.GetProperty("version").GetUInt32())).StatusCode);
        
        Assert.DoesNotContain((await Json(await Send("GET", "/api/employees/login-users"))).GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetInt64() == 103);
    }
}
