using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Authentication;
using Authorization;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Controllers;
using IdentityTenant.Application.Plans;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TenantContext;

namespace Backend.Authorization.Tests;

public sealed class TenantPlanRulesTests
{
    [Fact]
    public void ProductNamesAreStoredAsTheShortNames_WithoutDuplicates()
    {
        var (portals, features, error) = TenantPlanSettings.Normalize(["GridHub", "persona", "workspace"], ["Athenna"]);
        Assert.Null(error);
        Assert.Equal(["employee", "workspace"], portals);
        Assert.Equal(["athenna"], features);
    }

    [Fact]
    public void LeavingAPartOut_RemovesItsRestriction()
    {
        var (portals, features, error) = TenantPlanSettings.Normalize(null, null);
        Assert.Null(error);
        Assert.Null(portals);
        Assert.Null(features);
    }

    [Theory]
    [MemberData(nameof(BadPlans))]
    public void AnUnclearPlan_IsRejectedInsteadOfSilentlyMeaningUnrestricted(string[]? portals, string[]? features, string expected)
    {
        var (_, _, error) = TenantPlanSettings.Normalize(portals, features);
        Assert.NotNull(error);
        Assert.Contains(expected, error);
    }

    public static IEnumerable<object?[]> BadPlans() =>
    [
        [new string[0], null, "at least one portal"],
        [new[] { "workspace", "" }, null, "at least one portal"],
        [new[] { "payroll" }, null, "Unknown portal"],
        [null, new string[0], "at least one entry"],
        [null, new[] { "telepathy" }, "Unknown feature"],
        [null, new[] { "none", "athenna" }, "cannot be combined"],
    ];

    [Fact]
    public async Task WithoutAConfiguredKey_TheEndpointDoesNotExist()
    {
        var controller = new PlatformTenantPlanController(null!, new ConfigurationBuilder().Build(), NullLogger<PlatformTenantPlanController>.Instance)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Request.Headers[PlatformTenantPlanController.KeyHeader] = "anything";
        Assert.IsType<NotFoundResult>(await controller.Get(1, default));
        Assert.IsType<NotFoundResult>(await controller.Put(1, new(["workspace"], null), default));
    }
}





public sealed class TenantPlanApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private const string Key = "operator-test-key";
    private static string Url(long tenant = 1) => $"/api/platform/tenants/{tenant}/plan";

    private async Task<HttpResponseMessage> Send(HttpMethod method, long tenant = 1, object? body = null, string? key = Key, string? bearer = null)
    {
        using var request = new HttpRequestMessage(method, Url(tenant));
        if (key is not null) request.Headers.Add(PlatformTenantPlanController.KeyHeader, key);
        if (bearer is not null) request.Headers.Authorization = new("Bearer", bearer);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<(string[]? Portals, string[]? Features)> Plan(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        string[]? List(string name) => json.GetProperty(name).ValueKind == JsonValueKind.Null ? null : json.GetProperty(name).EnumerateArray().Select(e => e.GetString()!).ToArray();
        return (List("portals"), List("features"));
    }

    private static async Task AssertPlan(HttpResponseMessage response, string[]? portals, string[]? features)
    {
        var (actualPortals, actualFeatures) = await Plan(response);
        Assert.Equal(portals, actualPortals);
        Assert.Equal(features, actualFeatures);
    }

    private async Task<ClaimsPrincipal> SignIn(string email, string client = "client")
    {
        using var scope = fixture.App.Services.CreateScope();
        
        var service = client == "client"
            ? scope.ServiceProvider.GetRequiredService<CompanyPrincipalService>()
            : ActivatorUtilities.CreateInstance<CompanyPrincipalService>(scope.ServiceProvider,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Authentication:Google:ClientId"] = client }).Build());
        return await service.SignInGoogleAsync(SecurityFixture.Google(email), default);
    }

    [Fact]
    public async Task ANeighbourOrAnAdmin_CannotSetThePlan()
    {
        var body = new { portals = new[] { "workspace" } };
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(HttpMethod.Put, body: body, key: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(HttpMethod.Put, body: body, key: "guess")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(HttpMethod.Get, key: "")).StatusCode);
        
        var admin = fixture.Token(100, 1, "ADMIN");
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(HttpMethod.Put, body: body, key: null, bearer: admin)).StatusCode);
    }

    [Fact]
    public async Task AnUnknownCompany_IsNotFound() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Send(HttpMethod.Put, tenant: 999, body: new { portals = new[] { "workspace" } })).StatusCode);

    [Fact]
    public async Task InvalidPlans_AreRefusedAndNothingIsChanged()
    {
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Put, body: new { portals = new[] { "workspace" } })).StatusCode);
            foreach (var bad in new object[] { new { portals = new string[0] }, new { portals = new[] { "payroll" } }, new { features = new[] { "none", "athenna" } } })
                Assert.Equal(HttpStatusCode.BadRequest, (await Send(HttpMethod.Put, body: bad)).StatusCode);
            var (portals, _) = await Plan(await Send(HttpMethod.Get));
            Assert.Equal(["workspace"], portals);
        }
        finally { await Send(HttpMethod.Put, body: new { }); }
    }

    [Fact]
    public async Task AGridHubAndPersonaPlanWithoutAi_ReachesTheTokenAndCanBeLifted()
    {
        var put = await Send(HttpMethod.Put, body: new { portals = new[] { "gridhub", "persona" }, features = new[] { "none" } });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        await AssertPlan(put, ["employee", "workspace"], ["none"]);
        await AssertPlan(await Send(HttpMethod.Get), ["employee", "workspace"], ["none"]);

        
        var restricted = await SignIn("user101@company.com");
        Assert.Equal(["employee", "workspace"], restricted.FindAll(TenantClaimTypes.Portal).Select(c => c.Value).Order());
        Assert.Equal(["none"], restricted.FindAll(TenantClaimTypes.Feature).Select(c => c.Value));
        Assert.DoesNotContain(restricted.FindAll(TenantClaimTypes.Permission), c => c.Value.StartsWith("TIMESHEET_"));
        Assert.Contains(restricted.FindAll(TenantClaimTypes.Permission), c => c.Value == "ISSUE_VIEW");
        Assert.Contains(restricted.FindAll(TenantClaimTypes.Permission), c => c.Value == "WORKLOG_CREATE");
        Assert.True(PortalLicense.IsLicensed(restricted, "workspace"));
        Assert.False(PortalLicense.IsLicensed(restricted, "timesheet"));
        Assert.False(FeatureLicense.IsLicensed(restricted, "athenna"));

        
        var token = fixture.App.Services.GetRequiredService<IAccessTokenService>().CreateAccessToken(restricted);
        using var chronos = new HttpRequestMessage(HttpMethod.Get, "/api/timesheets/me?from=2025-04-01&to=2025-04-30");
        chronos.Headers.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(chronos)).StatusCode);

        
        var other = await SignIn("user200@other.com", "other-client");
        Assert.Empty(other.FindAll(TenantClaimTypes.Portal));
        Assert.Contains(other.FindAll(TenantClaimTypes.Permission), c => c.Value == "TIMESHEET_VIEW");

        
        var lifted = await Send(HttpMethod.Put, body: new { });
        await AssertPlan(lifted, null, null);
        var open = await SignIn("user101@company.com");
        Assert.Empty(open.FindAll(TenantClaimTypes.Portal));
        Assert.Empty(open.FindAll(TenantClaimTypes.Feature));
        Assert.Contains(open.FindAll(TenantClaimTypes.Permission), c => c.Value == "TIMESHEET_VIEW");
    }

    [Fact]
    public async Task SettingThePlan_KeepsTheCompanysOtherSettings()
    {
        using (var scope = fixture.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityTenantDbContext>();
            var row = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == 2);
            if (row is null) { row = new TenantSettings(2); db.TenantSettings.Add(row); }
            row.Update(row.Timezone, row.Locale, new Dictionary<string, object?> { ["theme"] = "dark" });
            await db.SaveChangesAsync();
        }
        try
        {
            Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Put, tenant: 2, body: new { portals = new[] { "workspace" } })).StatusCode);
            using var check = fixture.App.Services.CreateScope();
            var saved = await check.ServiceProvider.GetRequiredService<IdentityTenantDbContext>().TenantSettings.AsNoTracking().SingleAsync(s => s.TenantId == 2);
            Assert.Equal("dark", saved.Settings["theme"]?.ToString());
            Assert.Equal(["workspace"], TenantPlanSettings.ReadList(saved.Settings, TenantPlanSettings.PortalsKey));
        }
        finally { await Send(HttpMethod.Put, tenant: 2, body: new { }); }
    }
}
