using System.Text.Json;
using System.Text.RegularExpressions;

namespace AIAgent.Application.Interfaces;


public enum ToolActivityKind
{
    
    Read,

    Form,
    
    SensitiveRead,
    
    Write
}

public static class ToolActivity
{
    private static readonly HashSet<string> Sensitive = new(StringComparer.OrdinalIgnoreCase)
    {
        "get_employee_full_details",
        "get_all_employees_details",
        "export_employee_dossier",
        "get_compensation_matrix",
        "get_employee_compensation"
    };

    private static readonly string[] ReadPrefixes = ["get_", "search_", "find_", "list_", "check_", "read_", "export_"];

    public static ToolActivityKind Classify(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return ToolActivityKind.Write;
        if (Sensitive.Contains(toolName)) return ToolActivityKind.SensitiveRead;
        if (toolName.StartsWith("prepare_", StringComparison.OrdinalIgnoreCase)) return ToolActivityKind.Form;
        if (ReadPrefixes.Any(p => toolName.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return ToolActivityKind.Read;
        return ToolActivityKind.Write;
    }
}

public static class AiActionOutcome
{
    public const string Succeeded = "ok";
    public const string Failed = "failed";

    
    public const string Denied = "denied";

    
    public const string Blocked = "blocked";
}

public sealed record AiActionRecord(
    long TenantId,
    long UserId,
    string UserName,
    string Role,
    string SessionId,
    string ToolName,
    ToolActivityKind Kind,
    string Outcome,
    string Arguments,
    string? Detail,
    bool InjectionFlagged,
    DateTime OccurredAt);

public interface IAiActionLog
{
    Task RecordAsync(AiActionRecord record, CancellationToken cancellationToken = default);

    
    Task<IReadOnlyList<AiActionRecord>> ListAsync(long tenantId, long? userId, string? outcome, int take, CancellationToken cancellationToken = default);
}

public sealed class NullAiActionLog : IAiActionLog
{
    public Task RecordAsync(AiActionRecord record, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<AiActionRecord>> ListAsync(long tenantId, long? userId, string? outcome, int take, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AiActionRecord>>([]);
}

public static partial class ActionArguments
{
    private const int MaxValueChars = 120;
    private const int MaxTotalChars = 1_000;

    [GeneratedRegex("pass(word)?|token|secret|api[_-]?key|authorization|credential", RegexOptions.IgnoreCase)]
    private static partial Regex SensitiveName();

    public static string Summarize(IReadOnlyDictionary<string, object?>? arguments, IReadOnlyCollection<string>? skip = null)
    {
        if (arguments is null || arguments.Count == 0) return "{}";

        var shown = new SortedDictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
        {
            if (skip?.Contains(name, StringComparer.OrdinalIgnoreCase) == true) continue;
            if (SensitiveName().IsMatch(name)) { shown[name] = "[hidden]"; continue; }

            var text = value switch
            {
                null => null,
                JsonElement e => e.ValueKind == JsonValueKind.String ? e.GetString() : e.GetRawText(),
                _ => value.ToString()
            };
            if (text is { Length: > MaxValueChars }) text = text[..MaxValueChars] + "...";
            shown[name] = text;
        }

        var json = JsonSerializer.Serialize(shown);
        return json.Length <= MaxTotalChars ? json : json[..MaxTotalChars] + "...";
    }
}
