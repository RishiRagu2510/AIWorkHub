using System.Text.RegularExpressions;

namespace AIAgent.Application.Services;

public static partial class ModelChoice
{
    public const string Auto = "auto";

    
    public const string Lite = "gemini-flash-lite-latest";

    private const int MaxLength = 90;
    private const int MinWords = 3;

    public static bool IsAuto(string? model) => string.Equals(model?.Trim(), Auto, StringComparison.OrdinalIgnoreCase);

    
    public static string? Resolve(string? message) => IsSimpleLookup(message) ? Lite : null;

    public static bool IsSimpleLookup(string? message)
    {
        var text = (message ?? "").Trim();
        if (text.Length is 0 or > MaxLength) return false;
        if (text.Contains('\n') || text.Contains("http", StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < MinWords) return false;
        if (text.TrimEnd('?', '.', '!').IndexOfAny(['?', '.', ';', '!']) >= 0) return false;   
        return Lookup().IsMatch(text) && !NotSimple().IsMatch(text);
    }

    
    [GeneratedRegex(@"^\s*(please\s+)?(show|list|what|what's|whats|who|who's|whose|which|when|where|how\s+many|how\s+much|do\s+i|am\s+i|is\s+there|are\s+there|any|check|find|get|tell\s+me|display|see)\b", RegexOptions.IgnoreCase)]
    private static partial Regex Lookup();

    
    
    [GeneratedRegex(@"\b(about|it|that|those|them|these|this|he|she|him|her|his|same|again|other|another|else|more|instead|create|add|make|assign|reassign|move|transition|update|change|edit|set|delete|remove|log|apply|approve|reject|cancel|submit|send|post|comment|reply|export|download|excel|xlsx|report|spreadsheet|compare|summari[sz]e|analy[sz]e|plan|why|explain|recommend|suggest|draft|write|and|then|also|after|before|between|versus|vs|if|team|everyone|everybody|all\s+employees|salary|salaries|ctc|compensation|payroll|aadhaa?r|password|token)\b", RegexOptions.IgnoreCase)]
    private static partial Regex NotSimple();
}
