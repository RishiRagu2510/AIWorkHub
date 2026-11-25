namespace Jira.Application.Services;
public static class WorkflowRules
{
    public static readonly string[] Steps = ["TODO", "IN_PROGRESS", "CODE_REVIEW", "TESTING", "DONE"];
    public static void Validate(string current, string target, bool confirmed)
    {
        target = target.Trim().ToUpperInvariant();
        if (!Steps.Contains(target) && target is not "BLOCKED" and not "CANCELLED") throw new ArgumentException("Unknown issue status.");
        if (current == target) return;
        var index = Array.IndexOf(Steps, current);
        if (index >= 0 && index + 1 < Steps.Length && Steps[index + 1] == target) return;
        if (!confirmed) throw new ArgumentException("Confirm skipping workflow stages before making this transition.");
    }
}
