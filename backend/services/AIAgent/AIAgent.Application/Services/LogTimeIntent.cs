using System.Text.RegularExpressions;

namespace AIAgent.Application.Services;

public static class LogTimeIntent
{
    private static readonly Regex Wants = new(
        @"\b(fill(?:ing)?|log(?:ging)?|enter(?:ing)?|record(?:ing)?|book(?:ing)?|add(?:ing)?|put(?:ting)?)\b.{0,40}\b(time ?sheets?|time|hours|work ?logs?|work)\b" +
        @"|\b(time ?sheets?|work ?logs?)\b.{0,25}\b(fill|log|enter|record|add)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GivesDuration = new(
        @"\d+(?:[.,]\d+)?\s*(?:h|hr|hrs|hours?|m|min|mins|minutes?)\b|\b\d{1,2}:\d{2}\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AsksSomethingElse = new(
        @"\b(history|summary|summar\w*|missing|approve|approval|reject|review|pending|show|view|check|list|how (?:many|much)|total|status|download|export|submitted?|delete|remove|edit|change)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool WantsForm(string? message)
    {
        if (string.IsNullOrWhiteSpace(message) || message.Length > 200) return false;
        return Wants.IsMatch(message) && !GivesDuration.IsMatch(message) && !AsksSomethingElse.IsMatch(message);
    }
}
