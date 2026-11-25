using System.Text.RegularExpressions;

namespace AIAgent.Application.Services;

public static class DeleteIntent
{
    private static readonly Regex Verb = new(@"\b(delete|remove|cancel|withdraw|discard|erase|get rid of)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly (string Kind, Regex Pattern)[] Kinds =
    [
        ("comments", new(@"\bcomments?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("worklog", new(@"\b(work ?logs?|time ?logs?|time entr(?:y|ies)|timesheet entr(?:y|ies)|logged (?:time|hours)|hours logged)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("leave", new(@"\b(leaves?|wfh|work from home|time off|days? off)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("skills", new(@"\bskills?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("certifications", new(@"\b(certifications?|certificates?|certs?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("education", new(@"\b(education|degrees?|qualifications?|schools?|colleges?|universit(?:y|ies))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("experience", new(@"\b(work experience|experience|previous (?:jobs?|employers?|compan(?:y|ies))|past jobs?|work history)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("languages", new(@"\blanguages?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ];
    
    private static readonly Regex SomeoneElse = new(
        @"\b(employees?|users?|people|persons?|team|teammates?|colleagues?|accounts?|someone|somebody|everyone|everybody|his|her|their)\b|\b(?!my\b)[a-z]+'s\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    
    private static readonly Regex OtherRecords = new(
        @"\b(issues?|tickets?|projects?|sprints?|attachments?|files?|chats?|conversations?|notifications?|roles?|permissions?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Generic = new(
        @"^\s*(?:please\s+)?(?:(?:i\s+(?:want|need|would like|'d like)\s+to|can you|could you|let me|help me)\s+)?(?:delete|remove|erase|discard)\s*(?:something|anything|an?\s+(?:entry|record|item)|some\s+(?:entries|records|items)|stuff|entries|records|items)?(?:\s+(?:from|in)\s+my\s+(?:profile|account))?\s*(?:please)?\s*[.!?]*\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex IssueKey = new(@"\b([a-z][a-z0-9]{1,9}-\d{1,6})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool WantsForm(string? message, out string kind, out string issueKey)
    {
        kind = "";
        issueKey = "";
        if (string.IsNullOrWhiteSpace(message) || message.Length > 160) return false;
        if (Generic.IsMatch(message)) return true;
        if (!Verb.IsMatch(message)) return false;

        foreach (var (name, pattern) in Kinds)
        {
            if (!pattern.IsMatch(message)) continue;
            if (SomeoneElse.IsMatch(message)) return false;
            if (OtherRecords.IsMatch(message) && name is not ("comments" or "worklog")) return false;
            kind = name;
            if (name == "comments" && IssueKey.Match(message) is { Success: true } key) issueKey = key.Groups[1].Value.ToUpperInvariant();
            return true;
        }

        return false;
    }

    public static bool WantsForm(string? message) => WantsForm(message, out _, out _);
}
