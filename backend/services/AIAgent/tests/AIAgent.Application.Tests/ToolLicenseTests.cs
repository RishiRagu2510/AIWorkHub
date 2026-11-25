using System.Reflection;
using System.Runtime.CompilerServices;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Domain.Models;
using Xunit;

namespace AIAgent.Application.Tests;






public sealed class ToolLicenseTests
{
    private sealed class PortalTool(string name, string? portal) : IAgentTool
    {
        public string Name => name;
        public string Description => name;
        public string Category => "Test";
        public string RequiredPermission => "";
        public string? RequiredPortal => portal;
        public object ParameterSchema => new { };
        public int Runs { get; private set; }

        public Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
        {
            Runs++;
            return Task.FromResult(new ToolResult(true, "{}"));
        }
    }

    private sealed class Identity(VerifiedCaller? caller) : ICallerIdentityProvider
    {
        public bool HasCredentials => true;
        public Task<VerifiedCaller?> VerifyAsync(CancellationToken cancellationToken = default) => Task.FromResult(caller);
    }

    private static VerifiedCaller Company(params string[]? portals) => new(7, "DEVELOPER", ["DEVELOPER"], [], 1, null, portals);

    

    [Theory]
    [InlineData(null, "timesheet", true)]                          
    [InlineData("workspace,employee", "timesheet", false)]         
    [InlineData("workspace,employee", "employee", true)]
    [InlineData("workspace", "employee", false)]
    [InlineData("workspace,chronos", "timesheet", true)]           
    [InlineData("workspace,persona", "employee", true)]
    [InlineData("workspace", null, true)]                          
    public void AToolIsAvailableOnlyWhenItsPortalIsInThePlan(string? plan, string? portal, bool expected) =>
        Assert.Equal(expected, ToolLicense.IsAvailable(new PortalTool("t", portal), plan?.Split(',')));

    

    private static async Task<(AgentChatResponse Response, string[] Offered, PortalTool Timesheets, PortalTool Shared)> Ask(VerifiedCaller caller, bool modelCallsTimesheetTool = false)
    {
        var timesheets = new PortalTool("get_timesheet_summary", "timesheet");
        var shared = new PortalTool("log_work", null);
        string[] offered = [];
        var step = 0;
        var llm = new MockLlmProvider
        {
            OnChat = (_, tools) =>
            {
                offered = tools?.Select(t => t.Name).ToArray() ?? [];
                return ++step == 1 && modelCallsTimesheetTool
                    ? new LlmChatResponse { ToolCalls = [new ToolCall { Name = "get_timesheet_summary", ArgumentsJson = "{}" }] }
                    : new LlmChatResponse { Content = "ok" };
            },
        };
        var response = await new AthennaAgentOrchestrator(llm, [timesheets, shared], callerIdentity: new Identity(caller))
            .ExecuteChatAsync(new AgentChatRequest("How many hours did I log?"));
        return (response, offered, timesheets, shared);
    }

    [Fact]
    public async Task ACompanyWithoutChronos_IsNotOfferedTheTimesheetTools_ButKeepsTheSharedOnes()
    {
        var (_, offered, _, _) = await Ask(Company("workspace", "employee"));
        Assert.Equal(["log_work"], offered);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("workspace,employee,timesheet")]
    public async Task ACompanyWithChronos_OrNoPlan_IsOfferedEverything(string? plan)
    {
        var (_, offered, _, _) = await Ask(Company(plan?.Split(',')));
        Assert.Equal(["get_timesheet_summary", "log_work"], offered.Order().ToArray());
    }

    [Fact]
    public async Task AModelThatNamesAToolTheCompanyDoesNotHave_IsRefused_AndNothingRuns()
    {
        var (response, _, timesheets, _) = await Ask(Company("workspace", "employee"), modelCallsTimesheetTool: true);

        Assert.Equal(0, timesheets.Runs);
        var action = Assert.Single(response.ExecutedActions!);
        Assert.False(action.Success);
        Assert.Contains("not part of your company's plan", action.Result?.ToString());
    }

    [Fact]
    public async Task AModelCallingAToolTheCompanyHas_RunsIt()
    {
        var (response, _, timesheets, _) = await Ask(Company("workspace", "timesheet"), modelCallsTimesheetTool: true);
        Assert.Equal(1, timesheets.Runs);
        Assert.True(Assert.Single(response.ExecutedActions!).Success);
    }

    

    private static async Task<string> PromptSeenBy(VerifiedCaller caller)
    {
        var tools = new IAgentTool[]
        {
            new PortalTool("get_timesheet_history", "timesheet"), new PortalTool("submit_timesheet", "timesheet"),
            new PortalTool("get_work_logs", null), new PortalTool("log_work", null),
        };
        string prompt = "";
        var llm = new MockLlmProvider { OnChat = (messages, _) => { prompt = messages[0].Content ?? ""; return new LlmChatResponse { Content = "ok" }; } };
        await new AthennaAgentOrchestrator(llm, tools, callerIdentity: new Identity(caller)).ExecuteChatAsync(new AgentChatRequest("hello"));
        return prompt;
    }

    [Fact]
    public async Task ForACompanyWithoutChronos_TheInstructionsStopTellingTheModelToUseTimesheetTools()
    {
        var prompt = await PromptSeenBy(Company("workspace", "employee"));

        Assert.Contains("NOT PART OF THIS COMPANY'S PLAN", prompt);
        Assert.Contains("timesheets and timesheet approval (Chronos)", prompt);
        
        var notice = prompt[prompt.IndexOf("NOT PART OF THIS COMPANY'S PLAN", StringComparison.Ordinal)..];
        var before = prompt[..prompt.IndexOf("NOT PART OF THIS COMPANY'S PLAN", StringComparison.Ordinal)];
        Assert.DoesNotContain("call `submit_timesheet`", before);
        Assert.Contains("`submit_timesheet`", notice);
        Assert.Contains("`get_timesheet_history`", notice);
        
        Assert.Contains("`get_work_logs`", before);
        Assert.DoesNotContain("`log_work`", notice);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("workspace,employee,timesheet")]
    public async Task ForACompanyWithChronos_OrNoPlan_TheInstructionsAreUnchanged(string? plan)
    {
        var prompt = await PromptSeenBy(Company(plan?.Split(',')));

        Assert.DoesNotContain("NOT PART OF THIS COMPANY'S PLAN", prompt);
        Assert.Contains("call `submit_timesheet`", prompt);
        Assert.Contains("call `get_timesheet_history`", prompt);
    }

    [Fact]
    public void ABulletAboutOnlyUnavailableTools_IsDropped_AndOneAboutSharedToolsStays()
    {
        IAgentTool[] tools = [new PortalTool("submit_timesheet", "timesheet"), new PortalTool("log_work", null)];
        const string prompt = "intro\n- Submit my week: call `submit_timesheet`.\n- Log time: call `log_work`.\n- Mixed: `log_work` or `submit_timesheet`.\nend";

        var result = PlanPrompt.Apply(prompt, tools, ["workspace"]);

        Assert.DoesNotContain("- Submit my week", result);
        Assert.Contains("- Log time: call `log_work`.", result);
        Assert.Contains("- Mixed:", result);
        Assert.Contains("intro", result);
        Assert.Contains("end", result);
    }

    [Fact]
    public void WhenNothingIsMissingFromThePlan_ThePromptIsReturnedAsIs() =>
        Assert.Equal("anything `submit_timesheet`", PlanPrompt.Apply("anything `submit_timesheet`", [new PortalTool("submit_timesheet", "timesheet")], null));

    

    [Fact]
    public async Task TheHomeNudges_DoNotRunTimesheetToolsForACompanyWithoutChronos()
    {
        var history = new PortalTool("get_timesheet_history", "timesheet");
        await new NudgeService([history], new Identity(Company("workspace", "employee")), null, () => new DateOnly(2026, 11, 11)).GetAsync();
        Assert.Equal(0, history.Runs);

        await new NudgeService([history], new Identity(Company("workspace", "timesheet")), null, () => new DateOnly(2026, 11, 11)).GetAsync();
        Assert.True(history.Runs > 0);
    }

    

    private static IEnumerable<IAgentTool> RealTools() =>
        typeof(AIAgent.Infrastructure.Tools.BaseAgentTool).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(IAgentTool).IsAssignableFrom(t) && t.Namespace == "AIAgent.Infrastructure.Tools")
            .Select(t => (IAgentTool)RuntimeHelpers.GetUninitializedObject(t));

    [Fact]
    public void TheToolsThatNeedAPortal_SayWhichOne()
    {
        var actual = RealTools().Where(t => t.RequiredPortal is not null).ToDictionary(t => t.Name, t => t.RequiredPortal);
        Assert.Equal(new Dictionary<string, string?>
        {
            ["get_timesheet_history"] = "timesheet", ["approve_timesheet_period"] = "timesheet", ["get_timesheet_summary"] = "timesheet", ["submit_timesheet"] = "timesheet",
            ["prepare_skills"] = "employee", ["get_career_history"] = "employee", ["update_my_profile"] = "employee", ["prepare_profile_section"] = "employee",
            ["get_employee_record"] = "employee", ["export_employee_dossier"] = "employee", ["get_compensation_matrix"] = "employee", ["get_employee_compensation"] = "employee",
            ["get_all_employees_details"] = "employee", ["get_employee_profile"] = "employee", ["get_employee_full_details"] = "employee", ["prepare_delete"] = "employee",
        }.OrderBy(p => p.Key), actual.OrderBy(p => p.Key));
    }

    [Theory]
    [InlineData("log_work")]                
    [InlineData("get_work_logs")]
    [InlineData("update_work_log")]
    [InlineData("export_work_log_report")]
    [InlineData("prepare_work_log")]
    [InlineData("apply_leave")]             
    [InlineData("prepare_leave")]
    [InlineData("check_employee_leaves")]
    [InlineData("get_company_holidays")]
    [InlineData("find_people")]             
    [InlineData("search_issues")]
    [InlineData("create_issue")]
    [InlineData("search_company_docs")]
    public void SharedTools_AreAvailableToEveryCompany(string name) =>
        Assert.Null(RealTools().Single(t => t.Name == name).RequiredPortal);
}
