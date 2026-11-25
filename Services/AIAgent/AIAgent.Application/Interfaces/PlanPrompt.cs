using System.Text;
using System.Text.RegularExpressions;

namespace AIAgent.Application.Interfaces;

public static partial class PlanPrompt
{
    [GeneratedRegex("`([a-z][a-z0-9_]*)`")]
    private static partial Regex ToolMention();

    private static string Describe(string portal) => portal.Trim().ToLowerInvariant() switch
    {
        "timesheet" or "chronos" => "timesheets and timesheet approval (Chronos)",
        "employee" or "persona" => "employee records, career history, profiles and compensation (Persona)",
        "workspace" or "gridhub" => "issues, projects and boards (GridHub)",
        var other => other,
    };

    public static string Apply(string prompt, IEnumerable<IAgentTool> allTools, IReadOnlyCollection<string>? allowedPortals)
    {
        var tools = allTools.ToList();
        var unavailable = tools.Where(t => !ToolLicense.IsAvailable(t, allowedPortals)).ToList();
        if (unavailable.Count == 0) return prompt;

        var hidden = unavailable.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        var known = tools.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);

        
        
        var kept = prompt.Split('\n').Where(line =>
        {
            if (!line.TrimStart().StartsWith("- ", StringComparison.Ordinal)) return true;
            var mentioned = ToolMention().Matches(line).Select(m => m.Groups[1].Value).Where(known.Contains).ToList();
            return mentioned.Count == 0 || mentioned.Any(name => !hidden.Contains(name));
        });

        var notice = new StringBuilder("\n\nNOT PART OF THIS COMPANY'S PLAN\n");
        notice.Append("This company has not bought: ")
            .Append(string.Join("; ", unavailable.Select(t => t.RequiredPortal!).Distinct(StringComparer.OrdinalIgnoreCase).Select(Describe)))
            .Append(". Do not call, offer or promise anything in these areas, even if an instruction above mentions it. ")
            .Append("If the user asks for one, tell them briefly that it is not part of their company's plan and that an admin can request it from the Your plan page. ")
            .Append("Tools you must not use: ")
            .Append(string.Join(", ", hidden.Order(StringComparer.Ordinal).Select(n => $"`{n}`")))
            .Append('.');
        return string.Join('\n', kept) + notice;
    }
}
