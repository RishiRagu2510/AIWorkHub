using System.Text;

namespace AIAgent.Application.Interfaces;

public interface IAgentProgress
{
    
    Task StatusAsync(string text);

    Task TextAsync(string delta);

    Task DiscardTextAsync();
}

public sealed class NullAgentProgress : IAgentProgress
{
    public static readonly NullAgentProgress Instance = new();
    public Task StatusAsync(string text) => Task.CompletedTask;
    public Task TextAsync(string delta) => Task.CompletedTask;
    public Task DiscardTextAsync() => Task.CompletedTask;
}


public static class ToolStatus
{
    private static readonly Dictionary<string, string> Named = new(StringComparer.OrdinalIgnoreCase)
    {
        ["read_attachment"] = "Reading your files",
    };

    private static readonly (string Prefix, string Verb)[] Verbs =
    [
        ("get_", "Looking up"),
        ("list_", "Looking up"),
        ("search_", "Searching"),
        ("find_", "Searching"),
        ("check_", "Checking"),
        ("read_", "Reading"),
        ("prepare_", "Preparing"),
        ("create_", "Creating"),
        ("apply_", "Applying"),
        ("approve_", "Approving"),
        ("transition_", "Moving"),
        ("update_", "Updating"),
        ("add_", "Adding"),
        ("log_", "Logging"),
        ("export_", "Exporting"),
        ("submit_", "Submitting"),
    ];

    public static string For(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName)) return "Working on it";
        if (Named.TryGetValue(toolName, out var named)) return named;

        foreach (var (prefix, verb) in Verbs)
        {
            if (!toolName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = Humanize(toolName[prefix.Length..]);
            return rest.Length == 0 ? verb : $"{verb} {rest}";
        }
        return $"Running {Humanize(toolName)}";
    }

    private static string Humanize(string name)
    {
        var text = new StringBuilder();
        foreach (var ch in name) text.Append(ch == '_' ? ' ' : char.ToLowerInvariant(ch));
        return text.ToString().Trim();
    }
}
