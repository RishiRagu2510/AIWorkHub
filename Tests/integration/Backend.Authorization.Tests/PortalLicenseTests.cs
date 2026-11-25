using System.Net.Http.Json;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using Authorization;
using TenantContext;

namespace Backend.Authorization.Tests;

public sealed class PortalLicenseUnitTests
{
    private static ClaimsPrincipal User(params string[] portals) =>
        new(new ClaimsIdentity(portals.Select(p => new Claim(TenantClaimTypes.Portal, p)), "tests"));

    [Theory]
    [InlineData("timesheet")]
    [InlineData("employee")]
    [InlineData("workspace")]
    [InlineData("hub")]
    public void ACompanyWithNoPortalSetting_IsNotRestricted(string portal) =>
        Assert.True(PortalLicense.IsLicensed(User(), portal));

    [Theory]
    [InlineData("workspace", "timesheet", false)]
    [InlineData("workspace", "employee", false)]
    [InlineData("workspace", "hub", false)]
    [InlineData("workspace", "workspace", true)]
    [InlineData("gridhub", "workspace", true)]        
    [InlineData("workspace", "gridhub", true)]
    [InlineData("chronos", "timesheet", true)]
    [InlineData("timesheet", "chronos", true)]
    [InlineData("persona", "employee", true)]
    [InlineData("aiworkhub", "hub", true)]
    [InlineData("WORKSPACE", "workspace", true)]      
    [InlineData("chronos", "employee", false)]
    public void OnlyThePortalsInTheLicence_AreOpen(string licensed, string asked, bool expected) =>
        Assert.Equal(expected, PortalLicense.IsLicensed(User(licensed), asked));

    [Fact]
    public void SeveralPortals_AreAllOpen()
    {
        var user = User("workspace", "chronos");
        Assert.True(PortalLicense.IsLicensed(user, "workspace"));
        Assert.True(PortalLicense.IsLicensed(user, "timesheet"));
        Assert.False(PortalLicense.IsLicensed(user, "employee"));
    }

    [Theory]
    [InlineData("TIMESHEET_VIEW", "timesheet")]
    [InlineData("TIMESHEET_TEAM_VIEW", "timesheet")]
    [InlineData("TIMESHEET_APPROVE", "timesheet")]
    [InlineData("ISSUE_VIEW", null)]
    [InlineData("WORKLOG_CREATE", null)]     
    [InlineData("EMPLOYEE_VIEW", null)]
    public void EachPermission_KnowsWhichPortalOwnsIt(string permission, string? owner) =>
        Assert.Equal(owner, PortalLicense.OwningPortal(permission));

    [Theory]
    [InlineData("TIMESHEET_VIEW", false)]       
    [InlineData("TIMESHEET_TEAM_VIEW", false)]
    [InlineData("ISSUE_VIEW", true)]
    [InlineData("WORKLOG_CREATE", true)]
    [InlineData("EMPLOYEE_VIEW", true)]
    public void AGridHubAndPersonaCompany_IsNotIssuedChronosPermissions(string permission, bool expected) =>
        Assert.Equal(expected, PortalLicense.IsPermissionLicensed(["workspace", "persona"], permission));

    [Fact]
    public void AChronosLicence_BringsTheTimesheetPermissionsBack() =>
        Assert.True(PortalLicense.IsPermissionLicensed(["gridhub", "persona", "chronos"], "TIMESHEET_VIEW"));

    [Fact]
    public void ACompanyWithNoPortalSetting_KeepsEveryPermission()
    {
        Assert.True(PortalLicense.IsPermissionLicensed([], "TIMESHEET_VIEW"));
        Assert.True(PortalLicense.IsPermissionLicensed(["", "  "], "TIMESHEET_TEAM_VIEW"));
    }

    private static ClaimsPrincipal WithFeatures(params string[] features) =>
        new(new ClaimsIdentity(features.Select(f => new Claim(TenantClaimTypes.Feature, f)), "tests"));

    [Fact]
    public void ACompanyWithNoFeatureSetting_HasEveryFeature() =>
        Assert.True(FeatureLicense.IsLicensed(WithFeatures(), "athenna"));

    [Theory]
    [InlineData("athenna", "athenna", true)]
    [InlineData("ATHENNA", "athenna", true)]     
    [InlineData("none", "athenna", false)]       
    [InlineData("other", "athenna", false)]
    public void OnlyTheFeaturesInThePlan_AreOpen(string listed, string asked, bool expected) =>
        Assert.Equal(expected, FeatureLicense.IsLicensed(WithFeatures(listed), asked));

    [Fact]
    public void PortalsAndTheFeatureList_AreSeparateLists()
    {
        var gridHubAndPersonaWithoutAi = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(TenantClaimTypes.Portal, "workspace"), new Claim(TenantClaimTypes.Portal, "employee"), new Claim(TenantClaimTypes.Feature, "none")], "tests"));
        Assert.True(PortalLicense.IsLicensed(gridHubAndPersonaWithoutAi, "workspace"));
        Assert.True(PortalLicense.IsLicensed(gridHubAndPersonaWithoutAi, "employee"));
        Assert.False(PortalLicense.IsLicensed(gridHubAndPersonaWithoutAi, "timesheet"));
        Assert.False(FeatureLicense.IsLicensed(gridHubAndPersonaWithoutAi, "athenna"));
    }

    private static ClaimsPrincipal With(string[] portals, params string[] features) =>
        new(new ClaimsIdentity(
            portals.Select(p => new Claim(TenantClaimTypes.Portal, p)).Concat(features.Select(f => new Claim(TenantClaimTypes.Feature, f))), "tests"));

    [Theory]
    [InlineData("workspace,employee", "", false)]           
    [InlineData("workspace,employee", "athenna", false)]    
    [InlineData("workspace,employee,hub", "", true)]        
    [InlineData("workspace,employee,hub", "athenna", true)]
    [InlineData("workspace,employee,hub", "none", false)]   
    [InlineData("workspace,employee,aiworkhub", "", true)]  
    [InlineData("", "", true)]                              
    [InlineData("", "none", false)]
    public void TheAiChat_ComesWithAIWorkHub(string portals, string features, bool expected) =>
        Assert.Equal(expected, FeatureLicense.IsLicensed(
            With(portals.Split(',', StringSplitOptions.RemoveEmptyEntries), features.Split(',', StringSplitOptions.RemoveEmptyEntries)), "athenna"));

    [Fact]
    public void TheListRuleAlone_DoesNotLookAtPortals() =>
        Assert.True(FeatureLicense.IsListed([], "athenna"));

    [Fact]
    public void ABlankFeatureEntry_DoesNotLockAnyoneOut() =>
        Assert.True(FeatureLicense.IsListed(["", "  "], "athenna"));

    [Fact]
    public void ANullFeatureList_MeansNoPlanSetting() =>
        Assert.True(FeatureLicense.IsListed((IEnumerable<string>?)null, "athenna"));

    [Fact]
    public void AnUnknownFeatureName_IsRefusedWhenTheEndpointIsWritten() =>
        Assert.Throws<ArgumentException>(() => new RequireFeatureAttribute("payroll"));

    [Fact]
    public void TheFeatureAttribute_UsesTheFeaturePolicy() =>
        Assert.Equal("Feature:athenna", new RequireFeatureAttribute("Athenna").Policy);

    [Fact]
    public void ABlankPortalClaim_DoesNotLockAnyoneOut() =>
        Assert.True(PortalLicense.IsLicensed(User("  ", ""), "timesheet"));

    [Fact]
    public void AnUnknownPortalName_IsRefusedWhenTheEndpointIsWritten() =>
        Assert.Throws<ArgumentException>(() => new RequirePortalAttribute("payroll"));

    [Fact]
    public void TheAttribute_UsesThePortalPolicy() =>
        Assert.Equal("Portal:timesheet", new RequirePortalAttribute("Timesheet").Policy);
}





public sealed class PortalLicenseApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private static readonly string[] Everything = [.. SecurityFixture.Permissions, "EMPLOYEE_VIEW", "EMPLOYEE_VIEW_TEAM", "EMPLOYEE_CREATE", "EMPLOYEE_UPDATE_SELF",
        "TIMESHEET_VIEW", "TIMESHEET_TEAM_VIEW", "WORKLOG_VIEW", "WORKLOG_CREATE"];

    private async Task<HttpStatusCode> Get(string path, string role, params string[]? portals)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",
            fixture.Token(100, 1, role, Everything, portals is { Length: > 0 } ? portals : null));
        using var response = await fixture.Client.SendAsync(request);
        return response.StatusCode;
    }

    private const string Month = "from=2025-04-01&to=2025-04-30";

    
    public static TheoryData<string, string> Owned => new()
    {
        { $"/api/timesheets/me?{Month}", "DEVELOPER" },
        { $"/api/timesheets/team?{Month}", "ADMIN" },
        { "/api/employees/setup", "ADMIN" },
        { "/api/employees/onboarding/options", "TALENT_ACQUISITION" },
        { "/api/employees/me/skills", "DEVELOPER" },
        { "/api/employees/me/languages", "DEVELOPER" },
        { "/api/employees/org-chart", "ADMIN" },
        { "/api/employees/career-catalog", "ADMIN" },
        { "/api/employees/manager-options", "ADMIN" },
    };

    
    public static TheoryData<string, string> Shared => new()
    {
        { $"/api/worklogs?{Month}", "DEVELOPER" },
        { "/api/employees", "ADMIN" },
        { "/api/employees/references", "ADMIN" },
        { "/api/employees/me", "DEVELOPER" },
        { "/api/employees/absence", "DEVELOPER" },
        { "/api/issues", "DEVELOPER" },
        { "/api/issues/999999/employee-request", "DEVELOPER" },
        { "/api/projects", "ADMIN" },
        { "/api/notifications", "DEVELOPER" },
    };

    [Theory]
    [MemberData(nameof(Owned))]
    public async Task AGridHubOnlyCompany_IsRefusedOnAnotherPortalsEndpoints(string path, string role)
    {
        Assert.Equal(HttpStatusCode.Forbidden, await Get(path, role, "workspace"));
        Assert.Equal(HttpStatusCode.Forbidden, await Get(path, role, "gridhub"));
    }

    [Theory]
    [MemberData(nameof(Owned))]
    public async Task AnUnrestrictedCompany_IsNotRefusedByTheLicence(string path, string role) =>
        Assert.NotEqual(HttpStatusCode.Forbidden, await Get(path, role));

    [Theory]
    [InlineData($"/api/timesheets/me?{Month}", "DEVELOPER", "chronos")]
    [InlineData($"/api/timesheets/me?{Month}", "DEVELOPER", "timesheet")]
    [InlineData("/api/employees/setup", "ADMIN", "persona")]
    [InlineData("/api/employees/setup", "ADMIN", "employee")]
    [InlineData("/api/employees/me/skills", "DEVELOPER", "persona")]
    public async Task AnEndpointOpensForACompanyLicensedForItsPortal(string path, string role, string portal) =>
        Assert.NotEqual(HttpStatusCode.Forbidden, await Get(path, role, "workspace", portal));

    [Theory]
    [MemberData(nameof(Shared))]
    public async Task SharedEndpoints_AnswerAGridHubOnlyCompanyLikeAnyOther(string path, string role)
    {
        var unrestricted = await Get(path, role);
        var gridHubOnly = await Get(path, role, "workspace");

        Assert.Equal(unrestricted, gridHubOnly);
    }

    
    [Theory]
    [InlineData("/api/issues/employee-requests", "TALENT_ACQUISITION")]
    [InlineData("/api/issues/1/employee-request/review", "ADMIN")]
    [InlineData("/api/issues/1/employee-request/complete", "ADMIN")]
    public async Task OnboardingWrites_AreRefusedForACompanyWithoutPersona_ButOpenWithIt(string path, string role)
    {
        async Task<HttpStatusCode> Post(params string[] portals)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(100, 1, role, Everything, portals.Length > 0 ? portals : null));
            using var response = await fixture.Client.SendAsync(request);
            return response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.Forbidden, await Post("workspace"));
        Assert.NotEqual(HttpStatusCode.Forbidden, await Post("workspace", "employee"));
        Assert.NotEqual(HttpStatusCode.Forbidden, await Post());
    }

    [Fact]
    public async Task ASignedOutCaller_IsStillUnauthorized_NotForbidden()
    {
        using var response = await fixture.Client.GetAsync($"/api/timesheets/me?{Month}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
