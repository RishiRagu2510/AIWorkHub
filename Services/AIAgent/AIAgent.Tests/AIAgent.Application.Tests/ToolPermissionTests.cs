using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Tools;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AIAgent.Application.Tests;

public class ToolPermissionTests
{
    private static List<IAgentTool> AllTools()
    {
        var factory = new FakeHttpClientFactory(new RecordingHandler(_ => Http.Json("{}")));
        var accessor = new HttpContextAccessor();
        return typeof(BaseAgentTool).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(IAgentTool).IsAssignableFrom(t))
            .Select(t => (IAgentTool)(t.GetConstructors()[0].GetParameters().Any(p => p.ParameterType == typeof(IUserMemoryStore))
                ? Activator.CreateInstance(t, new FakeMemoryStore())!
                : t.GetConstructors()[0].GetParameters().Any(p => p.ParameterType == typeof(IKnowledgeStore))
                    ? Activator.CreateInstance(t, new FakeKnowledgeStore())!
                    : Activator.CreateInstance(t, factory, accessor)!))
            .ToList();
    }

    [Fact]
    public void EveryToolThatReadsPrivateHrData_NeedsThePermissionOnlyHrAndAdminHold()
    {
        
        
        
        var sensitive = AllTools().Where(t => ToolActivity.Classify(t.Name) == ToolActivityKind.SensitiveRead).ToList();

        Assert.NotEmpty(sensitive);
        Assert.All(sensitive, tool => Assert.Equal("EMPLOYEE_VIEW_SENSITIVE", tool.RequiredPermission));
    }

    [Fact]
    public void TheDossierExportIsNotOfferedToEveryoneWhoCanViewEmployees() =>
        Assert.Equal("EMPLOYEE_VIEW_SENSITIVE", AllTools().Single(t => t.Name == "export_employee_dossier").RequiredPermission);

    [Fact]
    public void EveryToolHasANameADescriptionAndAUniqueName()
    {
        var tools = AllTools();
        Assert.All(tools, t => { Assert.False(string.IsNullOrWhiteSpace(t.Name)); Assert.False(string.IsNullOrWhiteSpace(t.Description)); });
        Assert.Equal(tools.Count, tools.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void NoToolIsLeftUnclassified_SoTheAuditLogNeverCallsALookupAWriteByMistake()
    {
        
        
        var writes = AllTools().Where(t => ToolActivity.Classify(t.Name) == ToolActivityKind.Write).Select(t => t.Name).OrderBy(n => n).ToArray();

        Assert.Equal(
            ["add_issue_comment", "apply_leave", "approve_leave_request", "approve_timesheet_period", "assign_issue", "create_issue", "forget_memory", "log_work", "remember_fact", "submit_timesheet", "transition_issue", "update_issue", "update_my_profile", "update_work_log"],
            writes);
    }
}
