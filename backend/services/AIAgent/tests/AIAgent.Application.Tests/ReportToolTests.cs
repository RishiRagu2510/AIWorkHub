using System.Net;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;

public class ExportIssuesReportToolTests
{
    private static DateOnly Today => FakeChronos.Today;

    private static string Issue(long id, string key, string summary, string status, string priority, long? assignee, string? due = null, long? points = null, int estimate = 0, int logged = 0, int remaining = 0, string type = "TASK", long reporter = 11) =>
        $"{{\"id\":{id},\"issueKey\":\"{key}\",\"issueNumber\":{key[(key.LastIndexOf('-') + 1)..]},\"summary\":\"{summary}\",\"issueType\":\"{type}\",\"statusCode\":\"{status}\",\"priority\":\"{priority}\"," +
        $"\"assigneeUserId\":{(assignee.HasValue ? assignee.ToString() : "null")},\"reporterUserId\":{reporter},\"dueDate\":{(due == null ? "null" : $"\"{due}\"")},\"storyPoints\":{(points.HasValue ? points.ToString() : "null")}," +
        $"\"originalEstimateMinutes\":{estimate},\"loggedTimeMinutes\":{logged},\"remainingEstimateMinutes\":{remaining}}}";

    private static string Page(IEnumerable<string> issues, int? total = null)
    {
        var list = issues.ToList();
        return $"{{\"items\":[{string.Join(",", list)}],\"totalCount\":{total ?? list.Count},\"page\":1,\"pageSize\":100,\"totalPages\":1}}";
    }

    private const string People = """
        {"items":[
          {"id":1,"userId":11,"firstName":"Priya","lastName":"Nair","employeeCode":"E1","workEmail":"priya@x.test"},
          {"id":2,"userId":12,"firstName":"Asha","lastName":"Kumar","employeeCode":"E2","workEmail":"asha@x.test"},
          {"id":3,"userId":13,"firstName":"Asha","lastName":"Menon","employeeCode":"E3","workEmail":"asham@x.test"}],
         "totalCount":3,"page":1,"pageSize":100,"totalPages":1}
        """;

    private static Dictionary<string, object?> Args(string json, long caller = 11, long? owner = null, string roles = "MANAGER")
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>(json)!;
        args[IssueVisibility.CallerUserIdArgument] = caller;
        args[IssueVisibility.CallerRolesArgument] = roles;
        if (owner.HasValue) args[IssueVisibility.OwnerArgument] = owner.Value;
        return args;
    }

    private static IEnumerable<string> Standard()
    {
        var late = Today.AddDays(-3).ToString("yyyy-MM-dd");
        var soon = Today.AddDays(4).ToString("yyyy-MM-dd");
        return
        [
            Issue(1, "FLP-1", "Fix payroll export", "IN_PROGRESS", "HIGH", 12, late, 5, 480, 120, 360, "BUG"),
            Issue(2, "FLP-2", "Add audit trail", "TODO", "MEDIUM", 11, soon, 3, 240, 0, 240),
            Issue(3, "FLP-3", "Login page", "DONE", "LOW", 12, late, 2, 120, 150, 0),
            Issue(4, "FLP-4", "No owner yet", "TODO", "LOW", null)
        ];
    }

    private static (RecordingHandler Handler, FakeHttpClientFactory Factory) Services(IEnumerable<string>? issues = null, int? total = null) =>
        new Routes()
            .Get("/api/projects/by-user", """[{"id":7,"key":"FLP","displayName":"Flagship"}]""")
            .Get("/api/projects", """[{"id":7,"key":"FLP","displayName":"Flagship"},{"id":8,"key":"OPS","displayName":"Operations"},{"id":9,"key":"OPX","displayName":"Operations Extra"}]""")
            .Get("/api/employees?", People)
            .Get("/api/issues/search", Page(issues ?? Standard(), total))
            .Build();

    private static async Task<(ToolResult Result, RecordingHandler Handler)> Run(string json, long caller = 11, long? owner = null, string roles = "MANAGER", IEnumerable<string>? issues = null, int? total = null)
    {
        var (handler, factory) = Services(issues, total);
        var result = await new ExportIssuesReportTool(factory, new HttpContextAccessor()).ExecuteAsync(Args(json, caller, owner, roles));
        return (result, handler);
    }

    private static JsonElement Sheet(ToolResult result)
    {
        using var data = JsonDocument.Parse((string)result.Data!);
        var content = data.RootElement.GetProperty("file").GetProperty("content").GetString()!;
        return JsonDocument.Parse(content).RootElement.Clone();
    }

    private static string SearchUrl(RecordingHandler handler) => handler.Calls.Select(c => c.Request.RequestUri!.PathAndQuery).Single(u => u.StartsWith("/api/issues/search"));

    [Fact]
    public async Task ABuiltReport_HasOneRowPerIssue_WithNamesAndHoursWorkedOutHere()
    {
        var (result, _) = await Run("{}");

        Assert.True(result.Success, result.Error);
        using var data = JsonDocument.Parse((string)result.Data!);
        Assert.Equal(4, data.RootElement.GetProperty("count").GetInt32());
        Assert.Equal(3, data.RootElement.GetProperty("open").GetInt32());
        Assert.Equal(1, data.RootElement.GetProperty("done").GetInt32());
        Assert.Equal(1, data.RootElement.GetProperty("overdue").GetInt32());   
        Assert.Equal(10, data.RootElement.GetProperty("storyPoints").GetInt64());
        Assert.Equal(4.5, data.RootElement.GetProperty("loggedHours").GetDouble());

        var file = data.RootElement.GetProperty("file");
        Assert.Matches(@"^Issues_Report_\d{4}-\d{2}-\d{2}\.xlsx$", file.GetProperty("fileName").GetString());
        Assert.Equal("table", file.GetProperty("format").GetString());
        Assert.Contains("spreadsheetml", file.GetProperty("mimeType").GetString());

        var sheet = Sheet(result);
        var headers = sheet.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("header").GetString()!).ToArray();
        Assert.Equal(["Key", "Summary", "Type", "Status", "Priority", "Assignee", "Reporter", "Due date", "Overdue", "Story points", "Estimate (h)", "Logged (h)", "Remaining (h)"], headers);
        var rows = sheet.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(4, rows.Count);
        Assert.Equal("FLP-1", rows[0][0].GetString());
        Assert.Equal("Asha Kumar", rows[0][5].GetString());       
        Assert.Equal("Priya Nair", rows[0][6].GetString());
        Assert.Equal("IN PROGRESS", rows[0][3].GetString());   
        Assert.Equal("3 days late", rows[0][8].GetString());
        Assert.Equal(8.0, rows[0][10].GetDouble());               
        Assert.Equal("", rows[2][8].GetString());                 
        Assert.Equal("Unassigned", rows[3][5].GetString());
        Assert.Equal(["Issues", "Open", "Overdue", "Logged hours"], sheet.GetProperty("summary").EnumerateArray().Select(s => s.GetProperty("label").GetString()!).ToArray());
        Assert.Equal("Issues", sheet.GetProperty("unit").GetString());
    }

    [Fact]
    public async Task TheModelIsTold_TheTotals_NotTheRows()
    {
        var (result, _) = await Run("{}");

        var model = System.Text.Json.JsonSerializer.Serialize(ToolPayload.ForModel(ToolPayload.Unpack(result.Data)));
        Assert.Contains("\"count\":4", model);
        Assert.Contains("contentWithheldFromAssistant", model);
        Assert.DoesNotContain("Fix payroll export", model);
        Assert.DoesNotContain("Asha Kumar", model);
    }

    [Fact]
    public async Task ADeveloper_GetsOnlyTheirOwnIssues_WhoeverIsAsked()
    {
        var (result, handler) = await Run("{}", caller: 12, owner: 12, roles: "DEVELOPER");

        var rows = Sheet(result).GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(["FLP-1", "FLP-3"], rows.Select(r => r[0].GetString()!).ToArray());   
        Assert.Contains("AssigneeUserId=12", SearchUrl(handler));
    }

    [Fact]
    public async Task ADeveloper_CannotExportSomeoneElsesIssues()
    {
        var (result, handler) = await Run("""{"assignee":"Priya"}""", caller: 12, owner: 12, roles: "DEVELOPER");

        Assert.False(result.Success);
        Assert.Contains("only export issues assigned to you", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.RequestUri!.AbsolutePath.StartsWith("/api/issues/search"));
    }

    [Fact]
    public async Task ADeveloper_MayAskForMe()
    {
        var (result, _) = await Run("""{"assignee":"me"}""", caller: 12, owner: 12, roles: "DEVELOPER");

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task AManager_CanNameAPerson_AndTheNameIsLookedUp()
    {
        var (result, handler) = await Run("""{"assignee":"Priya"}""");

        Assert.True(result.Success, result.Error);
        Assert.Contains("AssigneeUserId=11", SearchUrl(handler));
        using var data = JsonDocument.Parse((string)result.Data!);
        Assert.Contains("assigned to Priya Nair", data.RootElement.GetProperty("filters").GetString());
    }

    [Fact]
    public async Task ANameThatFitsSeveralPeople_IsRefusedWithTheCandidates()
    {
        var (result, handler) = await Run("""{"assignee":"Asha"}""");

        Assert.False(result.Success);
        Assert.Contains("Asha Kumar", result.Error);
        Assert.Contains("Asha Menon", result.Error);
        Assert.DoesNotContain(handler.Calls, c => c.Request.RequestUri!.AbsolutePath.StartsWith("/api/issues/search"));
    }

    [Fact]
    public async Task ANameThatFitsNobody_IsRefused()
    {
        var (result, _) = await Run("""{"assignee":"Zed"}""");

        Assert.False(result.Success);
        Assert.Contains("No one is called 'Zed'", result.Error);
    }

    [Fact]
    public async Task AProjectNamedByKey_NarrowsTheSearch_AndNamesTheFile()
    {
        var (result, handler) = await Run("""{"project":"flp"}""");

        Assert.True(result.Success, result.Error);
        Assert.Contains("ProjectId=7", SearchUrl(handler));
        using var data = JsonDocument.Parse((string)result.Data!);
        Assert.Matches(@"^Issues_Report_FLP_", data.RootElement.GetProperty("file").GetProperty("fileName").GetString());
        Assert.Contains("Flagship", Sheet(result).GetProperty("title").GetString());
    }

    [Fact]
    public async Task AProjectThatFitsSeveral_OrNone_IsRefused()
    {
        var several = (await Run("""{"project":"Oper"}""")).Result;
        var none = (await Run("""{"project":"Nowhere"}""")).Result;

        Assert.False(several.Success);
        Assert.Contains("fits more than one project", several.Error);
        Assert.False(none.Success);
        Assert.Contains("No project matches 'Nowhere'", none.Error);
    }

    [Fact]
    public async Task Filters_AreSentToTheSearch()
    {
        var (_, handler) = await Run("""{"statusCode":"in_progress","priority":"high","query":"payroll","dueFrom":"2026-11-01","dueTo":"2026-11-30"}""");

        var url = SearchUrl(handler);
        Assert.Contains("Status=IN_PROGRESS", url);
        Assert.Contains("Priority=HIGH", url);
        Assert.Contains("Text=payroll", url);
        Assert.Contains("DueFrom=2026-11-01", url);
        Assert.Contains("DueTo=2026-11-30", url);
    }

    [Fact]
    public async Task OpenOnly_LeavesOutDoneIssues_AndOverdueOnlyKeepsTheLateOpenOnes()
    {
        var open = Sheet((await Run("""{"openOnly":true}""")).Result).GetProperty("rows").EnumerateArray().Select(r => r[0].GetString()!).ToArray();
        var late = Sheet((await Run("""{"overdueOnly":true}""")).Result).GetProperty("rows").EnumerateArray().Select(r => r[0].GetString()!).ToArray();

        Assert.Equal(["FLP-1", "FLP-2", "FLP-4"], open);
        Assert.Equal(["FLP-1"], late);
    }

    [Fact]
    public async Task WhenNothingMatches_NoFileIsPrepared()
    {
        var (result, _) = await Run("{}", issues: []);

        Assert.True(result.Success);
        using var data = JsonDocument.Parse((string)result.Data!);
        Assert.Equal(0, data.RootElement.GetProperty("count").GetInt32());
        Assert.False(data.RootElement.TryGetProperty("file", out _));
        Assert.Contains("no file was prepared", data.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public async Task WhenMoreMatchThanWereRead_SaysTheReportIsIncomplete()
    {
        var (result, _) = await Run("{}", total: 750);

        using var data = JsonDocument.Parse((string)result.Data!);
        Assert.Contains("not complete", data.RootElement.GetProperty("note").GetString());
    }

    [Theory]
    [InlineData("""{"dueFrom":"01/11/2026"}""", "dueFrom must be written as YYYY-MM-DD")]
    [InlineData("""{"dueTo":"tomorrow"}""", "dueTo must be written as YYYY-MM-DD")]
    [InlineData("""{"dueFrom":"2026-12-01","dueTo":"2026-11-01"}""", "dueFrom is after dueTo")]
    public async Task BadDates_AreRefused_BeforeAnythingIsRead(string json, string message)
    {
        var (result, handler) = await Run(json);

        Assert.False(result.Success);
        Assert.Contains(message, result.Error);
        Assert.Empty(handler.Calls);
    }

    [Fact]
    public async Task WhenTheIssueServiceFails_SaysSo_AndPreparesNothing()
    {
        var (handler, factory) = new Routes()
            .Get("/api/employees?", People)
            .Get("/api/issues/search", """{"message":"down"}""", HttpStatusCode.ServiceUnavailable)
            .Build();

        var result = await new ExportIssuesReportTool(factory, new HttpContextAccessor()).ExecuteAsync(Args("{}"));

        Assert.False(result.Success);
        Assert.Contains("I could not read the issues", result.Error);
    }

    [Fact]
    public void ItIsClassifiedAsARead_AndNeedsOnlyTheIssueViewPermission()
    {
        var tool = new ExportIssuesReportTool(new FakeHttpClientFactory(new RecordingHandler(_ => Http.Json("{}", HttpStatusCode.OK))), new HttpContextAccessor());

        Assert.Equal("ISSUE_VIEW", tool.RequiredPermission);
        Assert.Equal(ToolActivityKind.Read, ToolActivity.Classify(tool.Name));
    }
}

public class ExportWorkLogReportToolTests
{
    private static async Task<(ToolResult Result, FakeChronos Chronos)> Run(FakeChronos chronos, params (string Key, object? Value)[] pairs) =>
        (await chronos.Tool<ExportWorkLogReportTool>().ExecuteAsync(ChronosArgs.Of(pairs)), chronos);

    private static JsonElement Sheet(ToolResult result)
    {
        var content = ChronosArgs.Data(result).GetProperty("file").GetProperty("content").GetString()!;
        return JsonDocument.Parse(content).RootElement.Clone();
    }

    private static FakeChronos WithLogs()
    {
        var chronos = new FakeChronos();
        chronos.Logs =
        [
            FakeChronos.Log(2, "2026-10-14", 150, "Review", "SUBMITTED", billable: false),
            FakeChronos.Log(1, "2026-10-13", 90, "Fix login", "APPROVED"),
            FakeChronos.Log(3, "2026-10-14", 60, "Other person", userId: 9),
            FakeChronos.Log(4, "2026-10-20", 240, null, "DRAFT")
        ];
        return chronos;
    }

    [Fact]
    public async Task OneRowPerEntry_OldestFirst_WithTotalsWorkedOutHere()
    {
        var (result, _) = await Run(WithLogs(), ("fromDate", "2026-10-01"), ("toDate", "2026-10-31"));

        Assert.True(result.Success, result.Error);
        var data = ChronosArgs.Data(result);
        Assert.Equal(3, data.GetProperty("count").GetInt32());             
        Assert.Equal(8.0, data.GetProperty("totalHours").GetDouble());      
        Assert.Equal(5.5, data.GetProperty("billableHours").GetDouble());
        Assert.Equal(3, data.GetProperty("daysLogged").GetInt32());
        Assert.Equal("Time_Report_2026-10-01_to_2026-10-31.xlsx", data.GetProperty("file").GetProperty("fileName").GetString());

        var sheet = Sheet(result);
        var rows = sheet.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(["2026-10-13", "2026-10-14", "2026-10-20"], rows.Select(r => r[0].GetString()!).ToArray());
        Assert.Equal("Tue", rows[0][1].GetString());
        Assert.Equal("FLP-7", rows[0][2].GetString());
        Assert.Equal("Flagship", rows[0][4].GetString());
        Assert.Equal(1.5, rows[0][5].GetDouble());
        Assert.Equal("Yes", rows[0][6].GetString());
        Assert.Equal("No", rows[1][6].GetString());
        Assert.Equal("Approved", rows[0][8].GetString());
        Assert.Equal("", rows[2][7].GetString());                         
        Assert.Equal("Entries", sheet.GetProperty("unit").GetString());
        Assert.Equal(["Total hours", "Billable hours", "Days logged", "Entries"], sheet.GetProperty("summary").EnumerateArray().Select(s => s.GetProperty("label").GetString()!).ToArray());
        Assert.Contains("hours", sheet.GetProperty("columns")[5].GetProperty("header").GetString()!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task WithNoDates_CoversThisMonthSoFar_ForTheCallerOnly()
    {
        var (_, chronos) = await Run(new FakeChronos());

        var url = Assert.Single(chronos.Urls);
        Assert.Contains($"From={new DateOnly(FakeChronos.Today.Year, FakeChronos.Today.Month, 1):yyyy-MM-dd}", url);
        Assert.Contains($"To={FakeChronos.Today:yyyy-MM-dd}", url);
        Assert.Contains("UserId=5", url);
    }

    [Fact]
    public async Task AnIssueCanNarrowTheReport()
    {
        var chronos = WithLogs();

        await Run(chronos, ("fromDate", "2026-10-01"), ("toDate", "2026-10-31"), ("issueIdOrKey", "FLP-7"));

        Assert.Contains("IssueId=42", chronos.Urls.Single(u => u.StartsWith("/api/worklogs")));
    }

    [Fact]
    public async Task WhenNothingIsLogged_NoFileIsPrepared()
    {
        var (result, _) = await Run(new FakeChronos(), ("fromDate", "2026-10-01"), ("toDate", "2026-10-31"));

        var data = ChronosArgs.Data(result);
        Assert.Equal(0, data.GetProperty("count").GetInt32());
        Assert.False(data.TryGetProperty("file", out _));
        Assert.Contains("no file was prepared", data.GetProperty("note").GetString());
    }

    [Theory]
    [InlineData("2026-13-01", "2026-10-31", "fromDate must be written as YYYY-MM-DD")]
    [InlineData("2026-10-01", "soon", "toDate must be written as YYYY-MM-DD")]
    [InlineData("2026-11-01", "2026-10-01", "fromDate is after toDate")]
    [InlineData("2024-01-01", "2026-10-01", "at most a year")]
    public async Task BadRanges_AreRefused_BeforeAnythingIsRead(string from, string to, string message)
    {
        var (result, chronos) = await Run(new FakeChronos(), ("fromDate", from), ("toDate", to));

        Assert.False(result.Success);
        Assert.Contains(message, result.Error);
        Assert.Empty(chronos.Urls);
    }

    [Fact]
    public async Task WithoutKnowingWhoTheCallerIs_NothingIsRead()
    {
        var chronos = new FakeChronos();
        var args = new Dictionary<string, object?> { [IssueVisibility.CallerRolesArgument] = "DEVELOPER" };

        var result = await chronos.Tool<ExportWorkLogReportTool>().ExecuteAsync(args);

        Assert.False(result.Success);
        Assert.Contains("can't tell who you are", result.Error);
        Assert.Empty(chronos.Urls);
    }

    [Fact]
    public void ItIsClassifiedAsARead_AndNeedsOnlyTheWorkLogViewPermission()
    {
        var tool = new FakeChronos().Tool<ExportWorkLogReportTool>();

        Assert.Equal("WORKLOG_VIEW", tool.RequiredPermission);
        Assert.Equal(ToolActivityKind.Read, ToolActivity.Classify(tool.Name));
    }
}
