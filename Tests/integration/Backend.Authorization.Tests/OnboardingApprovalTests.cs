using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Authentication;
using IdentityTenant.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Authorization.Tests;





public sealed class OnboardingApprovalTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private const string Key = "test-onboarding-approval-key-0123456789abcdef";
    private static readonly DateTimeOffset Now = new(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
    private readonly OnboardingApprovalSigner _signer = new(Key);

    private string Genuine(long tenant = 1, long issue = 7) => _signer.Sign(tenant, issue, "new.hire@company.com", 3, DateTimeOffset.UtcNow);

    [Fact]
    public void AGenuineApproval_IsAcceptedForItsCompanyAndRequest()
    {
        var claims = _signer.Verify(_signer.Sign(1, 7, "new.hire@company.com", 3, Now), 1, 7, Now.AddSeconds(30));
        Assert.NotNull(claims);
        Assert.Equal(("new.hire@company.com", 3L), (claims.WorkEmail, claims.TeamId));
    }

    [Fact]
    public void ARequestWithoutATeam_KeepsItsMissingTeam() =>
        Assert.Null(_signer.Verify(_signer.Sign(1, 7, "a@b.co", null, Now), 1, 7, Now)!.TeamId);

    [Fact]
    public void AnApproval_ExpiresAfterTwoMinutes()
    {
        var token = _signer.Sign(1, 7, "a@b.co", null, Now);
        Assert.NotNull(_signer.Verify(token, 1, 7, Now.AddSeconds(119)));
        Assert.Null(_signer.Verify(token, 1, 7, Now.AddSeconds(121)));
    }

    [Fact]
    public void AnApproval_IsOnlyForTheCompanyAndRequestItNames()
    {
        var token = _signer.Sign(1, 7, "a@b.co", null, Now);
        Assert.Null(_signer.Verify(token, 2, 7, Now));   
        Assert.Null(_signer.Verify(token, 1, 8, Now));   
    }

    [Fact]
    public void ASignatureFromAnotherKey_IsRejected() =>
        Assert.Null(_signer.Verify(new OnboardingApprovalSigner("some-other-key-that-is-long-enough-0123456789").Sign(1, 7, "a@b.co", null, Now), 1, 7, Now));

    [Fact]
    public void ChangingAnythingInTheApproval_BreaksIt()
    {
        var token = _signer.Sign(1, 7, "a@b.co", null, Now);
        var parts = token.Split('.');
        var forgedPayload = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[0].Replace('-', '+').Replace('_', '/').PadRight(((parts[0].Length + 3) / 4) * 4, '='))).Replace("a@b.co", "me@evil.co")))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        Assert.Null(_signer.Verify($"{forgedPayload}.{parts[1]}", 1, 7, Now));
        Assert.Null(_signer.Verify($"{parts[0]}.{parts[1][..^2]}AA", 1, 7, Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    [InlineData("!!!.???")]
    public void AnythingThatIsNotAnApproval_IsSimplyNoApproval(string? token) => Assert.Null(_signer.Verify(token, 1, 7, Now));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("too-short")]
    public void WithoutAStrongSharedKey_TheServiceRefusesToStart(string? key) => Assert.Throws<InvalidOperationException>(() => new OnboardingApprovalSigner(key));

    private async Task<(HttpStatusCode Status, string Body)> Hire(string? approval, long issue = 7, long tenant = 1)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/employees/from-onboarding/{issue}")
        { Content = JsonContent.Create(new EmployeeRequest(101, "EMP101", "New", "Hire", new DateOnly(2026, 10, 1))) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", fixture.Token(100, tenant, "ADMIN", SecurityFixture.Permissions.Append("EMPLOYEE_CREATE").ToArray()));
        if (approval is not null) request.Headers.Add(OnboardingApprovalSigner.Header, approval);
        using var response = await fixture.Client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Identity_RefusesToHireWithoutAGenuineCurrentApprovalForThisCompanyAndRequest()
    {
        var expired = _signer.Sign(1, 7, "new.hire@company.com", null, DateTimeOffset.UtcNow.AddMinutes(-10));
        foreach (var (name, token, issue) in new (string, string?, long)[]
        {
            ("none", null, 7),
            ("garbage", "nonsense", 7),
            ("expired", expired, 7),
            ("another request", Genuine(issue: 8), 7),
            ("another company", Genuine(tenant: 2), 7),
            ("another key", new OnboardingApprovalSigner("some-other-key-that-is-long-enough-0123456789").Sign(1, 7, "x@y.co", null, DateTimeOffset.UtcNow), 7),
        })
        {
            var (status, body) = await Hire(token, issue);
            Assert.True(status == HttpStatusCode.Forbidden, $"{name}: expected 403 but got {(int)status}");
            Assert.Contains("ONBOARDING_APPROVAL_REQUIRED", body);
        }
    }

    [Fact]
    public async Task AGenuineApproval_GetsPastTheApprovalCheck()
    {
        
        var (_, body) = await Hire(Genuine());
        Assert.DoesNotContain("ONBOARDING_APPROVAL_REQUIRED", body);
    }

    [Fact]
    public void IdentityNoLongerKnowsHowToCallJira()
    {
        var identity = typeof(Persona.Api.Controllers.EmployeesController).Assembly;
        Assert.Null(identity.GetType("Persona.Api.Services.Employees.OnboardingApprovalClient"));
        Assert.DoesNotContain(identity.GetReferencedAssemblies(), a => a.Name!.StartsWith("Jira", StringComparison.Ordinal));
    }
}
