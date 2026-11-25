using System.Net;
using System.Net.Http.Json;
using AIAgent.Infrastructure.Tools;

namespace Backend.Authorization.Tests;






public sealed class ServiceRouteContractTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    public static TheoryData<string, string> Calls => new()
    {
        
        { "POST", JiraRoutes.Issues },
        { "GET", JiraRoutes.IssueSearch + "?Page=1&PageSize=1" },
        { "GET", JiraRoutes.Issue(1) },
        { "GET", JiraRoutes.IssueByKey("FLP-1") },
        { "PUT", JiraRoutes.Issue(1) },
        { "PUT", JiraRoutes.IssueAssignee(1) },
        { "PUT", JiraRoutes.IssueTransition(1) },
        { "POST", JiraRoutes.IssueComments(1) },
        { "GET", JiraRoutes.Projects },
        { "GET", JiraRoutes.ProjectsByUser(1) },
        { "GET", JiraRoutes.ProjectAllocations + "?userId=1" },
        { "GET", JiraRoutes.ProjectAssignmentMembers(1) },
        { "GET", JiraRoutes.Boards + "?projectId=1" },
        { "GET", JiraRoutes.BoardSprints(1) },
        { "GET", JiraRoutes.BoardBacklog(1) },
        { "GET", JiraRoutes.WorkLogs },
        { "POST", JiraRoutes.WorkLogs },
        { "GET", JiraRoutes.WorkLog(1) },
        { "PUT", JiraRoutes.WorkLog(1) },
        { "GET", JiraRoutes.MyTimesheet },
        { "GET", JiraRoutes.TeamTimesheets },
        { "POST", JiraRoutes.SubmitTimesheet },
        { "GET", JiraRoutes.TimesheetApprovers(1) },
        { "POST", JiraRoutes.TimesheetAction("approve") },
        { "POST", JiraRoutes.TimesheetAction("reject") },
        
        { "GET", IdentityRoutes.CurrentUser },
        { "GET", IdentityRoutes.Employees },
        { "GET", IdentityRoutes.Employee(1) },
        { "GET", IdentityRoutes.EmployeeSkills(1) },
        { "GET", IdentityRoutes.EmployeeCareerHistory(1) },
        { "GET", IdentityRoutes.EmployeeCompensation(1) },
        { "GET", IdentityRoutes.EmployeeSection(1, "education") },
        { "GET", IdentityRoutes.EmployeeSection(1, "certifications") },
        { "GET", IdentityRoutes.EmployeeSection(1, "experience") },
        { "GET", IdentityRoutes.EmployeeSection(1, "languages") },
        { "GET", IdentityRoutes.EmployeeReferences },
        { "GET", IdentityRoutes.CareerCatalog },
        { "GET", IdentityRoutes.ExportDetails },
        { "GET", IdentityRoutes.Me },
        { "PUT", IdentityRoutes.MyProfile },
        { "GET", IdentityRoutes.MySkills },
        { "POST", IdentityRoutes.MySkills },
        { "GET", IdentityRoutes.MySection("education") },
        { "GET", IdentityRoutes.MySection("certifications") },
        { "GET", IdentityRoutes.Absence + "?year=2026&page=1" },
        { "POST", IdentityRoutes.Absence },
        { "GET", IdentityRoutes.AbsencePolicy + "?year=2026" },
        { "POST", IdentityRoutes.AbsenceDecision(1) },
    };

    [Theory]
    [MemberData(nameof(Calls))]
    public async Task EveryRouteAthennaCalls_ExistsOnTheRealService(string method, string url)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT") request.Content = JsonContent.Create(new { });

        using var response = await fixture.Client.SendAsync(request);

        Assert.True(response.StatusCode != HttpStatusCode.NotFound, $"{method} {url} no longer exists (404).");
        Assert.True(response.StatusCode != HttpStatusCode.MethodNotAllowed, $"{method} {url} no longer accepts that method (405).");
    }

    [Fact]
    public async Task AnUnknownRoute_IsReportedAsMissing_SoTheCheckAboveCanFail()
    {
        using var response = await fixture.Client.GetAsync("/api/employees/athenna-renamed-this");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void TheToolsHoldNoUrlsOfTheirOwn()
    {
        var source = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../Services/AIAgent/AIAgent.Infrastructure"));
        Assert.True(Directory.Exists(source), $"Expected the AIAgent source at {source}");

        var offenders = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Path.GetFileName(f) != "ServiceRoutes.cs")
            .Where(f => File.ReadAllText(f).Contains("\"/api/", StringComparison.Ordinal) || File.ReadAllText(f).Contains("$\"/api/", StringComparison.Ordinal))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(offenders.Count == 0, "Write service URLs in ServiceRoutes.cs, not in tools: " + string.Join(", ", offenders));
    }
}
