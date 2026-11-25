using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace Backend.Authorization.Tests;





public sealed class IdentityContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null, long user = 100, long tenant = 1, string role = "", string[]? permissions = null, uint? version = null, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(user, tenant, role == "" ? (user == 100 || user == 200 ? "ADMIN" : "DEVELOPER") : role, permissions ?? SecurityFixture.Permissions));
        request.Content = content ?? (body is null ? null : JsonContent.Create(body));
        if (version.HasValue) request.Headers.TryAddWithoutValidation("If-Match", $"\"{version}\"");
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        Assert.True(r.IsSuccessStatusCode, await r.Content.ReadAsStringAsync());
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    
    private static readonly SemaphoreSlim ManagerGate = new(1, 1);
    private static long? managerId;
    private async Task<long> Manager()
    {
        await ManagerGate.WaitAsync();
        try
        {
            managerId ??= (await Json(await Send("POST", "/api/employees", new { userId = 100, employeeCode = "ABS100", firstName = "Manager", lastName = "Test", joiningDate = "2025-01-01" }))).GetProperty("id").GetInt64();
            return managerId.Value;
        }
        finally { ManagerGate.Release(); }
    }

    private static string[] Keys(JsonElement element) => element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray();
    private static string[] Expect(params string[] keys) => keys.Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task InvitationsKeepTheirExactShapeAndErrorCodes()
    {
        string[] invitationKeys = Expect("id", "tenantId", "email", "roleCode", "invitedByUserId", "createdAt", "expiresAt", "acceptedAt", "revokedAt");
        var created = await Send("POST", "/api/invitations", new { email = " Shape.Invite@Gmail.com ", expiresInDays = 3 });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/invitations", created.Headers.Location!.OriginalString);
        var invitation = await Json(created);
        Assert.Equal(invitationKeys, Keys(invitation));
        Assert.Equal("shape.invite@gmail.com", invitation.GetProperty("email").GetString());
        Assert.Equal(JsonValueKind.Null, invitation.GetProperty("acceptedAt").ValueKind);
        var id = invitation.GetProperty("id").GetGuid();

        async Task<string?> Code(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
        var invalid = await Send("POST", "/api/invitations", new { email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("INVALID_INVITATION", await Code(invalid));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("POST", "/api/invitations", new { email = "x@gmail.com", expiresInDays = 31 })).StatusCode);
        var duplicate = await Send("POST", "/api/invitations", new { email = "shape.invite@gmail.com" });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("INVITATION_NOT_AVAILABLE", await Code(duplicate));
        var member = await Send("POST", "/api/invitations", new { email = "user101@company.com" });
        Assert.Equal(HttpStatusCode.Conflict, member.StatusCode);
        Assert.Equal("INVITATION_NOT_AVAILABLE", await Code(member));

        var list = await Json(await Send("GET", "/api/invitations"));
        Assert.Equal(Expect("items", "page", "pageSize", "totalCount"), Keys(list));
        Assert.Equal(50, list.GetProperty("pageSize").GetInt32());
        Assert.Equal(invitationKeys, Keys(list.GetProperty("items").EnumerateArray().First()));
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("GET", "/api/invitations?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/invitations")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", $"/api/invitations/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/api/invitations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Send("DELETE", $"/api/invitations/{id}")).StatusCode);   
        Assert.Equal(HttpStatusCode.NotFound, (await Send("DELETE", $"/api/invitations/{id}", user: 200, tenant: 2)).StatusCode);
    }

    [Fact]
    public async Task TimesheetApproversAndOnboardingOptionsKeepTheirExactShape()
    {
        var approvers = await Json(await Send("GET", "/api/users/timesheet-approvers"));
        var admin = approvers.EnumerateArray().Single(a => a.GetProperty("id").GetInt64() == 100);
        Assert.Equal(Expect("id", "displayName", "roles"), Keys(admin));
        Assert.Equal("User 100", admin.GetProperty("displayName").GetString());
        Assert.Equal(new[] { "ADMIN" }, admin.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray());
        Assert.DoesNotContain(approvers.EnumerateArray(), a => a.GetProperty("id").GetInt64() == 200);   
        Assert.Equal(new long[] { 100, 101, 102 }, approvers.EnumerateArray().Select(a => a.GetProperty("id").GetInt64()).Order().ToArray());
        Assert.Equal(HttpStatusCode.BadRequest, (await Send("GET", "/api/users/timesheet-approvers?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("GET", "/api/users/timesheet-approvers", permissions: [])).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client.GetAsync("/api/users/timesheet-approvers")).StatusCode);

        Assert.True(await Manager() > 0);
        var options = await Json(await Send("GET", "/api/employees/onboarding/options", role: "TALENT_ACQUISITION"));
        Assert.Equal(Expect("references", "profiles", "levels", "managers", "teams", "employmentTypes", "employmentStatuses", "designations"), Keys(options));
        Assert.Equal(new[] { "FULL_TIME", "PART_TIME", "CONTRACT", "INTERN" }, options.GetProperty("employmentTypes").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(new[] { "ACTIVE", "ON_LEAVE", "TERMINATED", "INACTIVE" }, options.GetProperty("employmentStatuses").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(new[] { "Intern", "Junior", "Senior", "Lead", "Staff", "Principal" }, options.GetProperty("designations").EnumerateArray().Select(x => x.GetString()).ToArray());
        var candidate = options.GetProperty("managers").EnumerateArray().Single(m => m.GetProperty("employeeCode").GetString() == "ABS100");
        Assert.Equal(Expect("id", "firstName", "lastName", "employeeCode", "workEmail"), Keys(candidate));
        Assert.Equal(HttpStatusCode.Forbidden, (await Send("GET", "/api/employees/onboarding/options", role: "ADMIN")).StatusCode);
    }

    [Fact]
    public async Task EmployeePhotosKeepTheirExactShapeHeadersAndErrorMessages()
    {
        var employee = await Json(await Send("POST", "/api/employees", new { userId = 102, employeeCode = "PIC102", firstName = "Picture", lastName = "Test", joiningDate = "2025-01-01" }));
        var id = employee.GetProperty("id").GetInt64();
        var version = employee.GetProperty("version").GetUInt32();
        var png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/l9sAAAAASUVORK5CYII=");
        HttpContent File(byte[] bytes) => new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", "photo.png" } };
        string[] self = ["EMPLOYEE_VIEW", "EMPLOYEE_UPDATE_SELF"];

        var empty = await Send("POST", $"/api/employees/{id}/photo", user: 102, permissions: self, version: version, content: File([]));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal("Choose an image up to 2 MB.", (await empty.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        var notImage = await Send("POST", $"/api/employees/{id}/photo", user: 102, permissions: self, version: version, content: File("<svg/>"u8.ToArray()));
        Assert.Equal(HttpStatusCode.BadRequest, notImage.StatusCode);
        Assert.Equal("Choose a PNG, JPEG or WebP image.", (await notImage.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());

        var uploaded = await Json(await Send("POST", $"/api/employees/{id}/photo", user: 102, permissions: self, version: version, content: File(png)));
        Assert.Equal(Expect("profilePhotoUrl", "version"), Keys(uploaded));
        var url = uploaded.GetProperty("profilePhotoUrl").GetString()!;
        Assert.StartsWith($"/api/employees/{id}/photo/", url);
        Assert.True(uploaded.GetProperty("version").GetUInt32() > version);

        var fetched = await Send("GET", url, user: 102, permissions: ["EMPLOYEE_VIEW"]);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(png, await fetched.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/png", fetched.Content.Headers.ContentType!.MediaType);
        Assert.Equal("public, max-age=31536000, immutable", fetched.Headers.CacheControl!.ToString());
        Assert.Equal("nosniff", fetched.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal(HttpStatusCode.NotFound, (await Send("GET", $"/api/employees/{id}/photo/{Guid.NewGuid():N}", user: 102, permissions: ["EMPLOYEE_VIEW"])).StatusCode);
    }

    [Fact]
    public async Task AbsenceKeepsItsExactShapeForTheScreensAndTheAgent()
    {
        const string path = "/api/employees/absence";
        var managerEmployee = await Manager();
        await Json(await Send("POST", "/api/employees", new { userId = 104, employeeCode = "ABS104", firstName = "Employee", lastName = "Test", joiningDate = "2025-01-01", managerEmployeeId = managerEmployee }));
        var rowKeys = Expect("id", "employeeId", "employeeCode", "employeeName", "kind", "startDate", "endDate", "days", "reason", "status", "decisionNote", "decidedBy", "createdAt", "version");

        var unset = await Json(await Send("GET", path + "/policy?year=2028"));
        Assert.Equal(Expect("year", "casualDays", "sickDays", "earnedDays", "holidays", "version", "configured"), Keys(unset));
        Assert.False(unset.GetProperty("configured").GetBoolean());
        var badYear = await Send("GET", path + "/policy?year=1999");
        Assert.Equal(HttpStatusCode.BadRequest, badYear.StatusCode);
        var problem = await badYear.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Choose a year between 2000 and 2200.", problem.GetProperty("title").GetString());
        Assert.Equal("ABSENCE_VALIDATION", problem.GetProperty("code").GetString());

        var saved = await Json(await Send("PUT", path + "/policy?year=2028", new { casualDays = 5, sickDays = 6, earnedDays = 7, holidays = new[] { "2028-01-05" }, version = 0 }));
        Assert.Equal(Expect("year", "casualDays", "sickDays", "earnedDays", "holidays", "version", "configured"), Keys(saved));
        Assert.True(saved.GetProperty("configured").GetBoolean());
        Assert.Equal(new[] { "2028-01-05" }, saved.GetProperty("holidays").EnumerateArray().Select(h => h.GetString()).ToArray());
        var wrongYear = await Send("PUT", path + "/policy?year=2028", new { casualDays = 1, sickDays = 1, earnedDays = 1, holidays = new[] { "2029-01-05" }, version = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, wrongYear.StatusCode);
        Assert.Equal("ABSENCE_VALIDATION", (await wrongYear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var requested = await Json(await Send("POST", path, new { kind = "CASUAL", startDate = "2028-01-03", endDate = "2028-01-07", reason = " Family event " }, user: 104));
        Assert.Equal(rowKeys, Keys(requested));
        Assert.Equal("PENDING", requested.GetProperty("status").GetString());
        Assert.Equal(4, requested.GetProperty("days").GetInt32());   
        Assert.Equal("Family event", requested.GetProperty("reason").GetString());
        Assert.Equal("ABS104", requested.GetProperty("employeeCode").GetString());
        Assert.Equal("Employee Test", requested.GetProperty("employeeName").GetString());
        Assert.Equal(JsonValueKind.Null, requested.GetProperty("decidedBy").ValueKind);
        var id = requested.GetProperty("id").GetInt64();

        var mine = await Json(await Send("GET", path + "?year=2028", user: 104));
        Assert.Equal(Expect("items", "totalCount", "page", "pageSize", "totalPages", "balances", "manager", "hasReports", "policyConfigured"), Keys(mine));
        Assert.Equal(25, mine.GetProperty("pageSize").GetInt32());
        Assert.Equal(rowKeys, Keys(mine.GetProperty("items").EnumerateArray().Single()));
        var balance = mine.GetProperty("balances").EnumerateArray().First();
        Assert.Equal(Expect("kind", "allowance", "used", "pending", "available"), Keys(balance));
        Assert.Equal(1, balance.GetProperty("available").GetInt32());
        Assert.Equal(Expect("id", "name"), Keys(mine.GetProperty("manager")));
        Assert.False(mine.GetProperty("hasReports").GetBoolean());
        Assert.Equal(500, (await Json(await Send("GET", path + "?year=2028&all=true"))).GetProperty("pageSize").GetInt32());
        Assert.True((await Json(await Send("GET", path + "?year=2028"))).GetProperty("hasReports").GetBoolean());

        var invalid = await Send("POST", path + $"/{id}/decision", new { action = "MAYBE", version = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("Invalid decision.", (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
        var decided = await Json(await Send("POST", path + $"/{id}/decision", new { action = "APPROVE", version = 1, note = "Enjoy" }));
        Assert.Equal(rowKeys, Keys(decided));
        Assert.Equal("APPROVED", decided.GetProperty("status").GetString());
        Assert.Equal(100, decided.GetProperty("decidedBy").GetInt64());
        Assert.Equal("Enjoy", decided.GetProperty("decisionNote").GetString());
        Assert.Equal(2, decided.GetProperty("version").GetInt32());
        var processed = await Send("POST", path + $"/{id}/decision", new { action = "APPROVE", version = 2 });
        Assert.Equal(HttpStatusCode.Conflict, processed.StatusCode);
        var conflict = await processed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("This request has already been processed.", conflict.GetProperty("title").GetString());
        Assert.Equal("ABSENCE_CONFLICT", conflict.GetProperty("code").GetString());
    }
}
