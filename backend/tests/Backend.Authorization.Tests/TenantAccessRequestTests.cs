using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityTenant.Api.Controllers;

namespace Backend.Authorization.Tests;





public sealed class TenantAccessRequestTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private const string OperatorKey = "operator-test-key";
    private const string Plan = "/api/tenants/current/plan";

    private async Task<HttpResponseMessage> Call(HttpMethod method, string url, string? token = null, object? body = null, string? operatorKey = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new("Bearer", token);
        if (operatorKey is not null) request.Headers.Add(PlatformTenantPlanController.KeyHeader, operatorKey);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SetPlan(string[]? portals, string[]? features = null, long tenant = 1) =>
        Call(HttpMethod.Put, $"/api/platform/tenants/{tenant}/plan", body: new { portals, features }, operatorKey: OperatorKey);

    private string Developer(long user = 100, long tenant = 1) => fixture.Token(user, tenant, "DEVELOPER");
    private string Admin(long user = 102, long tenant = 1) => fixture.Token(user, tenant, "ADMIN");

    private static async Task<JsonElement> Json(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>());

    private async Task<JsonElement[]> Requests(string token) =>
        (await Json(await Call(HttpMethod.Get, Plan, token))).GetProperty("requests").EnumerateArray().ToArray();

    
    private async Task WithGridHubAndPersona(Func<Task> scenario)
    {
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await SetPlan(["workspace", "employee"])).StatusCode);
            await scenario();
        }
        finally
        {
            await SetPlan(null);
            await SetPlan(null, tenant: 2);
        }
    }

    [Fact]
    public Task TheAskerAndTheAdminSeeTheRequest_ColleaguesDoNot() => WithGridHubAndPersona(async () =>
    {
        var response = await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos", note = "  We need timesheets for payroll.  " });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await Json(response);
        Assert.Equal("timesheet", created.GetProperty("name").GetString());
        Assert.Equal("We need timesheets for payroll.", created.GetProperty("note").GetString());

        Assert.Single(await Requests(Developer()));
        Assert.Empty(await Requests(Developer(user: 101)));

        var admin = await Requests(Admin());
        var seen = Assert.Single(admin);
        Assert.Equal(100, seen.GetProperty("userId").GetInt64());
        Assert.False(string.IsNullOrWhiteSpace(seen.GetProperty("userName").GetString()));
    });

    [Fact]
    public Task AskingTwiceKeepsTheFirstRequest() => WithGridHubAndPersona(async () =>
    {
        var first = await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "timesheet" });
        var second = await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "Chronos", note = "again" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await Json(first)).GetProperty("id").GetString(), (await Json(second)).GetProperty("id").GetString());
        Assert.Single(await Requests(Admin()));
    });

    [Fact]
    public Task AskingForTheAiChat_IsARequestToo() => WithGridHubAndPersona(async () =>
    {
        await SetPlan(["workspace", "employee", "hub"], ["none"]);   
        Assert.Equal(HttpStatusCode.Created, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { feature = "athenna" })).StatusCode);
        Assert.Equal("athenna", Assert.Single(await Requests(Admin())).GetProperty("name").GetString());
    });

    [Fact]
    public Task AskingForTheAiChat_WithoutAIWorkHub_IsRefused_BecauseItComesWithAIWorkHub() => WithGridHubAndPersona(async () =>
    {
        var response = await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { feature = "athenna" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("REQUIRES_PORTAL", body.GetProperty("code").GetString());
        Assert.Equal("hub", body.GetProperty("requires").GetString());
        Assert.Empty(await Requests(Admin()));
    });

    [Fact]
    public Task TheAiChat_IsNotIncluded_WhenTheCompanyHasNoAIWorkHub_EvenWithNoFeatureSetting() => WithGridHubAndPersona(async () =>
    {
        
        var athenna = (await Json(await Call(HttpMethod.Get, Plan, Developer()))).GetProperty("features")[0];
        Assert.Equal("athenna", athenna.GetProperty("id").GetString());
        Assert.False(athenna.GetProperty("included").GetBoolean());
        Assert.Equal("hub", athenna.GetProperty("requires").GetString());

        await SetPlan(["workspace", "employee", "hub"]);
        Assert.True((await Json(await Call(HttpMethod.Get, Plan, Developer()))).GetProperty("features")[0].GetProperty("included").GetBoolean());
    });

    [Fact]
    public Task SomethingTheCompanyAlreadyHas_IsNotRequestedAgain() => WithGridHubAndPersona(async () =>
    {
        var response = await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "gridhub" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("ALREADY_INCLUDED", (await Json(response)).GetProperty("code").GetString());
        Assert.Empty(await Requests(Admin()));
    });

    [Fact]
    public async Task ACompanyWithoutAPlanHasEverything_SoThereIsNothingToRequest()
    {
        await SetPlan(null);
        Assert.Equal(HttpStatusCode.Conflict, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "timesheet" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { feature = "athenna" })).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"portal\":\"chronos\",\"feature\":\"athenna\"}")]
    [InlineData("{\"portal\":\"payroll\"}")]
    [InlineData("{\"feature\":\"telepathy\"}")]
    public Task AnUnclearRequest_IsRefused(string json) => WithGridHubAndPersona(async () =>
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Plan}/requests") { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new("Bearer", Developer());
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await Requests(Admin()));
    });

    [Fact]
    public Task ALongNote_IsRefused() => WithGridHubAndPersona(async () =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos", note = new string('x', 501) })).StatusCode));

    [Fact]
    public async Task SignedOutCallers_AreRefused()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Call(HttpMethod.Get, Plan)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Call(HttpMethod.Post, $"{Plan}/requests", body: new { portal = "chronos" })).StatusCode);
    }

    [Fact]
    public Task ThePlanScreenShowsWhatIsIncluded() => WithGridHubAndPersona(async () =>
    {
        await SetPlan(["workspace", "employee"], ["none"]);
        var plan = await Json(await Call(HttpMethod.Get, Plan, Developer()));
        var portals = plan.GetProperty("portals").EnumerateArray().ToDictionary(p => p.GetProperty("id").GetString()!, p => p.GetProperty("included").GetBoolean());
        Assert.Equal(new Dictionary<string, bool> { ["workspace"] = true, ["employee"] = true, ["timesheet"] = false, ["hub"] = false }, portals);
        Assert.False(plan.GetProperty("features")[0].GetProperty("included").GetBoolean());
        Assert.False(plan.GetProperty("isAdmin").GetBoolean());
        Assert.True((await Json(await Call(HttpMethod.Get, Plan, Admin()))).GetProperty("isAdmin").GetBoolean());
    });

    [Fact]
    public Task ARequest_NeverChangesThePlan() => WithGridHubAndPersona(async () =>
    {
        await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos" });
        var plan = await Json(await Call(HttpMethod.Get, "/api/platform/tenants/1/plan", operatorKey: OperatorKey));
        Assert.Equal(["employee", "workspace"], plan.GetProperty("portals").EnumerateArray().Select(p => p.GetString()).ToArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await Call(HttpMethod.Get, "/api/timesheets/me?from=2025-04-01&to=2025-04-30",
            fixture.Token(100, 1, "DEVELOPER", portals: ["workspace", "employee"]))).StatusCode);
    });

    [Fact]
    public Task AnotherCompany_NeverSeesIt() => WithGridHubAndPersona(async () =>
    {
        await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos" });
        Assert.Empty(await Requests(Admin(tenant: 2)));
    });

    [Fact]
    public Task SettingThePlanToIncludeIt_ClosesTheRequest_AndLeavesTheOthers() => WithGridHubAndPersona(async () =>
    {
        await SetPlan(["workspace", "employee", "hub"], ["none"]);
        Assert.Equal(HttpStatusCode.Created, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos" })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { feature = "athenna" })).StatusCode);

        await SetPlan(["workspace", "employee", "hub", "timesheet"], ["none"]);

        Assert.Equal("athenna", Assert.Single(await Requests(Admin())).GetProperty("name").GetString());
    });

    

    private async Task<string> Ask(string token, string portal = "chronos")
    {
        var response = await Call(HttpMethod.Post, $"{Plan}/requests", token, new { portal });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    [Fact]
    public Task APersonCanCancelTheirOwnRequest_AndThenAskAgain() => WithGridHubAndPersona(async () =>
    {
        var id = await Ask(Developer());

        Assert.Equal(HttpStatusCode.NoContent, (await Call(HttpMethod.Delete, $"{Plan}/requests/{id}", Developer())).StatusCode);

        Assert.Empty(await Requests(Admin()));
        Assert.NotEqual(id, await Ask(Developer()));   
    });

    [Fact]
    public Task AColleagueCannotCancelSomeoneElsesRequest_AndIsToldItDoesNotExist() => WithGridHubAndPersona(async () =>
    {
        var id = await Ask(Developer(100));

        var response = await Call(HttpMethod.Delete, $"{Plan}/requests/{id}", Developer(101));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);   
        Assert.Single(await Requests(Admin()));
    });

    [Fact]
    public Task AnAdminCanCancelAnyRequestOfTheirCompany() => WithGridHubAndPersona(async () =>
    {
        var id = await Ask(Developer(100));
        Assert.Equal(HttpStatusCode.NoContent, (await Call(HttpMethod.Delete, $"{Plan}/requests/{id}", Admin())).StatusCode);
        Assert.Empty(await Requests(Admin()));
    });

    [Fact]
    public Task AnAdminOfAnotherCompany_CannotCancelIt() => WithGridHubAndPersona(async () =>
    {
        var id = await Ask(Developer(100));
        var otherAdmin = fixture.Token(300, 2, "ADMIN");
        Assert.Equal(HttpStatusCode.NotFound, (await Call(HttpMethod.Delete, $"{Plan}/requests/{id}", otherAdmin)).StatusCode);
        Assert.Single(await Requests(Admin()));
    });

    [Fact]
    public Task CancellingNeedsSigningIn_AndAnUnknownRequestIsNotFound() => WithGridHubAndPersona(async () =>
    {
        var id = await Ask(Developer());
        Assert.Equal(HttpStatusCode.Unauthorized, (await Call(HttpMethod.Delete, $"{Plan}/requests/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Call(HttpMethod.Delete, $"{Plan}/requests/does-not-exist", Developer())).StatusCode);
        Assert.Single(await Requests(Admin()));
    });

    [Fact]
    public Task Cancelling_NeverChangesThePlan_AndLeavesTheOtherRequests() => WithGridHubAndPersona(async () =>
    {
        var chronos = await Ask(Developer(), "chronos");
        await Ask(Developer(), "hub");

        await Call(HttpMethod.Delete, $"{Plan}/requests/{chronos}", Developer());

        Assert.Equal("hub", Assert.Single(await Requests(Admin())).GetProperty("name").GetString());
        var plan = await Json(await Call(HttpMethod.Get, "/api/platform/tenants/1/plan", operatorKey: OperatorKey));
        Assert.Equal(2, plan.GetProperty("portals").GetArrayLength());
    });

    [Fact]
    public Task TheOperatorSeesTheRequests_AndCanDeclineOne() => WithGridHubAndPersona(async () =>
    {
        await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos" });
        var list = await Json(await Call(HttpMethod.Get, "/api/platform/tenants/1/plan/requests", operatorKey: OperatorKey));
        var id = Assert.Single(list.EnumerateArray()).GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Call(HttpMethod.Delete, $"/api/platform/tenants/1/plan/requests/{id}", operatorKey: "wrong")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Call(HttpMethod.Delete, "/api/platform/tenants/1/plan/requests/nope", operatorKey: OperatorKey)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Call(HttpMethod.Delete, $"/api/platform/tenants/1/plan/requests/{id}", operatorKey: OperatorKey)).StatusCode);

        Assert.Empty(await Requests(Admin()));
        
        var plan = await Json(await Call(HttpMethod.Get, "/api/platform/tenants/1/plan", operatorKey: OperatorKey));
        Assert.Equal(2, plan.GetProperty("portals").GetArrayLength());
    });
}
