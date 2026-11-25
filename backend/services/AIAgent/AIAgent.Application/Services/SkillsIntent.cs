using System.Text.RegularExpressions;

namespace AIAgent.Application.Services;

public static class SkillsIntent
{
    private static readonly Regex Wants = new(
        @"^\s*(?:please\s+)?(?:(?:i\s+(?:want|need|would like|'d like)\s+to|can you|could you|let me|help me|i\s+wanna)\s+)?" +
        @"(?:update|edit|change|manage|modify|add|maintain|revise)\s+" +
        @"(?:(?:my|some|a|an|the|new|more|another|technical|tech)\s+)*skills?\s*(?:please)?\s*[.!?]*\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool WantsForm(string? message) => !string.IsNullOrWhiteSpace(message) && message.Length <= 80 && Wants.IsMatch(message);
}
