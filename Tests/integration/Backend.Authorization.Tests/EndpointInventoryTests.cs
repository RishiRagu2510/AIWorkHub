using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;

namespace Backend.Authorization.Tests;

// Inventory supplied for the API audit. Registration checks complement (not replace)
// the authenticated workflow and response-contract tests in this project.
public sealed class EndpointInventoryTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    public static TheoryData<string, string> Routes => new()
    {
        { "POST", "/api/auth/password/login" },
        { "POST", "/api/auth/password/register" },
        { "POST", "/api/auth/password/verify" },
        { "POST", "/api/auth/google" },
        { "GET", "/api/auth/google/configuration" },
        { "GET", "/api/auth/me" },
        { "GET", "/api/auth/context" },
        { "POST", "/api/auth/token" },
        { "GET", "/api/auth/token-dev" },
        { "POST", "/api/auth/logout" },
        { "GET", "/api/auth/permission-test" },
        { "GET", "/api/users" },
        { "GET", "/api/users/{userId}" },
        { "GET", "/api/users/{userId}/assignment-eligibility" },
        { "GET", "/api/users/{id}/collaboration-eligibility" },
        { "GET", "/api/users/timesheet-approvers" },
        { "GET", "/api/tenants/me" },
        { "GET", "/api/tenants/{tenantId}" },
        { "GET", "/api/invitations" },
        { "POST", "/api/invitations" },
        { "DELETE", "/api/invitations/{id}" },
        { "GET", "/api/employees" },
        { "POST", "/api/employees" },
        { "GET", "/api/employees/{id}" },
        { "PUT", "/api/employees/{id}" },
        { "DELETE", "/api/employees/{id}" },
        { "GET", "/api/employees/me" },
        { "PUT", "/api/employees/me/profile" },
        { "GET", "/api/employees/org-chart" },
        { "GET", "/api/employees/manager-options" },
        { "GET", "/api/employees/login-users" },
        { "GET", "/api/employees/{id}/career-history" },
        { "POST", "/api/employees/{id}/photo" },
        { "GET", "/api/employees/{id}/photo/{image}" },
        { "GET", "/api/employees/absence" },
        { "POST", "/api/employees/absence" },
        { "GET", "/api/employees/absence/policy" },
        { "PUT", "/api/employees/absence/policy" },
        { "POST", "/api/employees/absence/{id}/decision" },
        { "GET", "/api/employees/career-catalog" },
        { "GET", "/api/employees/references" },
        { "POST", "/api/employees/references" },
        { "PUT", "/api/employees/references/{id}" },
        { "GET", "/api/employees/setup" },
        { "GET", "/api/employees/setup/users" },
        { "POST", "/api/employees/setup/users/{userId}/roles" },
        { "PUT", "/api/employees/setup/users/{userId}/roles" },
        { "POST", "/api/employees/setup/{kind}" },
        { "DELETE", "/api/employees/setup/{kind}" },
        { "PUT", "/api/employees/setup/{kind}/{id}" },
        { "DELETE", "/api/employees/setup/{kind}/{id}" },
        { "GET", "/api/projects" },
        { "POST", "/api/projects" },
        { "GET", "/api/projects/{projectId}" },
        { "PUT", "/api/projects/{projectId}" },
        { "DELETE", "/api/projects/{projectId}" },
        { "PUT", "/api/projects/{projectId}/parent" },
        { "GET", "/api/projects/{projectId}/subprojects" },
        { "POST", "/api/projects/{projectId}/subprojects" },
        { "GET", "/api/projects/by-user/{userId}" },
        { "GET", "/api/projects/context" },
        { "GET", "/api/projects/accounts" },
        { "POST", "/api/projects/accounts" },
        { "PUT", "/api/projects/accounts/{id}" },
        { "DELETE", "/api/projects/accounts/{id}" },
        { "GET", "/api/projects/{projectId}/members" },
        { "POST", "/api/projects/{projectId}/members" },
        { "DELETE", "/api/projects/{projectId}/members/{userId}" },
        { "GET", "/api/projects/{projectId}/members/effective" },
        { "GET", "/api/projects/{projectId}/assignment-members" },
        { "GET", "/api/projects/allocations" },
        { "POST", "/api/projects/allocations" },
        { "DELETE", "/api/projects/allocations/{id}" },
        { "GET", "/api/issues/search" },
        { "POST", "/api/issues" },
        { "GET", "/api/issues/{issueId}" },
        { "PUT", "/api/issues/{issueId}" },
        { "DELETE", "/api/issues/{issueId}" },
        { "GET", "/api/issues/key/{issueKey}" },
        { "GET", "/api/issues/project/{projectId}" },
        { "PUT", "/api/issues/{issueId}/assignee" },
        { "PUT", "/api/issues/{issueId}/transition" },
        { "GET", "/api/boards" },
        { "POST", "/api/boards" },
        { "GET", "/api/boards/{id}" },
        { "GET", "/api/boards/{boardId}/backlog" },
        { "GET", "/api/boards/{boardId}/sprints" },
        { "POST", "/api/boards/{boardId}/sprints" },
        { "PUT", "/api/boards/{boardId}/issues/{issueId}/placement" },
        { "POST", "/api/sprints/{id}/start" },
        { "POST", "/api/sprints/{id}/complete" },
        { "GET", "/api/projects/{projectId}/components" },
        { "POST", "/api/projects/{projectId}/components" },
        { "PUT", "/api/projects/{projectId}/components/{id}" },
        { "GET", "/api/projects/{projectId}/versions" },
        { "POST", "/api/projects/{projectId}/versions" },
        { "PUT", "/api/projects/{projectId}/versions/{id}" },
        { "GET", "/api/projects/{projectId}/labels" },
        { "GET", "/api/projects/{id}/workflow" },
        { "PUT", "/api/projects/{id}/workflow" },
        { "GET", "/api/issues/{issueId}/comments" },
        { "POST", "/api/issues/{issueId}/comments" },
        { "PUT", "/api/issues/{issueId}/comments/{commentId}" },
        { "DELETE", "/api/issues/{issueId}/comments/{commentId}" },
        { "GET", "/api/issues/{issueId}/attachments" },
        { "POST", "/api/issues/{issueId}/attachments" },
        { "GET", "/api/issues/{issueId}/attachments/{id}/download" },
        { "DELETE", "/api/issues/{issueId}/attachments/{id}" },
        { "GET", "/api/issues/{id}/watchers" },
        { "PUT", "/api/issues/{id}/watchers/me" },
        { "DELETE", "/api/issues/{id}/watchers/me" },
        { "GET", "/api/issues/{id}/activity" },
        { "GET", "/api/activity" },
        { "GET", "/api/worklogs" },
        { "POST", "/api/worklogs" },
        { "GET", "/api/worklogs/{id}" },
        { "PUT", "/api/worklogs/{id}" },
        { "DELETE", "/api/worklogs/{id}" },
        { "GET", "/api/timesheets/me" },
        { "GET", "/api/timesheets/summary" },
        { "POST", "/api/timesheets/submit" },
        { "POST", "/api/timesheets/{id}/submit" },
        { "GET", "/api/timesheets/team" },
        { "GET", "/api/timesheets/report" },
        { "GET", "/api/timesheets/hierarchy" },
        { "GET", "/api/timesheets/{periodId}/approvers" },
        { "POST", "/api/timesheets/approve" },
        { "POST", "/api/timesheets/reject" },
        { "POST", "/api/timesheets/{id}/approve" },
        { "POST", "/api/timesheets/{id}/reject" },
        { "GET", "/api/preferences" },
        { "PUT", "/api/preferences/{key}" },
        { "GET", "/api/notifications" },
        { "GET", "/api/notifications/unread-count" },
        { "PUT", "/api/notifications/{id}/read" },
        { "POST", "/api/notifications/read-all" },
        { "GET", "/api/audit/logs" },
        { "GET", "/api/employees/{id}/skills" },
        { "POST", "/api/employees/{id}/skills" },
        { "PUT", "/api/employees/{id}/skills/{entryId}" },
        { "DELETE", "/api/employees/{id}/skills/{entryId}" },
        { "GET", "/api/employees/{id}/education" },
        { "POST", "/api/employees/{id}/education" },
        { "PUT", "/api/employees/{id}/education/{entryId}" },
        { "DELETE", "/api/employees/{id}/education/{entryId}" },
        { "GET", "/api/employees/{id}/certifications" },
        { "POST", "/api/employees/{id}/certifications" },
        { "PUT", "/api/employees/{id}/certifications/{entryId}" },
        { "DELETE", "/api/employees/{id}/certifications/{entryId}" },
        { "GET", "/api/employees/{id}/experience" },
        { "POST", "/api/employees/{id}/experience" },
        { "PUT", "/api/employees/{id}/experience/{entryId}" },
        { "DELETE", "/api/employees/{id}/experience/{entryId}" },
        { "GET", "/api/employees/{id}/languages" },
        { "POST", "/api/employees/{id}/languages" },
        { "PUT", "/api/employees/{id}/languages/{entryId}" },
        { "DELETE", "/api/employees/{id}/languages/{entryId}" },
        { "GET", "/api/employees/{id}/compensation" },
        { "POST", "/api/employees/{id}/compensation" },
        { "PUT", "/api/employees/{id}/compensation/{entryId}" },
        { "DELETE", "/api/employees/{id}/compensation/{entryId}" },
    };

    private IEnumerable<RouteEndpoint> Endpoints => ((IEndpointRouteBuilder)fixture.App)
        .DataSources.SelectMany(source => source.Endpoints).OfType<RouteEndpoint>();

    private static string Normalize(string route) =>
        Regex.Replace(route.Trim('/'), @"\{[^}]+\}", "{}").ToLowerInvariant();

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ListedRouteIsRegisteredAndProtectedWhenRequired(string method, string path)
    {
        var endpoint = Assert.Single(Endpoints, e =>
            Normalize(e.RoutePattern.RawText!) == Normalize(path) &&
            e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

        if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            !endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any()) return;

        // Use values satisfying route constraints so a 404 cannot conceal an auth regression.
        var url = endpoint.RoutePattern.RawText!;
        foreach (var parameter in endpoint.RoutePattern.Parameters)
        {
            var value = parameter.ParameterPolicies.Any(p => p.Content == "guid")
                ? "11111111-1111-1111-1111-111111111111" : "1";
            url = Regex.Replace(url, @"\{" + parameter.Name + @"(?::[^}]+)?\}", value);
        }
        using var request = new HttpRequestMessage(new HttpMethod(method), "/" + url.TrimStart('/'));
        if (method == "POST" && (path.EndsWith("/photo") || path.EndsWith("/attachments")))
            request.Content = new MultipartFormDataContent();
        else if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });
        using var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/hubs/notifications")]
    [InlineData("/hubs/issues")]
    [InlineData("/hubs/boards/1")]
    public async Task HubsRejectAnonymousNegotiation(string path)
    {
        using var response = await fixture.Client.PostAsync(path + "/negotiate?negotiateVersion=1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
