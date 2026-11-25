using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;


internal sealed class FakeChronos
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
    public static DateOnly MondayOf(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    public List<string> Logs = [];
    public string PeriodsJson = "[]";
    public string ApproversJson = """[{"id":21,"displayName":"Meera Iyer","roles":["PROJECT_MANAGER"]},{"id":22,"displayName":"Dev Patel","roles":["MANAGER"]}]""";
    public HttpStatusCode PutStatus = HttpStatusCode.OK;
    public HttpStatusCode SubmitStatus = HttpStatusCode.OK;
    public string? SentBody;

    public static string Log(long id, string date, int minutes, string? note = "Fixed it", string status = "DRAFT", long userId = 5, string? start = null, bool billable = true, long issueId = 42, string key = "FLP-7", int version = 2) =>
        $"{{\"id\":{id},\"userId\":{userId},\"issueId\":{issueId},\"workDate\":\"{date}\",\"durationMinutes\":{minutes},\"description\":{(note == null ? "null" : JsonSerializer.Serialize(note))},\"billable\":{(billable ? "true" : "false")},\"issueKey\":\"{key}\",\"issueSummary\":\"Fix login\",\"projectName\":\"Flagship\",\"periodStatus\":\"{status}\",\"startTime\":{(start == null ? "null" : $"\"{start}\"")},\"version\":{version}}}";

    public static string Period(DateOnly monday, string status = "DRAFT", long totalMinutes = 2400, int version = 4, string? submittedAt = null) =>
        $"{{\"id\":77,\"userId\":5,\"periodStart\":\"{monday:yyyy-MM-dd}\",\"periodEnd\":\"{monday.AddDays(6):yyyy-MM-dd}\",\"status\":\"{status}\",\"version\":{version},\"totalMinutes\":{totalMinutes},\"submittedAt\":{(submittedAt == null ? "null" : $"\"{submittedAt}\"")}}}";

    public RecordingHandler Handler { get; }

    public FakeChronos()
    {
        Handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Put && path.StartsWith("/api/worklogs/"))
            {
                SentBody = Handler!.Calls.Last().Body;
                if (PutStatus != HttpStatusCode.OK) return Http.Json("""{"message":"Submitted or approved timesheets cannot be changed."}""", PutStatus);
                var id = long.Parse(path[(path.LastIndexOf('/') + 1)..]);
                var existing = JsonNode.Parse(Logs.First(l => JsonNode.Parse(l)!["id"]!.GetValue<long>() == id))!.AsObject();
                foreach (var (key, value) in JsonNode.Parse(SentBody!)!.AsObject()) existing[key] = value?.DeepClone();
                return Http.Json(existing.ToJsonString());
            }
            if (request.Method == HttpMethod.Post && path == "/api/timesheets/submit")
            {
                SentBody = Handler!.Calls.Last().Body;
                if (SubmitStatus != HttpStatusCode.OK) return Http.Json("""{"message":"The selected approver cannot access every issue in this period."}""", SubmitStatus);
                var period = JsonNode.Parse(JsonNode.Parse(PeriodsJson)!.AsArray()[0]!.ToJsonString())!.AsObject();
                period["status"] = "SUBMITTED";
                period["submittedAt"] = "2026-11-13T10:00:00+00:00";
                return Http.Json(period.ToJsonString());
            }
            if (path == "/api/worklogs") return Http.Json($"{{\"items\":[{string.Join(",", Logs)}],\"totalCount\":{Logs.Count},\"page\":1,\"pageSize\":100,\"totalPages\":1}}");
            if (path.StartsWith("/api/worklogs/"))
            {
                var id = path[(path.LastIndexOf('/') + 1)..];
                var hit = Logs.FirstOrDefault(l => JsonNode.Parse(l)!["id"]!.ToString() == id);
                return hit == null ? Http.Json("{}", HttpStatusCode.NotFound) : Http.Json(hit);
            }
            if (path == "/api/timesheets/me") return Http.Json($"{{\"periods\":{PeriodsJson},\"dailyTotals\":[]}}");
            if (path.EndsWith("/approvers")) return Http.Json(ApproversJson);
            if (path.StartsWith("/api/issues/key/")) return Http.Json("{\"id\":42}");
            return Http.Json("{}", HttpStatusCode.NotFound);
        });
    }

    public IEnumerable<string> Urls => Handler.Calls.Select(c => c.Request.RequestUri!.PathAndQuery);
    public IEnumerable<(HttpRequestMessage Request, string? Body)> Writes => Handler.Calls.Where(c => c.Request.Method != HttpMethod.Get);

    public T Tool<T>() where T : class => (T)Activator.CreateInstance(typeof(T), new FakeHttpClientFactory(Handler), new HttpContextAccessor())!;
}

internal static class ChronosArgs
{
    public static Dictionary<string, object?> Of(params (string Key, object? Value)[] pairs)
    {
        var args = new Dictionary<string, object?> { [IssueVisibility.CallerUserIdArgument] = 5L, [IssueVisibility.CallerRolesArgument] = "DEVELOPER" };
        foreach (var (key, value) in pairs) args[key] = value;
        return args;
    }

    public static JsonElement Data(ToolResult result) => JsonDocument.Parse(result.Data!.ToString()!).RootElement;
}

public class GetWorkLogsToolTests
{
    [Fact]
    public async Task WithNoDates_ReadsThisWeekSoFar_ForTheCallerOnly()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(1, "2026-11-10", 90)];

        var result = await chronos.Tool<GetWorkLogsTool>().ExecuteAsync(ChronosArgs.Of());

        Assert.True(result.Success, result.Error);
        var url = Assert.Single(chronos.Urls);
        Assert.Contains($"From={FakeChronos.MondayOf(FakeChronos.Today):yyyy-MM-dd}", url);
        Assert.Contains($"To={FakeChronos.Today:yyyy-MM-dd}", url);
        Assert.Contains("UserId=5", url);
    }

    [Fact]
    public async Task ListsNewestFirst_WithTotals_AndShowsWhichWeeksAreLocked()
    {
        var chronos = new FakeChronos();
        chronos.Logs =
        [
            FakeChronos.Log(1, "2026-11-09", 60, "Mon work", status: "SUBMITTED"),
            FakeChronos.Log(2, "2026-11-11", 150, "Wed work"),
            FakeChronos.Log(3, "2026-11-11", 30, "Wed more"),
        ];

        var data = ChronosArgs.Data(await chronos.Tool<GetWorkLogsTool>().ExecuteAsync(ChronosArgs.Of(("fromDate", "2026-11-09"), ("toDate", "2026-11-15"))));

        Assert.Equal(3, data.GetProperty("count").GetInt32());
        Assert.Equal(4.0, data.GetProperty("totalHours").GetDouble());
        var logs = data.GetProperty("logs").EnumerateArray().ToList();
        Assert.Equal([3L, 2L, 1L], logs.Select(l => l.GetProperty("workLogId").GetInt64()).ToArray());
        Assert.True(logs[0].GetProperty("canEdit").GetBoolean());
        Assert.False(logs[2].GetProperty("canEdit").GetBoolean());
        Assert.Equal("Mon work", logs[2].GetProperty("note").GetString());
    }

    [Fact]
    public async Task AnotherPersonsEntries_AreNeverShown()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(1, "2026-11-10", 60), FakeChronos.Log(2, "2026-11-10", 480, userId: 9)];

        var data = ChronosArgs.Data(await chronos.Tool<GetWorkLogsTool>().ExecuteAsync(ChronosArgs.Of()));

        Assert.Equal(1, data.GetProperty("count").GetInt32());
        Assert.Equal(1.0, data.GetProperty("totalHours").GetDouble());
    }

    [Fact]
    public async Task OneDateGiven_MeansThatSingleDay()
    {
        var chronos = new FakeChronos();

        await chronos.Tool<GetWorkLogsTool>().ExecuteAsync(ChronosArgs.Of(("fromDate", "2026-11-10")));

        var url = Assert.Single(chronos.Urls);
        Assert.Contains("From=2026-11-10", url);
        Assert.Contains("To=2026-11-10", url);
    }

    [Fact]
    public async Task NothingLogged_SaysSo()
    {
        var chronos = new FakeChronos();

        var data = ChronosArgs.Data(await chronos.Tool<GetWorkLogsTool>().ExecuteAsync(ChronosArgs.Of()));

        Assert.Equal(0, data.GetProperty("count").GetInt32());
        Assert.Contains("Nothing is logged", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task FiltersToOneIssue()
    {
        var chronos = new FakeChronos();

        await chronos.Tool<GetWorkLogsTool>().ExecuteAsync(ChronosArgs.Of(("issueIdOrKey", "FLP-7")));

        Assert.Contains(chronos.Urls, u => u.StartsWith("/api/worklogs") && u.Contains("IssueId=42"));
    }

    [Theory]
    [InlineData("not-a-date", null)]
    [InlineData("2026-11-12", "2026-11-01")]
    public async Task BadOrBackwardsDates_AreRefused_WithoutCallingTheService(string from, string? to)
    {
        var chronos = new FakeChronos();

        var result = await chronos.Tool<GetWorkLogsTool>().ExecuteAsync(ChronosArgs.Of(("fromDate", from), ("toDate", to)));

        Assert.False(result.Success);
        Assert.Empty(chronos.Urls);
    }

    [Fact]
    public async Task WithoutAKnownCaller_ItRefuses()
    {
        var chronos = new FakeChronos();

        var result = await chronos.Tool<GetWorkLogsTool>().ExecuteAsync(new Dictionary<string, object?>());

        Assert.False(result.Success);
        Assert.Contains("sign in", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(chronos.Urls);
    }
}

public class UpdateWorkLogToolTests
{
    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs) => ChronosArgs.Of(pairs);

    [Fact]
    public async Task ChangesTheTime_KeepsEverythingElse_AndSendsTheVersion()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60, "Fixed it", start: "09:00:00", version: 6)];

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L), ("durationMinutes", 180L)));

        Assert.True(result.Success, result.Error);
        var put = Assert.Single(chronos.Writes);
        Assert.Equal("/api/worklogs/11", put.Request.RequestUri!.AbsolutePath);
        Assert.Equal("\"6\"", put.Request.Headers.IfMatch.Single().Tag);

        var sent = JsonDocument.Parse(put.Body!).RootElement;
        Assert.Equal(42, sent.GetProperty("issueId").GetInt64());
        Assert.Equal("2026-11-10", sent.GetProperty("workDate").GetString());
        Assert.Equal(180, sent.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("Fixed it", sent.GetProperty("description").GetString());
        Assert.True(sent.GetProperty("billable").GetBoolean());
        Assert.Equal("09:00:00", sent.GetProperty("startTime").GetString());
        Assert.Equal("12:00:00", sent.GetProperty("endTime").GetString());

        var data = ChronosArgs.Data(result);
        Assert.True(data.GetProperty("updated").GetBoolean());
        Assert.Equal("FLP-7", data.GetProperty("issueKey").GetString());
        var change = Assert.Single(data.GetProperty("changes").EnumerateArray());
        Assert.Equal("time", change.GetProperty("field").GetString());
        Assert.Equal("1 h", change.GetProperty("from").GetString());
        Assert.Equal("3 h", change.GetProperty("to").GetString());
    }

    [Fact]
    public async Task FindsTheEntryByIssueAndDay_WhenThereIsOnlyOne()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60)];

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("issueIdOrKey", "FLP-7"), ("workDate", "2026-11-10"), ("description", "Reviewed the fix")));

        Assert.True(result.Success, result.Error);
        Assert.Contains(chronos.Urls, u => u.StartsWith("/api/worklogs?") && u.Contains("From=2026-11-10") && u.Contains("To=2026-11-10") && u.Contains("IssueId=42"));
        Assert.Equal("Reviewed the fix", JsonDocument.Parse(chronos.SentBody!).RootElement.GetProperty("description").GetString());
        Assert.Equal(60, JsonDocument.Parse(chronos.SentBody!).RootElement.GetProperty("durationMinutes").GetInt32());
    }

    [Fact]
    public async Task SeveralEntriesOnTheSameIssueAndDay_AreListed_AndNothingIsChanged()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60, "Morning"), FakeChronos.Log(12, "2026-11-10", 120, "Afternoon")];

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("issueIdOrKey", "FLP-7"), ("workDate", "2026-11-10"), ("durationMinutes", 30L)));

        Assert.False(result.Success);
        Assert.Contains("workLogId 11", result.Error);
        Assert.Contains("workLogId 12", result.Error);
        Assert.Contains("Ask the user which one", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task NothingOnThatDay_IsSaidPlainly()
    {
        var chronos = new FakeChronos();

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("issueIdOrKey", "FLP-7"), ("workDate", "2026-11-10"), ("durationMinutes", 30L)));

        Assert.False(result.Success);
        Assert.Contains("no time logged", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Theory]
    [InlineData("SUBMITTED")]
    [InlineData("APPROVED")]
    public async Task ASubmittedOrApprovedWeekIsLocked(string status)
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60, status: status)];

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L), ("durationMinutes", 30L)));

        Assert.False(result.Success);
        Assert.Contains(status, result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task ARejectedWeek_CanBeCorrected()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60, status: "REJECTED")];

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L), ("durationMinutes", 90L)));

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task AskingForNoChange_IsRefusedBeforeAnyCall()
    {
        var chronos = new FakeChronos();

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L)));

        Assert.False(result.Success);
        Assert.Empty(chronos.Urls);
    }

    [Fact]
    public async Task ChangingToWhatItAlreadyIs_ChangesNothing()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60, "Fixed it")];

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L), ("durationMinutes", 60L), ("description", "Fixed it")));

        Assert.False(result.Success);
        Assert.Contains("already looks like that", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task SomeoneElsesEntry_IsNotFoundForYou()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60, userId: 9)];

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L), ("durationMinutes", 30L)));

        Assert.False(result.Success);
        Assert.Contains("not yours", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(1441L)]
    public async Task ATimeOutsideOneDay_IsRefusedBeforeAnyCall(long minutes)
    {
        var chronos = new FakeChronos();

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L), ("durationMinutes", minutes)));

        Assert.False(result.Success);
        Assert.Empty(chronos.Urls);
    }

    [Fact]
    public async Task MovesAnEntryToAnotherDay_AndFlipsBillable()
    {
        var chronos = new FakeChronos();
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60, billable: true)];

        var data = ChronosArgs.Data(await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L), ("moveToDate", "2026-11-09"), ("billable", false))));

        var sent = JsonDocument.Parse(chronos.SentBody!).RootElement;
        Assert.Equal("2026-11-09", sent.GetProperty("workDate").GetString());
        Assert.False(sent.GetProperty("billable").GetBoolean());
        Assert.Equal(["billable", "day"], data.GetProperty("changes").EnumerateArray().Select(c => c.GetProperty("field").GetString()!).ToArray());
    }

    [Fact]
    public async Task WhenTheServiceRefuses_TheReasonIsPassedOn()
    {
        var chronos = new FakeChronos { PutStatus = HttpStatusCode.Conflict };
        chronos.Logs = [FakeChronos.Log(11, "2026-11-10", 60)];

        var result = await chronos.Tool<UpdateWorkLogTool>().ExecuteAsync(Args(("workLogId", 11L), ("durationMinutes", 30L)));

        Assert.False(result.Success);
        Assert.Contains("cannot be changed", result.Error);
    }
}

public class SubmitTimesheetToolTests
{
    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs) => ChronosArgs.Of(pairs);

    private static FakeChronos WithThisWeek(string status = "DRAFT", long minutes = 2400, string? submittedAt = null)
    {
        var chronos = new FakeChronos();
        chronos.PeriodsJson = "[" + FakeChronos.Period(FakeChronos.MondayOf(FakeChronos.Today), status, minutes, submittedAt: submittedAt) + "]";
        return chronos;
    }

    [Fact]
    public async Task WithoutAReviewer_ListsWhoCanReview_AndSubmitsNothing()
    {
        var chronos = WithThisWeek();

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args());

        Assert.False(result.Success);
        Assert.Contains("Meera Iyer", result.Error);
        Assert.Contains("Dev Patel", result.Error);
        Assert.Contains("40 h", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task SubmitsToTheReviewerByName_WithThePeriodsVersion()
    {
        var chronos = WithThisWeek();

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "meera")));

        Assert.True(result.Success, result.Error);
        var post = Assert.Single(chronos.Writes);
        Assert.Equal("/api/timesheets/submit", post.Request.RequestUri!.AbsolutePath);
        Assert.Equal("\"4\"", post.Request.Headers.IfMatch.Single().Tag);
        var sent = JsonDocument.Parse(post.Body!).RootElement;
        Assert.Equal(77, sent.GetProperty("periodId").GetInt64());
        Assert.Equal(21, sent.GetProperty("approverUserId").GetInt64());

        var data = ChronosArgs.Data(result);
        Assert.True(data.GetProperty("submitted").GetBoolean());
        Assert.Equal("Meera Iyer", data.GetProperty("approver").GetString());
        Assert.Equal("SUBMITTED", data.GetProperty("status").GetString());
        Assert.Equal(40.0, data.GetProperty("loggedHours").GetDouble());
        Assert.Equal(40, data.GetProperty("weeklyTargetHours").GetInt32());
    }

    [Fact]
    public async Task AReviewerCanBeGivenByUserId()
    {
        var chronos = WithThisWeek();

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "22")));

        Assert.True(result.Success, result.Error);
        Assert.Equal(22, JsonDocument.Parse(chronos.SentBody!).RootElement.GetProperty("approverUserId").GetInt64());
    }

    [Fact]
    public async Task ANameThatFitsTwoReviewers_IsRefusedWithBoth()
    {
        var chronos = WithThisWeek();
        chronos.ApproversJson = """[{"id":21,"displayName":"Meera Iyer","roles":[]},{"id":23,"displayName":"Meera Das","roles":[]}]""";

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "Meera")));

        Assert.False(result.Success);
        Assert.Contains("Meera Iyer (userId 21)", result.Error);
        Assert.Contains("Meera Das (userId 23)", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Theory]
    [InlineData("Zoe")]
    [InlineData("me")]
    [InlineData("999")]
    public async Task APersonWhoCannotReview_IsRefusedWithTheEligibleList(string who)
    {
        var chronos = WithThisWeek();

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", who)));

        Assert.False(result.Success);
        Assert.Contains("Eligible reviewers: Meera Iyer, Dev Patel", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task AWeekWithNothingLogged_HasNothingToSubmit()
    {
        var chronos = new FakeChronos();

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "Meera")));

        Assert.False(result.Success);
        Assert.Contains("Nothing is logged", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task AnEmptyPeriod_HasNothingToSubmit()
    {
        var chronos = WithThisWeek(minutes: 0);

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "Meera")));

        Assert.False(result.Success);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task AWeekAlreadySubmitted_IsNotSentAgain()
    {
        var chronos = WithThisWeek("SUBMITTED", submittedAt: "2026-11-12T08:00:00+00:00");

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "Meera")));

        Assert.False(result.Success);
        Assert.Contains("already submitted on 2026-11-12", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task AnApprovedWeek_IsNotSentAgain()
    {
        var chronos = WithThisWeek("APPROVED");

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "Meera")));

        Assert.False(result.Success);
        Assert.Contains("already approved", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task ARejectedWeek_CanBeSubmittedAgain()
    {
        var chronos = WithThisWeek("REJECTED");

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "Dev")));

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task AnyDayInTheWeek_MeansThatWeeksMonday()
    {
        var chronos = new FakeChronos();

        await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("weekStart", "2026-11-12"))); 

        var url = chronos.Urls.First(u => u.StartsWith("/api/timesheets/me"));
        Assert.Contains("From=2026-11-09", url);
        Assert.Contains("To=2026-11-15", url);
    }

    [Fact]
    public async Task WhenTheServiceRefuses_TheReasonIsPassedOn()
    {
        var chronos = WithThisWeek();
        chronos.SubmitStatus = HttpStatusCode.BadRequest;

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "Meera")));

        Assert.False(result.Success);
        Assert.Contains("cannot access every issue", result.Error);
    }

    [Fact]
    public async Task NoOneToReview_IsExplained()
    {
        var chronos = WithThisWeek();
        chronos.ApproversJson = "[]";

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(Args(("approver", "Meera")));

        Assert.False(result.Success);
        Assert.Contains("No one is eligible", result.Error);
        Assert.Empty(chronos.Writes);
    }

    [Fact]
    public async Task WithoutAKnownCaller_ItRefuses()
    {
        var chronos = WithThisWeek();

        var result = await chronos.Tool<SubmitTimesheetTool>().ExecuteAsync(new Dictionary<string, object?> { ["approver"] = "Meera" });

        Assert.False(result.Success);
        Assert.Empty(chronos.Urls);
    }
}
