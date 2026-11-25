using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Controllers;
using IdentityTenant.Application.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Authorization.Tests;


public sealed class PlanCancellationTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private const string Plan = "/api/tenants/current/plan";
    private const string PlatformPlan = "/api/platform/tenants/1/plan";
    private const string OperatorKey = "operator-test-key";
    private OperatorActionTokens Tokens => fixture.App.Services.GetRequiredService<OperatorActionTokens>();
    private RecordingAccessRequestNotifier Notifier => fixture.App.Services.GetRequiredService<RecordingAccessRequestNotifier>();

    private async Task<HttpResponseMessage> Call(HttpMethod method, string url, string? token = null, object? body = null, string? operatorKey = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (operatorKey is not null) request.Headers.Add(PlatformTenantPlanController.KeyHeader, operatorKey);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private string Developer() => fixture.Token(100, 1, "DEVELOPER");
    private string Admin() => fixture.Token(102, 1, "ADMIN");
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();
    private Task<HttpResponseMessage> SetPlan(string[]? portals, string[]? features = null) => Call(HttpMethod.Put, PlatformPlan, body: new { portals, features }, operatorKey: OperatorKey);

    private Task<HttpResponseMessage> Cancel(string? portal, string? feature = null, string? token = null, string? action = "remove") =>
        Call(HttpMethod.Post, $"{Plan}/requests", token ?? Admin(), new { portal, feature, action });

    private async Task<string> CancelOk(string? portal, string? feature = null)
    {
        var response = await Cancel(portal, feature);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await Json(response);
        Assert.Equal("remove", body.GetProperty("action").GetString());
        return body.GetProperty("id").GetString()!;
    }

    private string Link(string id, OperatorAction action) => $"/operator/access-requests/{Tokens.Create(1, id, action, DateTimeOffset.UtcNow)}";
    private async Task<JsonElement[]> Requests() => (await Json(await Call(HttpMethod.Get, Plan, Admin()))).GetProperty("requests").EnumerateArray().ToArray();
    private async Task<JsonElement> Stored() => await Json(await Call(HttpMethod.Get, PlatformPlan, operatorKey: OperatorKey));
    private async Task<string[]> Portals() => Strings((await Stored()).GetProperty("portals"));
    private async Task<string[]?> Features() => (await Stored()).TryGetProperty("features", out var f) && f.ValueKind == JsonValueKind.Array ? Strings(f) : null;
    private static string[] Strings(JsonElement list) => list.EnumerateArray().Select(x => x.GetString()!).ToArray();

    
    private async Task ClearRequests()
    {
        foreach (var r in await Requests()) await Call(HttpMethod.Delete, $"{Plan}/requests/{r.GetProperty("id").GetString()}", Admin());
    }

    private async Task Scenario(string[]? portals, string[]? features, Func<Task> run)
    {
        await ClearRequests();
        try { Notifier.Clear(); Assert.Equal(HttpStatusCode.OK, (await SetPlan(portals, features)).StatusCode); await run(); }
        finally { await SetPlan(null); await ClearRequests(); }
    }

    [Fact]
    public Task AnAdminCanAskToCancelAnIncludedPortal_AndNothingChangesYet() => Scenario(["workspace", "employee", "timesheet"], null, async () =>
    {
        var id = await CancelOk("timesheet");

        Assert.Equal(["employee", "timesheet", "workspace"], await Portals());
        var open = Assert.Single(await Requests());
        Assert.Equal("remove", open.GetProperty("action").GetString());
        
        var sent = Assert.Single(Notifier.All);
        Assert.True(sent.Request.IsCancellation);
        Assert.Contains("cancellation request", AccessRequestEmailNotifier.Subject(sent.Company, sent.Request));
        Assert.Equal(id, sent.Request.Id);
    });

    [Fact]
    public Task AskingTwice_IsHarmless() => Scenario(["workspace", "employee", "timesheet"], null, async () =>
    {
        var first = await CancelOk("timesheet");
        var again = await Cancel("timesheet");          
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(first, (await Json(again)).GetProperty("id").GetString());
        Assert.Single(await Requests());
        Assert.Single(Notifier.All);                    
    });

    [Fact]
    public Task OnlyAnAdminCanAskToCancel() => Scenario(["workspace", "employee", "timesheet"], null, async () =>
    {
        var response = await Cancel("timesheet", token: Developer());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("ADMIN_ONLY", (await Json(response)).GetProperty("code").GetString());
        Assert.Empty(await Requests());
        Assert.Empty(Notifier.All);
    });

    [Fact]
    public Task YouCannotCancelWhatThePlanDoesNotInclude() => Scenario(["workspace", "employee"], null, async () =>
    {
        var response = await Cancel("timesheet");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("NOT_INCLUDED", (await Json(response)).GetProperty("code").GetString());

        
        Assert.Equal(HttpStatusCode.Conflict, (await Cancel(null, "athenna")).StatusCode);
        Assert.Empty(await Requests());
    });

    [Fact]
    public Task TheLastPortalCannotBeCancelled() => Scenario(["workspace"], null, async () =>
    {
        var response = await Cancel("workspace");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("LAST_PORTAL", (await Json(response)).GetProperty("code").GetString());
        Assert.Empty(await Requests());
    });

    [Fact]
    public Task AnUnknownAction_IsRefused() => Scenario(["workspace", "employee"], null, async () =>
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await Cancel("employee", action: "delete")).StatusCode);
        Assert.Empty(await Requests());
    });

    [Fact]
    public Task Approving_TakesOutExactlyThatPortal_AndClosesTheRequest() => Scenario(["workspace", "employee", "timesheet"], null, async () =>
    {
        var id = await CancelOk("timesheet");

        var page = await fixture.Client.GetAsync(Link(id, OperatorAction.Approve));
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Approve cancellation of Chronos", html);
        Assert.Equal(["employee", "timesheet", "workspace"], await Portals());      

        var done = await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.Contains("Cancelled", await done.Content.ReadAsStringAsync());
        Assert.Equal(["employee", "workspace"], await Portals());
        Assert.Empty(await Requests());
        Assert.Contains("Already handled", await (await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null)).Content.ReadAsStringAsync());

        
        Assert.Equal(HttpStatusCode.Created, (await Call(HttpMethod.Post, $"{Plan}/requests", Admin(), new { portal = "timesheet" })).StatusCode);
    });

    [Fact]
    public Task Declining_KeepsThePlanAsItWas() => Scenario(["workspace", "employee", "timesheet"], null, async () =>
    {
        var id = await CancelOk("timesheet");

        var done = await fixture.Client.PostAsync(Link(id, OperatorAction.Decline), null);

        Assert.Contains("keeps Chronos", await done.Content.ReadAsStringAsync());
        Assert.Equal(["employee", "timesheet", "workspace"], await Portals());
        Assert.Empty(await Requests());
    });

    [Fact]
    public Task AnUnrestrictedPlan_IsWrittenOutSoOnlyThatPortalGoes() => Scenario(null, null, async () =>
    {
        var id = await CancelOk("timesheet");

        await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Equal(["employee", "hub", "workspace"], await Portals());
        Assert.Null(await Features());        
    });

    [Fact]
    public Task CancellingAIWorkHub_TakesTheAiChatWithIt() => Scenario(["workspace", "employee", "hub"], ["athenna"], async () =>
    {
        var id = await CancelOk("hub");

        var done = await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Contains("The Athenna AI chat goes with it", await done.Content.ReadAsStringAsync());
        Assert.Equal(["employee", "workspace"], await Portals());
        
        Assert.Equal(["none"], await Features());
        var plan = await Json(await Call(HttpMethod.Get, Plan, Admin()));
        Assert.False(plan.GetProperty("features").EnumerateArray().Single().GetProperty("included").GetBoolean());
    });

    [Fact]
    public Task CancellingTheAiChatAlone_LeavesAIWorkHubInPlace() => Scenario(["workspace", "hub"], ["athenna"], async () =>
    {
        var id = await CancelOk(null, "athenna");

        await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Equal(["hub", "workspace"], await Portals());
        Assert.Equal(["none"], await Features());
    });

    [Fact]
    public Task CancellingTheAiChat_WhenFeaturesAreUnrestricted_WritesNone() => Scenario(["workspace", "hub"], null, async () =>
    {
        var id = await CancelOk(null, "athenna");

        await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Equal(["none"], await Features());
    });

    [Fact]
    public Task IfThePlanShrankToOnePortalMeanwhile_ApprovingTheCancellationIsRefused() => Scenario(["workspace", "timesheet"], null, async () =>
    {
        var id = await CancelOk("timesheet");
        await SetPlan(["timesheet"]);     

        var response = await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("only portal left", await response.Content.ReadAsStringAsync());
        Assert.Equal(["timesheet"], await Portals());
        Assert.Equal(HttpStatusCode.Conflict, (await Call(HttpMethod.Post, $"{PlatformPlan}/requests/{id}/approve", operatorKey: OperatorKey)).StatusCode);
    });

    [Fact]
    public Task ChangingThePlanByHand_ClosesTheCancellationItSettles() => Scenario(["workspace", "employee", "timesheet"], null, async () =>
    {
        await CancelOk("timesheet");

        await SetPlan(["workspace", "employee", "timesheet"]);      
        Assert.Single(await Requests());

        await SetPlan(["workspace", "employee"]);                   
        Assert.Empty(await Requests());
    });

    [Fact]
    public Task TheOperatorApi_CanApproveACancellation() => Scenario(["workspace", "employee", "timesheet"], null, async () =>
    {
        var id = await CancelOk("timesheet");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Call(HttpMethod.Post, $"{PlatformPlan}/requests/{id}/approve", operatorKey: "wrong")).StatusCode);
        Assert.Equal(["employee", "timesheet", "workspace"], await Portals());

        Assert.Equal(HttpStatusCode.NoContent, (await Call(HttpMethod.Post, $"{PlatformPlan}/requests/{id}/approve", operatorKey: OperatorKey)).StatusCode);
        Assert.Equal(["employee", "workspace"], await Portals());
    });

    [Fact]
    public Task AnAdminCanWithdrawTheirCancellationRequest() => Scenario(["workspace", "employee", "timesheet"], null, async () =>
    {
        var id = await CancelOk("timesheet");

        Assert.Equal(HttpStatusCode.NoContent, (await Call(HttpMethod.Delete, $"{Plan}/requests/{id}", Admin())).StatusCode);

        Assert.Empty(await Requests());
        Assert.Equal(["employee", "timesheet", "workspace"], await Portals());
        Assert.Contains("Already handled", await (await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null)).Content.ReadAsStringAsync());
    });

    [Fact]
    public void ARequestStoredBeforeCancellingExisted_MeansAdd()
    {
        var stored = JsonSerializer.SerializeToElement(new[] { new { id = "old", kind = "portal", name = "hub", userId = 1, userName = "Asha", note = (string?)null, createdAt = DateTimeOffset.UtcNow } });
        var read = TenantAccessRequests.Read(new Dictionary<string, object?> { [TenantAccessRequests.Key] = stored });

        var request = Assert.Single(read);
        Assert.Equal("add", request.Action);
        Assert.False(request.IsCancellation);
        Assert.Single(TenantAccessRequests.StillOpen(read, ["workspace"], []));
        Assert.Empty(TenantAccessRequests.StillOpen(read, ["workspace", "hub"], []));
    }

    [Fact]
    public void TheEmailForACancellation_SaysSo_AndOffersTheRightButtons()
    {
        var request = new AccessRequest("id1", "portal", "timesheet", 7, "Asha", null, DateTimeOffset.UtcNow, "remove");
        var links = new DecisionLinks("http://localhost:5233/operator/access-requests/AAA", "http://localhost:5233/operator/access-requests/BBB");

        Assert.Equal("AIWorkHub cancellation request: Grid Dynamics wants to cancel Chronos", AccessRequestEmailNotifier.Subject("Grid Dynamics", request));
        var text = AccessRequestEmailNotifier.Body(1, "Grid Dynamics", request, links);
        Assert.Contains("has asked to cancel Chronos", text);
        Assert.Contains("Approve cancellation: http://localhost:5233/operator/access-requests/AAA", text);
        Assert.Contains("Keep their plan: http://localhost:5233/operator/access-requests/BBB", text);
        var html = AccessRequestEmailNotifier.Html(1, "Grid Dynamics", request, links);
        Assert.Contains(">Approve cancellation</a>", html);
        Assert.Contains(">Keep their plan</a>", html);
    }
}
