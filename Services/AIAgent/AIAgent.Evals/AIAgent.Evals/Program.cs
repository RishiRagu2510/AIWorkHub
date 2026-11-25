using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Infrastructure.Llm;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;























var options = Options.Parse(args);
if (options.Error is not null) { Console.Error.WriteLine(options.Error); return 2; }

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddJsonFile("appsettings.Secrets.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

if (string.IsNullOrWhiteSpace(configuration["AI:ApiKey"] ?? configuration["GEMINI_API_KEY"]))
{
    Console.Error.WriteLine("No model key found. Put AI:ApiKey in AIAgent.Api/appsettings.Secrets.json or set GEMINI_API_KEY.");
    return 2;
}

var casesPath = Path.Combine(AppContext.BaseDirectory, "cases.json");
var cases = JsonSerializer.Deserialize<List<EvalCase>>(await File.ReadAllTextAsync(casesPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
cases = cases
    .Where(c => options.Filter is null || c.Id.Contains(options.Filter, StringComparison.OrdinalIgnoreCase))
    .Where(c => options.Role is null || string.Equals(c.Role, options.Role, StringComparison.OrdinalIgnoreCase))
    .ToList();
if (cases.Count == 0) { Console.Error.WriteLine("No cases match."); return 2; }

var services = new ServiceCollection()
    .AddSingleton<IHttpClientFactory>(new OfflineClientFactory())
    .AddSingleton<IHttpContextAccessor>(new HttpContextAccessor())
    
    .AddSingleton<AIAgent.Application.Interfaces.IUserMemoryStore, AIAgent.Infrastructure.Memory.UnavailableUserMemoryStore>()
    .AddSingleton<AIAgent.Application.Interfaces.IKnowledgeStore, AIAgent.Infrastructure.Knowledge.UnavailableKnowledgeStore>()
    .BuildServiceProvider();

var realTools = typeof(BaseAgentTool).Assembly.GetTypes()
    .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IAgentTool).IsAssignableFrom(t))
    .Select(t => (IAgentTool)ActivatorUtilities.CreateInstance(services, t))
    .ToList();

HttpClient Client() => new() { Timeout = TimeSpan.FromMinutes(5) };
ILlmProvider llm = new ModelRoutingLlmProvider(
    new LlmProvider(Client(), configuration, new WarningLogger<LlmProvider>()),
    new OpenAiChatClient(Client(), configuration, new WarningLogger<OpenAiChatClient>()),
    new GroqChatClient(Client(), configuration, new WarningLogger<GroqChatClient>()),
    new WarningLogger<ModelRoutingLlmProvider>());

Console.WriteLine($"Athenna evaluation: {cases.Count} questions x {options.Repeat}, model {options.Model ?? configuration["AI:Model"] ?? "default"}, {realTools.Count} tools\n");

var results = new List<CaseResult>();
foreach (var evalCase in cases)
{
    var attempts = new List<Attempt>();
    for (var i = 0; i < options.Repeat; i++)
    {
        var attempt = await RunOnceAsync(evalCase);

        
        
        for (var retry = 1; attempt.Invalid && retry <= options.Retries; retry++)
        {
            Console.WriteLine($"      ~ no real answer ({Short(attempt.InvalidReason)}); waiting {options.RetryWaitSeconds}s, then asking again ({retry}/{options.Retries})");
            await Task.Delay(TimeSpan.FromSeconds(options.RetryWaitSeconds));
            attempt = await RunOnceAsync(evalCase);
        }

        attempts.Add(attempt);
        if (options.DelayMs > 0) await Task.Delay(options.DelayMs);
    }

    var result = new CaseResult(evalCase, attempts);
    results.Add(result);
    Console.WriteLine(result.Line());
    foreach (var problem in result.Problems()) Console.WriteLine($"      {problem}");
}

var valid = results.Sum(r => r.Attempts.Count(a => !a.Invalid));
var passed = results.Sum(r => r.Attempts.Count(a => a.Passed));
var invalid = results.Sum(r => r.Attempts.Count(a => a.Invalid));
var total = results.Sum(r => r.Attempts.Count);
var rate = total == 0 ? 0 : (double)passed / total;

Console.WriteLine();
Console.WriteLine($"Passed {passed} of {total} ({rate:P0}).{(invalid > 0 ? $" {invalid} did not get a real model answer and count as failed." : "")}");
foreach (var group in results.GroupBy(r => r.Case.Role).OrderBy(g => g.Key))
{
    var gTotal = group.Sum(r => r.Attempts.Count);
    var gPassed = group.Sum(r => r.Attempts.Count(a => a.Passed));
    Console.WriteLine($"  {group.Key,-16} {gPassed}/{gTotal}");
}

if (options.JsonPath is not null)
{
    var output = results.Select(r => new
    {
        r.Case.Id, r.Case.Role, r.Case.Message,
        attempts = r.Attempts.Select(a => new { a.Passed, a.Invalid, a.InvalidReason, tools = a.Calls.Select(c => c.Name), a.Failures, a.Reply, seconds = Math.Round(a.Seconds, 2) })
    });
    await File.WriteAllTextAsync(options.JsonPath, JsonSerializer.Serialize(new { at = DateTime.UtcNow, model = options.Model ?? configuration["AI:Model"], passed, total, cases = output }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Results written to {options.JsonPath}");
}

if (rate < options.MinPass)
{
    Console.WriteLine($"FAILED: {rate:P0} is below the required {options.MinPass:P0}.");
    return 1;
}
Console.WriteLine("OK");
return 0;

static string Short(string? text) => text is null ? "" : text.Length <= 90 ? text : text[..90] + "...";

async Task<Attempt> RunOnceAsync(EvalCase evalCase)
{
    var calls = new List<RecordedCall>();
    var spies = realTools.Select(t => (IAgentTool)new SpyTool(t, calls)).ToList();
    var orchestrator = new AthennaAgentOrchestrator(llm, spies);
    var stream = options.Stream ? new StreamRecorder() : null;

    var context = new Dictionary<string, object>
    {
        ["role"] = evalCase.Role,
        ["userId"] = 7L,
        ["userName"] = "Asha Kumar",
        ["permissions"] = Roles.PermissionsFor(evalCase.Role)
    };
    if (options.Model is not null) context["model"] = options.Model;

    var clock = Stopwatch.StartNew();
    AgentChatResponse response;
    try
    {
        response = await orchestrator.ExecuteChatAsync(new AgentChatRequest(evalCase.Message, SessionId: Guid.NewGuid().ToString("N"), ContextPortal: "AIWorkHub", ExtraContext: context), progress: stream);
    }
    catch (Exception ex)
    {
        return Attempt.Broken($"threw {ex.GetType().Name}: {ex.Message}", clock.Elapsed.TotalSeconds);
    }

    
    if (response.Message == LlmChatResponse.UnexpectedErrorMessage)
        return Attempt.Broken("the real model did not answer", clock.Elapsed.TotalSeconds, response.Message, calls);

    var failures = Judge.Check(evalCase, calls, response.Message ?? "");
    if (stream is not null)
    {
        Console.WriteLine($"      stream: {stream.Summary(clock.Elapsed.TotalSeconds)}");
        if (stream.Problem(response.Message ?? "") is { } problem) failures.Add(problem);
    }
    return new Attempt(failures.Count == 0, false, null, calls, failures, response.Message ?? "", clock.Elapsed.TotalSeconds);
}



internal sealed record EvalCase
{
    public string Id { get; init; } = "";
    public string Role { get; init; } = "DEVELOPER";
    public string Message { get; init; } = "";
    
    public List<string>? MustCall { get; init; }
    
    public List<string>? AnyOf { get; init; }
    public List<string>? MustNotCall { get; init; }
    
    public bool NoTools { get; init; }
    public Dictionary<string, int>? MaxCalls { get; init; }
    
    public Dictionary<string, Dictionary<string, string>>? Args { get; init; }
    
    public List<string>? ReplyAny { get; init; }
    public List<string>? ReplyNone { get; init; }
}

internal sealed record RecordedCall(string Name, Dictionary<string, string> Arguments);

internal sealed record Attempt(bool Passed, bool Invalid, string? InvalidReason, List<RecordedCall> Calls, List<string> Failures, string Reply, double Seconds)
{
    public static Attempt Broken(string reason, double seconds, string reply = "", List<RecordedCall>? calls = null) =>
        new(false, true, reason, calls ?? [], [], reply, seconds);
}

internal sealed record CaseResult(EvalCase Case, List<Attempt> Attempts)
{
    public string Line()
    {
        var ok = Attempts.Count(a => a.Passed);
        var mark = ok == Attempts.Count ? "PASS" : ok == 0 ? "FAIL" : "FLAKY";
        var tools = string.Join(", ", Attempts[^1].Calls.Select(c => c.Name).Distinct());
        var seconds = Attempts.Average(a => a.Seconds);
        var count = Attempts.Count > 1 ? $" {ok}/{Attempts.Count}" : "";
        return $"  {mark,-5}{count,-5} {Case.Role,-15} {Case.Id,-40} {(tools.Length == 0 ? "(no tools)" : tools),-50} {seconds,4:0.0}s";
    }

    public IEnumerable<string> Problems()
    {
        foreach (var attempt in Attempts.Where(a => !a.Passed).Take(1))
        {
            if (attempt.Invalid) yield return $"! {attempt.InvalidReason}";
            foreach (var failure in attempt.Failures) yield return $"- {failure}";
            if (attempt.Failures.Count > 0 && attempt.Reply.Length > 0)
                yield return $"  reply: {Shorten(attempt.Reply)}";
        }
    }

    private static string Shorten(string text)
    {
        var line = text.Replace('\n', ' ').Trim();
        return line.Length <= 160 ? line : line[..160] + "...";
    }
}

internal static class Judge
{
    public static List<string> Check(EvalCase c, List<RecordedCall> calls, string reply)
    {
        var failures = new List<string>();
        var names = calls.Select(x => x.Name).ToList();

        foreach (var tool in c.MustCall ?? []) if (!names.Contains(tool)) failures.Add($"should have called {tool}");
        if (c.AnyOf is { Count: > 0 } any && !any.Any(names.Contains)) failures.Add($"should have called one of {string.Join(", ", any)}");
        foreach (var tool in c.MustNotCall ?? []) if (names.Contains(tool)) failures.Add($"should not have called {tool}");
        if (c.NoTools && names.Count > 0) failures.Add($"should not have used tools, but called {string.Join(", ", names.Distinct())}");

        foreach (var (tool, limit) in c.MaxCalls ?? [])
        {
            var used = names.Count(n => n == tool);
            if (used > limit) failures.Add($"called {tool} {used} times, expected at most {limit}");
        }

        foreach (var (tool, expected) in c.Args ?? [])
        {
            var made = calls.Where(x => x.Name == tool).ToList();
            if (made.Count == 0) continue; 
            foreach (var (argument, text) in expected)
                if (!made.Any(call => call.Arguments.TryGetValue(argument, out var value) && value.Contains(text, StringComparison.OrdinalIgnoreCase)))
                    failures.Add($"{tool} should have been given {argument} containing '{text}'");
        }

        if (c.ReplyAny is { Count: > 0 } words && !words.Any(w => reply.Contains(w, StringComparison.OrdinalIgnoreCase)))
            failures.Add($"the reply should mention one of: {string.Join(", ", words)}");
        foreach (var word in c.ReplyNone ?? []) if (reply.Contains(word, StringComparison.OrdinalIgnoreCase)) failures.Add($"the reply should not mention '{word}'");

        return failures;
    }
}


internal sealed class SpyTool(IAgentTool inner, List<RecordedCall> calls) : IAgentTool
{
    public string Name => inner.Name;
    public string Description => inner.Description;
    public string Category => inner.Category;
    public string RequiredPermission => inner.RequiredPermission;
    public object ParameterSchema => inner.ParameterSchema;

    public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var shown = arguments
            .Where(a => !a.Key.StartsWith('_') && !IssueVisibility.ReservedArguments.Contains(a.Key))
            .ToDictionary(a => a.Key, a => a.Value?.ToString() ?? "");
        calls.Add(new RecordedCall(Name, shown));
        return Task.FromResult(new ToolResult(true, Canned.For(Name)));
    }
}

internal static class Canned
{
    public static object For(string tool) => tool switch
    {
        "search_issues" => new { count = 1, issues = new[] { new { issueKey = "FLP360-17", summary = "Login fails on Safari", status = "IN_PROGRESS", priority = "HIGH", assignee = "Asha Kumar" } } },
        "get_issue_details" => new { issueKey = "FLP360-17", summary = "Login fails on Safari", status = "IN_PROGRESS", priority = "HIGH", assignee = "Asha Kumar", reporter = "Ben Rao", description = "Users on Safari cannot sign in." },
        "assign_issue" => new { assigned = true, issueKey = "FLP360-17", summary = "Login fails on Safari", statusCode = "IN_PROGRESS", previousAssignees = "Asha Kumar", assignees = "Priya Nair" },
        "update_issue" => new { updated = true, issueKey = "FLP360-17", summary = "Login fails on Safari", statusCode = "IN_PROGRESS", priority = "HIGH", changes = new[] { new { field = "priority", from = "MEDIUM", to = "HIGH" } } },
        "get_work_digest" => new
        {
            digest = true,
            today = "2026-11-11",
            counts = new { overdue = 1, dueWithin7Days = 1, inProgress = 1, other = 0, total = 3 },
            items = new object[]
            {
                new { issueKey = "FLP360-9", summary = "Fix payroll export", statusCode = "TODO", priority = "HIGHEST", dueDate = "2026-11-09", group = "overdue", why = "overdue by 2 days, highest priority" },
                new { issueKey = "FLP360-12", summary = "Review leave policy copy", statusCode = "TODO", priority = "MEDIUM", dueDate = "2026-11-13", group = "due_soon", why = "due in 2 days, medium priority" },
                new { issueKey = "FLP360-17", summary = "Login fails on Safari", statusCode = "IN_PROGRESS", priority = "HIGH", dueDate = (string?)null, group = "in_progress", why = "already under way (in progress), high priority" }
            }
        },
        "get_work_logs" => new { from = "2026-11-10", to = "2026-11-10", count = 1, totalHours = 2.5, logs = new[] { new { workLogId = 11, issueKey = "FLP360-17", summary = "Login fails on Safari", workDate = "2026-11-10", hours = 2.5, note = "Fixed the redirect", canEdit = true } } },
        "update_work_log" => new { updated = true, workLogId = 11, issueKey = "FLP360-17", summary = "Login fails on Safari", workDate = "2026-11-10", hours = 3.0, changes = new[] { new { field = "time", from = "2.5 h", to = "3 h" } } },
        "submit_timesheet" => new { submitted = true, weekStart = "2026-11-09", weekEnd = "2026-11-15", status = "SUBMITTED", approver = "Meera Iyer", loggedHours = 40.0, weeklyTargetHours = 40 },
        "get_sprint_overview" => new
        {
            overview = true,
            project = new { id = 9, key = "FLP360", name = "Flagship" },
            sprint = new { id = 1, name = "Sprint 12", goal = "Ship the new login", status = "ACTIVE", startDate = "2026-11-02", endDate = "2026-11-15", daysLeft = 4, totalDays = 14, timeElapsedPercent = 71 },
            scope = "the whole sprint",
            issues = new { total = 12, done = 4, open = 8, cancelled = 0, byStatus = new { DONE = 4, IN_PROGRESS = 3, TODO = 5 } },
            points = new { total = 34.0, done = 11.0, remaining = 23.0, donePercent = 32.0, unestimatedIssues = 1 },
            signals = new[] { "Behind pace: 32% of the points are done with 71% of the time gone.", "2 open issues are past their due date." },
            workload = new[] { new { name = "Priya Nair", openIssues = 4, openPoints = 13.0 }, new { name = "Asha Kumar", openIssues = 3, openPoints = 8.0 } },
            items = new[] { new { issueKey = "FLP360-9", summary = "Fix payroll export", statusCode = "TODO", priority = "HIGH", assignee = "Priya Nair", why = "overdue by 2 days" } }
        },
        "get_company_holidays" => new
        {
            year = 2026,
            today = "2026-11-11",
            count = 3,
            upcomingCount = 2,
            next = new { date = "2026-12-25", weekday = "Friday", daysAway = 44, isPast = false, fallsOnWeekend = false, makesLongWeekend = true },
            holidays = new[]
            {
                new { date = "2026-08-15", weekday = "Saturday", daysAway = -88, isPast = true, fallsOnWeekend = true, makesLongWeekend = false },
                new { date = "2026-11-08", weekday = "Sunday", daysAway = -3, isPast = true, fallsOnWeekend = true, makesLongWeekend = false },
                new { date = "2026-12-25", weekday = "Friday", daysAway = 44, isPast = false, fallsOnWeekend = false, makesLongWeekend = true }
            }
        },
        "find_people" => new
        {
            count = 2,
            people = new[]
            {
                new { name = "Asha Kumar", employeeCode = "E1", workEmail = "asha@x.test", designation = "Software Engineer", department = "Engineering", jobTitle = "Backend Developer", location = "Chennai", manager = "Priya Nair", status = "ACTIVE" },
                new { name = "Ravi Menon", employeeCode = "E3", workEmail = "ravi@x.test", designation = "Software Engineer", department = "Engineering", jobTitle = "Backend Developer", location = "Pune", manager = "Priya Nair", status = "ACTIVE" }
            }
        },
        "list_projects" => new { projects = new[] { new { key = "FLP360", name = "Flagship" } } },
        "check_employee_leaves" => new { balances = new[] { new { label = "Sick leave", allocated = 12, used = 2, pending = 0, available = 10 } }, items = Array.Empty<object>(), policyConfigured = true },
        "get_timesheet_history" => new { weeks = new[] { new { week = "2026-11-02", logged = 32.0, target = 40.0, missing = 8.0, status = "OPEN" } } },
        "get_employee_profile" => new { name = "Vetri Selvan", employeeCode = "E102", designation = "Engineer", department = "Engineering", skills = new[] { new { skillName = "React", proficiencyLabel = "Advanced" } } },
        "get_career_history" => new { employee = "Vetri Selvan", count = 2, periods = new[] { new { period = "2024-01 to now", status = "CURRENT", gradeDesignation = "G4 / Engineer", jobTitle = "Software Engineer", department = "Engineering", periodLength = "1 yr 10 mos", notes = "" }, new { period = "2022-01 to 2023-12", status = "PAST", gradeDesignation = "G3 / Associate", jobTitle = "Associate Engineer", department = "Engineering", periodLength = "2 yrs", notes = "" } } },
        "get_employee_record" => new { employee = "Asha Kumar", section = "education", count = 1, items = new[] { new { institution = "MIT", degree = "BSc", fieldOfStudy = "Computer Science", period = "2018 - 2022" } } },
        "get_employee_compensation" => new { employee = "Vetri Selvan", currentCtc = "1200000", currency = "INR", effectiveFrom = "2024-01-01" },
        "get_compensation_matrix" => new { employeesWithCurrentCompensation = 5, employeesInDirectory = 23, bands = new[] { new { designation = "Engineer", min = 900000, max = 1500000, basedOn = "5 people" } } },
        "get_all_employees_details" => new { count = 1, employees = new[] { new { employeeCode = "E1", name = "Asha Kumar", designation = "Engineer", department = "Engineering", status = "ACTIVE" } }, message = "The Excel file is ready to download." },
        "get_employee_full_details" => new
        {
            employee = new { name = "Vetri Selvan", employeeCode = "E102", designation = "Engineer", department = "Engineering", status = "ACTIVE" },
            careerHistory = new { count = 1, periods = new[] { new { period = "2024-01 to now", status = "CURRENT", jobTitle = "Software Engineer" } } },
            compensation = new { currentCtc = "1200000", currency = "INR" },
            personal = new { items = Array.Empty<object>() },
            experience = new { count = 0, items = Array.Empty<object>() },
            education = new { count = 1, items = new[] { new { institution = "MIT", degree = "BSc" } } },
            certifications = new { count = 0, items = Array.Empty<object>() },
            languages = new { count = 1, items = new[] { new { language = "English", proficiency = "Fluent" } } },
            unavailable = Array.Empty<string>(),
            note = "Every section was read; there is nothing more to fetch."
        },
        "export_issues_report" => new { count = 12, open = 8, done = 4, overdue = 2, storyPoints = 31, loggedHours = 46.5, filters = "all issues you can see", file = new { fileName = "Issues_Report_2026-11-11.xlsx", format = "table", contentWithheldFromAssistant = true } },
        "export_work_log_report" => new { from = "2026-10-01", to = "2026-10-31", count = 22, totalHours = 168.5, billableHours = 150, daysLogged = 21, file = new { fileName = "Time_Report_2026-10-01_to_2026-10-31.xlsx", format = "table", contentWithheldFromAssistant = true } },
        "search_company_docs" => new
        {
            found = 2,
            passages = new[]
            {
                new { document = "Employee Handbook", section = "Notice period", updated = "2026-09-01", text = "The notice period for confirmed employees is 60 days. During probation it is 15 days." },
                new { document = "Leave Policy", section = "Casual leave", updated = "2026-08-15", text = "Every employee receives 12 casual leaves a year. Unused casual leave does not carry over." }
            },
            note = "These are extracts from company documents. Answer only from them, name the document, and if they do not answer the question say so."
        },
        "remember_fact" => new { saved = true, id = "0123456789ab", note = "Main project is FLP360", message = "Saved. I will use it in your future chats." },
        "list_memories" => new { count = 2, limit = 20, notes = new[] { new { id = "0123456789ab", note = "Main project is FLP360" }, new { id = "ba9876543210", note = "Prefers short answers" } } },
        "forget_memory" => new { forgotten = 1, note = "Main project is FLP360", message = "Forgotten." },
        "export_employee_dossier" => new { message = "Dossier for Vetri Selvan is ready as CSV.", file = new { fileName = "dossier.csv" } },
        _ when tool.StartsWith("prepare_", StringComparison.Ordinal) => new { form = tool["prepare_".Length..] },
        _ => new { ok = true, note = "Evaluation stand-in: the request was recorded and nothing was carried out." }
    };
}

internal static class Roles
{
    
    private static readonly Dictionary<string, string[]> ByRole = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DEVELOPER"] = ["EMPLOYEE_UPDATE_SELF", "EMPLOYEE_VIEW", "ISSUE_ASSIGN", "ISSUE_COMMENT_CREATE", "ISSUE_CREATE", "ISSUE_TRANSITION", "ISSUE_UPDATE", "ISSUE_VIEW", "PROJECT_VIEW", "TEAM_VIEW", "TIMESHEET_VIEW", "USER_VIEW", "WORKLOG_CREATE", "WORKLOG_DELETE", "WORKLOG_UPDATE", "WORKLOG_VIEW"],
        ["MANAGER"] = ["EMPLOYEE_UPDATE_SELF", "EMPLOYEE_VIEW", "EMPLOYEE_VIEW_TEAM", "ISSUE_ASSIGN", "ISSUE_COMMENT_CREATE", "ISSUE_CREATE", "ISSUE_TRANSITION", "ISSUE_UPDATE", "ISSUE_VIEW", "PROJECT_MANAGE", "PROJECT_VIEW", "TEAM_VIEW", "TIMESHEET_TEAM_VIEW", "TIMESHEET_VIEW", "USER_VIEW", "WORKLOG_CREATE", "WORKLOG_UPDATE", "WORKLOG_VIEW"],
        ["HUMAN_RESOURCE"] = ["EMPLOYEE_CREATE", "EMPLOYEE_DELETE", "EMPLOYEE_MANAGE_ORG", "EMPLOYEE_UPDATE", "EMPLOYEE_UPDATE_SELF", "EMPLOYEE_VIEW", "EMPLOYEE_VIEW_SENSITIVE", "EMPLOYEE_VIEW_TEAM", "ISSUE_VIEW", "PROJECT_VIEW", "TEAM_VIEW", "TIMESHEET_VIEW", "USER_VIEW", "WORKLOG_VIEW"],
        ["ADMIN"] = []   
    };

    public static string[] PermissionsFor(string role) => ByRole.TryGetValue(role, out var p) ? p : [];
}


internal sealed class WarningLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => level >= Microsoft.Extensions.Logging.LogLevel.Warning;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(level)) return;
        var text = formatter(state, exception);
        Console.WriteLine($"      ~ {(text.Length > 220 ? text[..220] + "..." : text)}");
    }
}

internal sealed class OfflineClientFactory : IHttpClientFactory
{
    private sealed class Refuse : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
    }

    public HttpClient CreateClient(string name) => new(new Refuse(), disposeHandler: true) { BaseAddress = new Uri("http://offline.invalid") };
}


internal sealed class StreamRecorder : IAgentProgress
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly System.Text.StringBuilder _text = new();
    private readonly List<string> _status = [];
    private int _pieces;
    private int _discards;
    private double? _firstText;

    public Task StatusAsync(string text) { _status.Add(text); return Task.CompletedTask; }

    public Task TextAsync(string delta)
    {
        _pieces++;
        _firstText ??= _clock.Elapsed.TotalSeconds;
        _text.Append(delta);
        return Task.CompletedTask;
    }

    public Task DiscardTextAsync() { _discards++; _text.Clear(); return Task.CompletedTask; }

    public string Summary(double total) =>
        $"{_status.Count} steps ({string.Join(" > ", _status)}), {_pieces} text pieces, {_discards} discarded, first text at {(_firstText is { } t ? $"{t:0.0}s" : "never")} of {total:0.0}s";

    
    public string? Problem(string finalMessage)
    {
        if (_status.Count == 0) return "streaming reported no steps";
        if (_pieces == 0) return null;   
        var streamed = _text.ToString().Trim();
        return streamed == finalMessage.Trim() ? null : $"streamed text differs from the final reply ({Short(streamed)} vs {Short(finalMessage)})";

        static string Short(string t) => t.Length <= 60 ? t : t[..60] + "...";
    }
}

internal sealed record Options(string? Filter, string? Role, int Repeat, string? Model, double MinPass, int DelayMs, string? JsonPath, string? Error, int Retries = 3, int RetryWaitSeconds = 30, bool Stream = false)
{
    public static Options Parse(string[] args)
    {
        string? filter = null, role = null, model = null, json = null;
        var repeat = 1; var min = 0.9; var delay = 3000; var retries = 3; var wait = 30; var stream = false;

        for (var i = 0; i < args.Length; i++)
        {
            string Next() => i + 1 < args.Length ? args[++i] : throw new ArgumentException($"{args[i]} needs a value.");
            try
            {
                switch (args[i])
                {
                    case "--filter": filter = Next(); break;
                    case "--role": role = Next(); break;
                    case "--model": model = Next(); break;
                    case "--json": json = Next(); break;
                    case "--stream": stream = true; break;
                    case "--repeat": repeat = Math.Clamp(int.Parse(Next()), 1, 20); break;
                    case "--delay": delay = Math.Max(0, int.Parse(Next())); break;
                    case "--retries": retries = Math.Clamp(int.Parse(Next()), 0, 10); break;
                    case "--retry-wait": wait = Math.Clamp(int.Parse(Next()), 1, 300); break;
                    case "--min-pass": min = Math.Clamp(double.Parse(Next(), System.Globalization.CultureInfo.InvariantCulture), 0, 1); break;
                    default: return new Options(null, null, 1, null, 0.9, 3000, null, $"Unknown option {args[i]}.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
            {
                return new Options(null, null, 1, null, 0.9, 3000, null, ex.Message);
            }
        }

        return new Options(filter, role, repeat, model, min, delay, json, null, retries, wait, stream);
    }
}
