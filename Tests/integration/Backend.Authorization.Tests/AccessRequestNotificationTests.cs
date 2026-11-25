using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IdentityTenant.Api.Authentication;
using IdentityTenant.Api.Controllers;
using IdentityTenant.Application.Plans;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Backend.Authorization.Tests;


public sealed class RecordingAccessRequestNotifier : IAccessRequestNotifier
{
    public sealed record Sent(long TenantId, string Company, AccessRequest Request);
    private readonly List<Sent> _sent = [];
    public bool Fail { get; set; }
    public IReadOnlyList<Sent> All { get { lock (_sent) return _sent.ToList(); } }
    public void Clear() { lock (_sent) _sent.Clear(); }

    public Task NotifyAsync(long tenantId, string companyName, AccessRequest request, CancellationToken ct = default)
    {
        if (Fail) return Task.FromException(new InvalidOperationException("mail server down"));   
        lock (_sent) _sent.Add(new Sent(tenantId, companyName, request));
        return Task.CompletedTask;
    }
}


public sealed class AccessRequestNotificationTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private const string Plan = "/api/tenants/current/plan";
    private const string OperatorKey = "operator-test-key";
    private RecordingAccessRequestNotifier Notifier => fixture.App.Services.GetRequiredService<RecordingAccessRequestNotifier>();

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

    private async Task Scenario(Func<Task> run)
    {
        Notifier.Clear(); Notifier.Fail = false;
        try { Assert.Equal(HttpStatusCode.OK, (await SetPlan(["workspace", "employee"])).StatusCode); await run(); }
        finally { Notifier.Fail = false; Notifier.Clear(); await SetPlan(null); }
    }

    [Fact]
    public Task ANewRequest_TellsTheOperatorWhoAskedForWhat() => Scenario(async () =>
    {
        var response = await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos", note = "Payroll needs it" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var sent = Assert.Single(Notifier.All);
        Assert.Equal(1, sent.TenantId);
        Assert.False(string.IsNullOrWhiteSpace(sent.Company));
        Assert.Equal("timesheet", sent.Request.Name);
        Assert.Equal(100, sent.Request.UserId);
        Assert.Equal("Payroll needs it", sent.Request.Note);
    });

    [Fact]
    public Task AskingAgain_DoesNotTellTheOperatorAgain() => Scenario(async () =>
    {
        await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos" });
        Assert.Equal(HttpStatusCode.OK, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos" })).StatusCode);
        Assert.Single(Notifier.All);
    });

    [Fact]
    public Task ARequestThatIsRefused_TellsNobody() => Scenario(async () =>
    {
        Assert.Equal(HttpStatusCode.Conflict, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "gridhub" })).StatusCode);   
        Assert.Equal(HttpStatusCode.BadRequest, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "nope" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { feature = "athenna" })).StatusCode);   
        Assert.Equal(HttpStatusCode.Unauthorized, (await Call(HttpMethod.Post, $"{Plan}/requests", null, new { portal = "chronos" })).StatusCode);
        Assert.Empty(Notifier.All);
    });

    [Fact]
    public Task ABrokenMailServer_DoesNotStopTheRequest() => Scenario(async () =>
    {
        Notifier.Fail = true;
        var response = await Call(HttpMethod.Post, $"{Plan}/requests", Developer(), new { portal = "chronos" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);   
        Assert.Single((await Call(HttpMethod.Get, Plan, fixture.Token(102, 1, "ADMIN")).Result.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("requests").EnumerateArray());
    });

    

    private static AccessRequestEmailNotifier Real(Dictionary<string, string?> settings, string environment = "Development")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new(configuration, new FakeEnvironment { EnvironmentName = environment }, new OperatorActionTokens(configuration), NullLogger<AccessRequestEmailNotifier>.Instance);
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = "/";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static readonly AccessRequest Sample = new("id1", "portal", "timesheet", 7, "Asha", "Payroll needs it", DateTimeOffset.Parse("2026-11-13T04:12:00Z"));

    [Theory]
    [InlineData("ops@example.com", 1)]
    [InlineData("ops@example.com; owner@example.com", 2)]
    [InlineData("ops@example.com, not-an-address, owner@example.com", 2)]   
    [InlineData("", 0)]
    public void TheRecipients_AreReadFromTheSetting(string setting, int expected) =>
        Assert.Equal(expected, Real(new() { [AccessRequestEmailNotifier.RecipientsKey] = setting }).Recipients().Count);

    [Fact]
    public async Task WithNoAddressOrNoMailServer_NothingIsSent_AndNothingThrows()
    {
        await Real(new()).NotifyAsync(1, "Grid Dynamics", Sample);                                                   
        await Real(new() { [AccessRequestEmailNotifier.RecipientsKey] = "ops@example.com" }).NotifyAsync(1, "Grid Dynamics", Sample);   
    }

    [Fact]
    public async Task AMailServerThatCannotBeReached_IsSwallowed()
    {
        var settings = new Dictionary<string, string?>
        {
            [AccessRequestEmailNotifier.RecipientsKey] = "ops@example.com", ["Smtp:Host"] = "127.0.0.1", ["Smtp:Port"] = "1", ["Smtp:From"] = "noreply@example.com", ["Smtp:EnableSsl"] = "false",
        };
        await Real(settings).NotifyAsync(1, "Grid Dynamics", Sample);   
    }

    private static readonly DecisionLinks SampleLinks = new("http://localhost:5233/operator/access-requests/AAA", "http://localhost:5233/operator/access-requests/BBB");

    [Fact]
    public void TheMail_SaysWhoWantsWhat_AndHasApproveAndDeclineLinks()
    {
        Assert.Equal("AIWorkHub access request: Grid Dynamics wants Chronos", AccessRequestEmailNotifier.Subject("Grid Dynamics", Sample));
        var body = AccessRequestEmailNotifier.Body(1, "Grid Dynamics", Sample, SampleLinks);
        Assert.Contains("Grid Dynamics (tenant 1) has asked for Chronos", body);
        Assert.Contains("Asha (user 7)", body);
        Assert.Contains("Payroll needs it", body);
        Assert.Contains("Approve: http://localhost:5233/operator/access-requests/AAA", body);
        Assert.Contains("Decline: http://localhost:5233/operator/access-requests/BBB", body);
        Assert.DoesNotContain("curl", body);
        Assert.DoesNotContain("operator-test-key", body);

        var html = AccessRequestEmailNotifier.Html(1, "Grid Dynamics", Sample, SampleLinks);
        Assert.Contains(">Approve</a>", html);
        Assert.Contains(">Decline</a>", html);
        Assert.Contains("http://localhost:5233/operator/access-requests/AAA", html);
    }

    [Fact]
    public void WithoutLinks_TheMailSaysWhyThereAreNoButtons()
    {
        var body = AccessRequestEmailNotifier.Body(1, "Grid Dynamics", Sample, null);
        Assert.DoesNotContain("Approve:", body);
        Assert.Contains("operator API", body);
        Assert.DoesNotContain(">Approve</a>", AccessRequestEmailNotifier.Html(1, "Grid Dynamics", Sample, null));
    }

    [Fact]
    public void WhatAPersonTyped_CannotInjectMarkupIntoTheOperatorsEmail()
    {
        var hostile = new AccessRequest("id1", "portal", "timesheet", 7, "<img src=x onerror=alert(1)>", "<script>alert(1)</script>", DateTimeOffset.UtcNow);
        var html = AccessRequestEmailNotifier.Html(1, "<b>Evil</b>", hostile, SampleLinks);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b>Evil</b>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void TheLinks_PointAtTheConfiguredAddress_OrThisMachineInDevelopment_AndNowhereElse()
    {
        var withKey = new Dictionary<string, string?> { ["Platform:OperatorKey"] = "k-for-tests-0123456789" };

        var dev = Real(withKey).LinksFor(1, Sample, DateTimeOffset.UtcNow);
        Assert.StartsWith("http://localhost:5233/operator/access-requests/", dev!.ApproveUrl);
        Assert.StartsWith("http://localhost:5233/operator/access-requests/", dev.DeclineUrl);
        Assert.NotEqual(dev.ApproveUrl, dev.DeclineUrl);

        var configured = Real(new(withKey) { [AccessRequestEmailNotifier.BaseUrlKey] = "https://ops.example.com" }, "Production").LinksFor(1, Sample, DateTimeOffset.UtcNow);
        Assert.StartsWith("https://ops.example.com/operator/access-requests/", configured!.ApproveUrl);

        
        Assert.Null(Real(withKey, "Production").LinksFor(1, Sample, DateTimeOffset.UtcNow));
        
        Assert.Null(Real(new() { [AccessRequestEmailNotifier.BaseUrlKey] = "https://ops.example.com" }).LinksFor(1, Sample, DateTimeOffset.UtcNow));
        
        Assert.Null(Real(new(withKey) { [AccessRequestEmailNotifier.BaseUrlKey] = "javascript:alert(1)" }, "Production").LinksFor(1, Sample, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ACompanyNameWithLineBreaks_CannotAddMailHeaders()
    {
        var subject = AccessRequestEmailNotifier.Subject("Evil\r\nBcc: attacker@example.com", Sample);
        Assert.DoesNotContain('\r', subject);
        Assert.DoesNotContain('\n', subject);
    }

    [Theory]
    [InlineData("portal", "hub", "AIWorkHub")]
    [InlineData("portal", "workspace", "GridHub")]
    [InlineData("portal", "employee", "Persona")]
    [InlineData("portal", "timesheet", "Chronos")]
    [InlineData("feature", "athenna", "the Athenna AI chat")]
    [InlineData("portal", "something-new", "something-new")]
    public void RequestsAreNamedTheWayPeopleKnowThem(string kind, string name, string expected) =>
        Assert.Equal(expected, TenantAccessRequests.DisplayName(kind, name));
}
