using System.Net;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;


internal sealed class FakePeople
{
    public HttpStatusCode PolicyStatus = HttpStatusCode.OK;
    public HttpStatusCode DirectoryStatus = HttpStatusCode.OK;
    public Dictionary<int, string> Policies = new();

    public string ReferencesJson = """
        [{"id":11,"kind":"DEPARTMENT","name":"Engineering"},{"id":12,"kind":"DEPARTMENT","name":"Design"},
         {"id":21,"kind":"DESIGNATION","name":"Software Engineer"},{"id":22,"kind":"DESIGNATION","name":"Engineering Manager"},{"id":23,"kind":"DESIGNATION","name":"Designer"},
         {"id":31,"kind":"JOB_TITLE","name":"Backend Developer"},{"id":32,"kind":"JOB_TITLE","name":"UI Designer"},
         {"id":41,"kind":"LOCATION","name":"Chennai"},{"id":42,"kind":"LOCATION","name":"Pune"}]
        """;

    
    public List<(long Id, string First, string Last, string Code, long Dept, long Desig, long Job, long Loc, long? Manager, string Status)> People =
    [
        (1, "Asha", "Kumar", "E1", 11, 21, 31, 41, null, "ACTIVE"),
        (2, "Priya", "Nair", "E2", 11, 22, 31, 41, null, "ACTIVE"),
        (3, "Ravi", "Menon", "E3", 11, 21, 31, 42, 2, "ACTIVE"),
        (4, "Meera", "Iyer", "E4", 12, 23, 32, 41, 2, "ACTIVE"),
        (5, "Old", "Timer", "E5", 11, 21, 31, 41, 2, "TERMINATED"),
    ];

    public RecordingHandler Handler { get; }

    public FakePeople()
    {
        Handler = new RecordingHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri.Query);
            if (path == "/api/employees/absence/policy")
            {
                if (PolicyStatus != HttpStatusCode.OK) return Http.Json("{}", PolicyStatus);
                var year = int.Parse(query["year"]!);
                return Http.Json(Policies.TryGetValue(year, out var holidays)
                    ? $"{{\"year\":{year},\"configured\":true,\"holidays\":{holidays}}}"
                    : $"{{\"year\":{year},\"configured\":false,\"holidays\":[]}}");
            }
            if (path == "/api/employees/references") return Http.Json(ReferencesJson);
            if (path == "/api/employees")
            {
                if (DirectoryStatus != HttpStatusCode.OK) return Http.Json("{}", DirectoryStatus);
                var search = query["Search"]?.ToLowerInvariant();
                var status = query["EmploymentStatus"];
                var rows = People.Where(p => (status == null || p.Status == status)
                    && (search == null || p.First.ToLowerInvariant().Contains(search) || p.Last.ToLowerInvariant().Contains(search) || p.Code.ToLowerInvariant().Contains(search))).ToList();
                var items = rows.Select(p => $"{{\"id\":{p.Id},\"userId\":{p.Id + 100},\"firstName\":\"{p.First}\",\"lastName\":\"{p.Last}\",\"employeeCode\":\"{p.Code}\",\"workEmail\":\"{p.First.ToLowerInvariant()}@x.test\",\"phone\":null,\"departmentId\":{p.Dept},\"designationId\":{p.Desig},\"jobTitleId\":{p.Job},\"locationId\":{p.Loc},\"managerEmployeeId\":{(p.Manager == null ? "null" : p.Manager.ToString())},\"employmentStatus\":\"{p.Status}\"}}");
                return Http.Json($"{{\"items\":[{string.Join(",", items)}],\"page\":1,\"pageSize\":100,\"totalCount\":{rows.Count},\"totalPages\":1}}");
            }
            return Http.Json("{}", HttpStatusCode.NotFound);
        });
    }

    public T Tool<T>() where T : class => (T)Activator.CreateInstance(typeof(T), new FakeHttpClientFactory(Handler), new HttpContextAccessor())!;
    public IEnumerable<string> Urls => Handler.Calls.Select(c => c.Request.RequestUri!.PathAndQuery);
}

public class CompanyHolidaysToolTests
{
    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    private static JsonElement Data(ToolResult result) => JsonDocument.Parse(result.Data!.ToString()!).RootElement;

    private static FakePeople WithCalendar(int year, params string[] dates)
    {
        var fake = new FakePeople();
        fake.Policies[year] = "[" + string.Join(",", dates.Select(d => $"\"{d}\"")) + "]";
        return fake;
    }

    [Fact]
    public async Task ListsTheYear_WithWeekdays_AndMarksLongWeekends()
    {
        var fake = WithCalendar(2030, "2030-01-01", "2030-01-25", "2030-01-26");

        var data = Data(await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args(("year", 2030L))));

        var holidays = data.GetProperty("holidays").EnumerateArray().ToList();
        Assert.Equal(["Tuesday", "Friday", "Saturday"], holidays.Select(h => h.GetProperty("weekday").GetString()!).ToArray());
        Assert.False(holidays[0].GetProperty("makesLongWeekend").GetBoolean());
        Assert.True(holidays[1].GetProperty("makesLongWeekend").GetBoolean());
        Assert.True(holidays[2].GetProperty("fallsOnWeekend").GetBoolean());
        Assert.Equal(3, data.GetProperty("count").GetInt32());
        Assert.Equal("2030-01-01", data.GetProperty("next").GetProperty("date").GetString());
        Assert.True(holidays[0].GetProperty("daysAway").GetInt32() > 0);
    }

    [Fact]
    public async Task APastYear_HasNoNextHoliday()
    {
        var fake = WithCalendar(2020, "2020-01-01", "2020-08-15");

        var data = Data(await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args(("year", 2020L))));

        Assert.Equal(JsonValueKind.Null, data.GetProperty("next").ValueKind);
        Assert.Equal(0, data.GetProperty("upcomingCount").GetInt32());
        Assert.All(data.GetProperty("holidays").EnumerateArray(), h => Assert.True(h.GetProperty("isPast").GetBoolean()));
    }

    [Fact]
    public async Task WithNoYear_ReadsThisYear()
    {
        var fake = new FakePeople();

        await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args());

        Assert.Contains(fake.Urls, u => u == $"/api/employees/absence/policy?year={FakeChronos.Today.Year}");
    }

    [Fact]
    public async Task ACalendarThatWasNeverSetUp_IsSaidPlainly()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args(("year", 2031L))));

        Assert.Equal(0, data.GetProperty("count").GetInt32());
        Assert.Contains("has not been set up for 2031", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task ARange_CountsWorkingDays_MinusHolidays()
    {
        var fake = WithCalendar(2030, "2030-12-25");

        var data = Data(await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args(("fromDate", "2030-12-23"), ("toDate", "2030-12-27"))));

        var range = data.GetProperty("range");
        Assert.Equal(5, range.GetProperty("calendarDays").GetInt32());
        Assert.Equal(4, range.GetProperty("workingDays").GetInt32());
        Assert.Equal(0, range.GetProperty("weekendDays").GetInt32());
        Assert.Equal("2030-12-25", Assert.Single(range.GetProperty("holidays").EnumerateArray()).GetProperty("date").GetString());
        Assert.Equal(4, data.GetProperty("leaveDaysUsed").GetInt32());
    }

    [Fact]
    public async Task ARangeAcrossNewYear_IsExplained_AndReadsBothCalendars()
    {
        var fake = WithCalendar(2030, "2030-12-25");

        var data = Data(await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args(("fromDate", "2030-12-23"), ("toDate", "2031-01-01"))));

        
        Assert.Contains("two separate requests", data.GetProperty("note").GetString());
        Assert.Contains(fake.Urls, u => u.EndsWith("year=2030"));
        Assert.Contains(fake.Urls, u => u.EndsWith("year=2031"));
    }

    [Fact]
    public async Task AHolidayOnAWeekend_IsNotTakenOffTheWorkingDays()
    {
        var fake = WithCalendar(2030, "2030-01-26"); 

        var data = Data(await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args(("fromDate", "2030-01-21"), ("toDate", "2030-01-27"))));

        var range = data.GetProperty("range");
        Assert.Equal(5, range.GetProperty("workingDays").GetInt32());
        Assert.Equal(2, range.GetProperty("weekendDays").GetInt32());
    }

    [Fact]
    public async Task OneDateGiven_IsASingleDay()
    {
        var fake = WithCalendar(2030, "2030-12-25");

        var data = Data(await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args(("toDate", "2030-12-25"))));

        Assert.Equal(1, data.GetProperty("range").GetProperty("calendarDays").GetInt32());
        Assert.Equal(0, data.GetProperty("range").GetProperty("workingDays").GetInt32());
    }

    [Theory]
    [InlineData("not-a-date", null, null)]
    [InlineData("2030-02-01", "2030-01-01", null)]
    [InlineData("2030-01-01", "2031-06-01", null)]
    [InlineData(null, null, 1999L)]
    public async Task BadInput_IsRefused_BeforeAnyCall(string? from, string? to, long? year)
    {
        var fake = new FakePeople();

        var result = await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args(("fromDate", from), ("toDate", to), ("year", year)));

        Assert.False(result.Success);
        Assert.Empty(fake.Urls);
    }

    [Fact]
    public async Task WhenTheCalendarCannotBeRead_ItSaysSo()
    {
        var fake = new FakePeople { PolicyStatus = HttpStatusCode.InternalServerError };

        var result = await fake.Tool<GetCompanyHolidaysTool>().ExecuteAsync(Args());

        Assert.False(result.Success);
        Assert.Contains("holiday calendar", result.Error);
    }
}

public class FindPeopleToolTests
{
    private static Dictionary<string, object?> Args(params (string Key, object? Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    private static JsonElement Data(ToolResult result) => JsonDocument.Parse(result.Data!.ToString()!).RootElement;
    private static string[] Names(JsonElement data) => data.GetProperty("people").EnumerateArray().Select(p => p.GetProperty("name").GetString()!).ToArray();

    [Fact]
    public async Task FindsPeopleByDepartment_ByName_OnlyActiveOnes()
    {
        var fake = new FakePeople();

        var result = await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("department", "engineering")));

        Assert.True(result.Success, result.Error);
        Assert.Equal(["Asha Kumar", "Priya Nair", "Ravi Menon"], Names(Data(result))); 
        Assert.Contains(fake.Urls, u => u.Contains("EmploymentStatus=ACTIVE"));
    }

    [Fact]
    public async Task StatusAll_IncludesPeopleWhoLeft()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("department", "Engineering"), ("status", "all"))));

        Assert.Contains("Old Timer", Names(data));
        Assert.DoesNotContain(fake.Urls, u => u.Contains("EmploymentStatus"));
    }

    [Fact]
    public async Task AnExactDesignation_WinsOverPartialMatches()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("designation", "Software Engineer"))));

        Assert.Equal(["Asha Kumar", "Ravi Menon"], Names(data));
    }

    [Fact]
    public async Task APartialDesignation_MatchesEveryRoleThatContainsIt()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("designation", "engineer"))));

        Assert.Equal(["Asha Kumar", "Priya Nair", "Ravi Menon"], Names(data)); 
    }

    [Fact]
    public async Task FiltersCombine()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("department", "Engineering"), ("location", "Chennai"))));

        Assert.Equal(["Asha Kumar", "Priya Nair"], Names(data));
    }

    [Fact]
    public async Task ShowsWorkDetails_AndNoPersonalOnes()
    {
        var fake = new FakePeople();

        var result = await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("query", "Meera")));

        var person = Assert.Single(Data(result).GetProperty("people").EnumerateArray());
        Assert.Equal("Design", person.GetProperty("department").GetString());
        Assert.Equal("Designer", person.GetProperty("designation").GetString());
        Assert.Equal("UI Designer", person.GetProperty("jobTitle").GetString());
        Assert.Equal("Chennai", person.GetProperty("location").GetString());
        Assert.Equal("Priya Nair", person.GetProperty("manager").GetString());
        Assert.Equal("meera@x.test", person.GetProperty("workEmail").GetString());
        var json = result.Data!.ToString()!;
        Assert.DoesNotContain("phone", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salary", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", json);
    }

    [Fact]
    public async Task AFullNameIsFound_EvenThoughTheDirectorySearchMatchesOneNameAtATime()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("query", "Ravi Menon"))));

        Assert.Equal(["Ravi Menon"], Names(data));
        Assert.Contains(fake.Urls, u => u.Contains("Search=Ravi") && !u.Contains("Menon"));
    }

    [Fact]
    public async Task ListsTheDirectReportsOfAManager_ByName()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("manager", "Priya Nair"))));

        Assert.Equal(["Meera Iyer", "Ravi Menon"], Names(data));
    }

    [Fact]
    public async Task AManagerIsFoundEvenWhenTheOtherFiltersWouldHideThem()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("manager", "Priya"), ("department", "Design"))));

        Assert.Equal(["Meera Iyer"], Names(data));
    }

    [Fact]
    public async Task AManagerNameThatFitsTwoPeople_IsRefusedWithBoth()
    {
        var fake = new FakePeople();
        fake.People.Add((6, "Priya", "Shah", "E6", 12, 23, 32, 42, null, "ACTIVE"));

        var result = await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("manager", "Priya")));

        Assert.False(result.Success);
        Assert.Contains("Priya Nair (E2)", result.Error);
        Assert.Contains("Priya Shah (E6)", result.Error);
    }

    [Fact]
    public async Task AManagerNobodyHas_IsSaid()
    {
        var fake = new FakePeople();

        var result = await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("manager", "Zoe")));

        Assert.False(result.Success);
        Assert.Contains("anyone called 'Zoe'", result.Error);
    }

    [Fact]
    public async Task AnUnknownDepartment_ListsTheOnesThatExist()
    {
        var fake = new FakePeople();

        var result = await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("department", "Marketing")));

        Assert.False(result.Success);
        Assert.Contains("No department is called 'Marketing'", result.Error);
        Assert.Contains("Engineering", result.Error);
        Assert.Contains("Design", result.Error);
    }

    [Fact]
    public async Task NobodyMatching_IsSaid()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("query", "Nobody"))));

        Assert.Equal(0, data.GetProperty("count").GetInt32());
        Assert.Equal("Nobody matches.", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task AShortLimit_ShowsSome_AndSaysThereAreMore()
    {
        var fake = new FakePeople();

        var data = Data(await fake.Tool<FindPeopleTool>().ExecuteAsync(Args(("department", "Engineering"), ("limit", 2L))));

        Assert.Equal(3, data.GetProperty("count").GetInt32());
        Assert.Equal(2, Names(data).Length);
        Assert.Contains("Showing 2 of 3", data.GetProperty("note").GetString());
    }

    [Fact]
    public async Task WhenTheDirectoryCannotBeRead_ItSaysSo()
    {
        var fake = new FakePeople { DirectoryStatus = HttpStatusCode.Forbidden };

        var result = await fake.Tool<FindPeopleTool>().ExecuteAsync(Args());

        Assert.False(result.Success);
        Assert.Contains("search the directory", result.Error);
    }
}
