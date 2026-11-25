using System.Text.RegularExpressions;

namespace AIAgent.Application.Services;

public static class CreateIssueIntent
{
    private static readonly Regex Wants = new(
        @"^\W*(?:(?:please|pls|can you|could you|can u|kindly|athenna|hey|hi|i need to|i want to|i would like to|i'd like to|i wanna|we need to|let's|lets)\W+)*(?:create|raise|file|open|make|add|log|new)\s+(?:(?:a|an|the|new|one|another)\s+)*(?:issue|ticket|task|bug|story)s?\b(?<rest>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex NamesAnIssue = new(@"\b[A-Za-z][A-Za-z0-9]{1,9}-\d+\b|#\d+", RegexOptions.Compiled);

    private static readonly Regex Skips = new(
        @"\b(employee|onboard\w*|how|why|what|which|where|explain|status|show|list|search|find|count|existing|already|assigned|history)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LeadIn = new(@"^\W*(?:for|to|about|on|regarding|that|:|-)\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    
    private static readonly Regex NamesProject = new(
        @"^(?:in|for|on|under|inside)\s+(?:the\s+)?(?<project>.+?)\s+project\b\W*(?<tail>.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    public static bool WantsForm(string? message, out string summary) => WantsForm(message, out summary, out _);

    public static bool WantsForm(string? message, out string summary, out string project)
    {
        summary = "";
        project = "";
        if (string.IsNullOrWhiteSpace(message) || message.Length > 300) return false;
        var match = Wants.Match(message.Trim());
        if (!match.Success || NamesAnIssue.IsMatch(message) || Skips.IsMatch(message)) return false;

        var rest = match.Groups["rest"].Value.Trim();
        if (NamesProject.Match(rest) is { Success: true } named)
        {
            project = named.Groups["project"].Value.Trim();
            rest = named.Groups["tail"].Value.Trim();
        }
        rest = LeadIn.Replace(rest, "").Trim().TrimEnd('.', '!', '?').Trim();
        summary = rest.Length > 0 ? char.ToUpperInvariant(rest[0]) + rest[1..] : "";
        return true;
    }
}
