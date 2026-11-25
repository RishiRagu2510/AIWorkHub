using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Controllers;
using IdentityTenant.Application.Plans;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Authorization.Tests;

public sealed class OperatorActionTokenTests
{
    private static OperatorActionTokens Tokens(string? key = "operator-key-for-tests-0123456789") =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Platform:OperatorKey"] = key }).Build());

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-13T05:00:00Z");

    [Theory]
    [InlineData(OperatorAction.Approve)]
    [InlineData(OperatorAction.Decline)]
    public void ALinkCarriesExactlyOneDecisionOnOneRequest(OperatorAction action)
    {
        var tokens = Tokens();
        var claim = tokens.Verify(tokens.Create(7, "abc123", action, Now), Now.AddHours(1));
        Assert.Equal(new OperatorActionClaim(7, "abc123", action, Now.Add(OperatorActionTokens.Lifetime)), claim);
    }

    [Fact]
    public void ALinkStopsWorkingAfterSevenDays()
    {
        var tokens = Tokens();
        var token = tokens.Create(1, "r", OperatorAction.Approve, Now);
        Assert.NotNull(tokens.Verify(token, Now.AddDays(6.9)));
        Assert.Null(tokens.Verify(token, Now.AddDays(7.1)));
    }

    [Fact]
    public void ChangingTheOperatorKey_CancelsEveryLinkAlreadySent()
    {
        var token = Tokens("first-key-0123456789abcdef").Create(1, "r", OperatorAction.Approve, Now);
        Assert.Null(Tokens("second-key-0123456789abcdef").Verify(token, Now));
    }

    [Fact]
    public void WithoutAnOperatorKey_NoLinkCanBeMade_AndNoneIsAccepted()
    {
        var token = Tokens().Create(1, "r", OperatorAction.Approve, Now);
        Assert.Null(Tokens(null).Create(1, "r", OperatorAction.Approve, Now));
        Assert.Null(Tokens("   ").Verify(token, Now));
    }

    [Fact]
    public void AnEditedLink_IsRefused_NotReadAsADifferentDecision()
    {
        var tokens = Tokens();
        var token = tokens.Create(1, "r", OperatorAction.Decline, Now)!;
        var (payload, signature) = (token.Split('.')[0], token.Split('.')[1]);

        
        var other = tokens.Create(2, "r", OperatorAction.Decline, Now)!;
        Assert.Null(tokens.Verify(other.Split('.')[0] + "." + signature, Now));
        Assert.Null(tokens.Verify(payload + "." + other.Split('.')[1], Now));
        Assert.Null(tokens.Verify(payload + "." + signature[..^2] + "AA", Now));
        Assert.Null(tokens.Verify(payload, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a.b")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    [InlineData("....")]
    public void AnythingThatIsNotALink_IsSimplyNotValid(string? token) =>
        Assert.Null(Tokens().Verify(token, Now));
}


public sealed class OperatorDecisionTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private const string Plan = "/api/tenants/current/plan";
    private const string OperatorKey = "operator-test-key";
    private OperatorActionTokens Tokens => fixture.App.Services.GetRequiredService<OperatorActionTokens>();

    private async Task<HttpResponseMessage> Call(HttpMethod method, string url, string? token = null, object? body = null, string? operatorKey = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (operatorKey is not null) request.Headers.Add(PlatformTenantPlanController.KeyHeader, operatorKey);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private Task<HttpResponseMessage> SetPlan(string[]? portals, string[]? features = null) =>
        Call(HttpMethod.Put, "/api/platform/tenants/1/plan", body: new { portals, features }, operatorKey: OperatorKey);

    private string Developer() => fixture.Token(100, 1, "DEVELOPER");
    private string Admin() => fixture.Token(102, 1, "ADMIN");
    private static async Task<JsonElement> Json(HttpResponseMessage r) => await r.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<string> Ask(string? portal = "chronos", string? feature = null)
    {
        var response = await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), portal is not null ? new { portal } : new { feature });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await Json(response)).GetProperty("id").GetString()!;
    }

    private string Link(string id, OperatorAction action) => $"/operator/access-requests/{Tokens.Create(1, id, action, DateTimeOffset.UtcNow)}";
    private async Task<JsonElement[]> Requests() => (await Json(await Call(HttpMethod.Get, Plan, Admin()))).GetProperty("requests").EnumerateArray().ToArray();
    private async Task<string[]> Portals() => (await Json(await Call(HttpMethod.Get, "/api/platform/tenants/1/plan", operatorKey: OperatorKey))).GetProperty("portals").EnumerateArray().Select(p => p.GetString()!).ToArray();

    private async Task Scenario(string[] portals, string[]? features, Func<Task> run)
    {
        try { Assert.Equal(HttpStatusCode.OK, (await SetPlan(portals, features)).StatusCode); await run(); }
        finally { await SetPlan(null); }
    }

    [Fact]
    public Task OpeningTheLink_ShowsTheRequest_AndChangesNothing() => Scenario(["workspace", "employee"], null, async () =>
    {
        var id = await Ask();

        
        foreach (var action in new[] { OperatorAction.Approve, OperatorAction.Decline, OperatorAction.Approve })
        {
            var page = await fixture.Client.GetAsync(Link(id, action));
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var html = await page.Content.ReadAsStringAsync();
            Assert.Contains("Chronos", html);
            Assert.Contains("<form method=\"post\">", html);
        }

        Assert.Equal(["employee", "workspace"], await Portals());
        Assert.Single(await Requests());
    });

    [Fact]
    public Task ThePage_IsNotCachedFramedOrLeakedThroughTheReferrer() => Scenario(["workspace", "employee"], null, async () =>
    {
        var page = await fixture.Client.GetAsync(Link(await Ask(), OperatorAction.Approve));
        Assert.Contains("no-store", page.Headers.CacheControl!.ToString());
        Assert.Equal("no-referrer", page.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("DENY", page.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("frame-ancestors 'none'", page.Headers.GetValues("Content-Security-Policy").Single());
    });

    [Fact]
    public Task Approving_AddsExactlyThatPortal_AndClosesTheRequest() => Scenario(["workspace", "employee"], null, async () =>
    {
        var id = await Ask("chronos");
        await Ask("hub");

        var done = await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.Contains("Approved", await done.Content.ReadAsStringAsync());
        Assert.Equal(["employee", "timesheet", "workspace"], await Portals());           
        Assert.Equal("hub", Assert.Single(await Requests()).GetProperty("name").GetString());   
    });

    [Fact]
    public Task Declining_RemovesTheRequest_AndLeavesThePlanAlone() => Scenario(["workspace", "employee"], null, async () =>
    {
        var id = await Ask("chronos");

        var done = await fixture.Client.PostAsync(Link(id, OperatorAction.Decline), null);

        Assert.Equal(HttpStatusCode.OK, done.StatusCode);
        Assert.Contains("Declined", await done.Content.ReadAsStringAsync());
        Assert.Empty(await Requests());
        Assert.Equal(["employee", "workspace"], await Portals());
    });

    [Fact]
    public Task AUsedLink_SaysItWasAlreadyHandled_AndDoesNothingTwice() => Scenario(["workspace", "employee"], null, async () =>
    {
        var id = await Ask("chronos");
        var link = Link(id, OperatorAction.Approve);
        await fixture.Client.PostAsync(link, null);

        var again = await fixture.Client.PostAsync(link, null);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Contains("Already handled", await again.Content.ReadAsStringAsync());
        Assert.Contains("Already handled", await (await fixture.Client.GetAsync(link)).Content.ReadAsStringAsync());
        Assert.Equal(["employee", "timesheet", "workspace"], await Portals());

        
        Assert.Contains("Already handled", await (await fixture.Client.PostAsync(Link(id, OperatorAction.Decline), null)).Content.ReadAsStringAsync());
    });

    [Fact]
    public Task ACancelledRequest_CannotBeApprovedFromTheEmailAnyMore() => Scenario(["workspace", "employee"], null, async () =>
    {
        var id = await Ask("chronos");
        await Call(HttpMethod.Delete, $"{Plan}/requests/{id}", Developer());

        var response = await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Contains("Already handled", await response.Content.ReadAsStringAsync());
        Assert.Equal(["employee", "workspace"], await Portals());
    });

    [Fact]
    public Task AForgedOrExpiredLink_ChangesNothing() => Scenario(["workspace", "employee"], null, async () =>
    {
        var id = await Ask("chronos");
        var genuine = Link(id, OperatorAction.Approve);

        foreach (var url in new[] { "/operator/access-requests/not-a-link", "/operator/access-requests/a.b", genuine + "x", genuine[..^3] + "AAA" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsync(url, null)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.GetAsync(url)).StatusCode);
        }
        var expired = $"/operator/access-requests/{Tokens.Create(1, id, OperatorAction.Approve, DateTimeOffset.UtcNow.AddDays(-8))}";
        Assert.Equal(HttpStatusCode.BadRequest, (await fixture.Client.PostAsync(expired, null)).StatusCode);

        Assert.Equal(["employee", "workspace"], await Portals());
        Assert.Single(await Requests());
    });

    [Fact]
    public Task ALinkForOneCompany_CannotDecideAnotherCompanysRequest() => Scenario(["workspace", "employee"], null, async () =>
    {
        var id = await Ask("chronos");
        var otherCompany = $"/operator/access-requests/{Tokens.Create(2, id, OperatorAction.Approve, DateTimeOffset.UtcNow)}";

        Assert.Contains("Already handled", await (await fixture.Client.PostAsync(otherCompany, null)).Content.ReadAsStringAsync());
        Assert.Single(await Requests());
    });

    [Fact]
    public Task ApprovingTheAiChat_ReplacesNoneWithAthenna_WhenTheCompanyHasAIWorkHub() => Scenario(["workspace", "employee", "hub"], ["none"], async () =>
    {
        var id = await Ask(portal: null, feature: "athenna");

        await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        var plan = await Json(await Call(HttpMethod.Get, "/api/platform/tenants/1/plan", operatorKey: OperatorKey));
        Assert.Equal(["athenna"], plan.GetProperty("features").EnumerateArray().Select(f => f.GetString()!).ToArray());
        Assert.Equal(["employee", "hub", "workspace"], await Portals());
        Assert.Empty(await Requests());
    });

    [Fact]
    public Task TheAiChat_CannotBeApprovedWithoutAIWorkHub_AndNothingChanges() => Scenario(["workspace", "employee", "hub"], ["none"], async () =>
    {
        var id = await Ask(portal: null, feature: "athenna");
        await SetPlan(["workspace", "employee"], ["none"]);   

        var response = await fixture.Client.PostAsync(Link(id, OperatorAction.Approve), null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("comes with AIWorkHub", await response.Content.ReadAsStringAsync());
        Assert.Equal(["employee", "workspace"], await Portals());
        Assert.Single(await Requests());
    });

    [Fact]
    public Task TheOperatorApi_CanApproveToo_WithTheOperatorKey() => Scenario(["workspace", "employee"], null, async () =>
    {
        var id = await Ask("chronos");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Call(HttpMethod.Post, $"/api/platform/tenants/1/plan/requests/{id}/approve")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Call(HttpMethod.Post, $"/api/platform/tenants/1/plan/requests/{id}/approve", operatorKey: "wrong")).StatusCode);
        Assert.Equal(["employee", "workspace"], await Portals());

        Assert.Equal(HttpStatusCode.NoContent, (await Call(HttpMethod.Post, $"/api/platform/tenants/1/plan/requests/{id}/approve", operatorKey: OperatorKey)).StatusCode);
        Assert.Equal(["employee", "timesheet", "workspace"], await Portals());
        Assert.Equal(HttpStatusCode.NotFound, (await Call(HttpMethod.Post, $"/api/platform/tenants/1/plan/requests/{id}/approve", operatorKey: OperatorKey)).StatusCode);
    });
}
