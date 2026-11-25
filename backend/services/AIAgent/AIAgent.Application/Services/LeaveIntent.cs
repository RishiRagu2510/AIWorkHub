using System.Text.RegularExpressions;

namespace AIAgent.Application.Services;

public static class LeaveIntent
{
    private static readonly Regex Wants = new(
        @"\b(apply(?:ing)?|request(?:ing)?|book(?:ing)?|take|taking|need|want|raise|raising|submit(?:ting)?|file|filing)\b.{0,40}\b(leaves?|wfh|work from home|time off|day off|days off|holidays?|pto)\b" +
        @"|\b(leaves?|wfh|work from home)\b.{0,12}\b(application|request)\b" +
        @"|^\s*(?:apply|request)\s+(?:for\s+)?(?:a\s+)?(?:leaves?|wfh|work from home)\s*[.!?]*\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GivesDates = new(
        @"\d|\b(today|tomorrow|tonight|yesterday|monday|tuesday|wednesday|thursday|friday|saturday|sunday|next week|next month|this week|from|till|until|through)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AsksSomethingElse = new(
        @"\b(balance|history|status|pending|approve|approval|approved|reject|rejected|cancel|show|list|check|view|how (?:many|much|do|can|to)|policy|policies|team|reports?|who|which|when|why|left|remaining|available|used|calendar|delete|edit|change)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool WantsForm(string? message) => WantsForm(message, out _);

    
    public static bool WantsForm(string? message, out string kind)
    {
        kind = "";
        if (string.IsNullOrWhiteSpace(message) || message.Length > 160) return false;
        
        var withoutWfh = Regex.Replace(message, @"work from home", "wfh", RegexOptions.IgnoreCase);
        if (!Wants.IsMatch(message) || GivesDates.IsMatch(withoutWfh) || AsksSomethingElse.IsMatch(message)) return false;

        var text = message.ToLowerInvariant();
        kind =
            Regex.IsMatch(text, @"\bsick\b") ? "SICK" :
            Regex.IsMatch(text, @"\b(floating|casual)\b") ? "CASUAL" :
            Regex.IsMatch(text, @"\b(earned|annual|pto|vacation)\b") ? "EARNED" :
            Regex.IsMatch(text, @"\b(unpaid|lop)\b") ? "UNPAID" :
            Regex.IsMatch(text, @"\b(wfh|work from home|remote)\b") ? "WFH" : "";
        return true;
    }
}
