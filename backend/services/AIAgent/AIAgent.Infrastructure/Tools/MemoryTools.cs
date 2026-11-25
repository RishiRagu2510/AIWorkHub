using System.Text.Json;
using AIAgent.Application.Interfaces;

namespace AIAgent.Infrastructure.Tools;


public abstract class MemoryToolBase(IUserMemoryStore store) : BaseAgentTool
{
    protected IUserMemoryStore Store => store;

    public override string Category => "General";
    public override string RequiredPermission => "";   

    protected static ToolResult Fail(string message) => new(false, null, message);

    protected static ToolResult Done(object data) => new(true, JsonSerializer.Serialize(data));

    
    protected static (long Tenant, long User)? Owner(Dictionary<string, object?> args)
    {
        var tenant = Long(args, IssueVisibility.CallerTenantIdArgument);
        var user = CallerUserId(args);
        return tenant is > 0 && user is > 0 ? (tenant.Value, user.Value) : null;
    }

    protected const string UnknownCaller = "I can't tell who you are, so I can't use your notes. Please sign in again.";
}

public sealed class RememberFactTool(IUserMemoryStore store) : MemoryToolBase(store)
{
    public override string Name => "remember_fact";
    public override string Description =>
        "Keep a short note about the user so you can use it in later chats, ONLY when the user asks you to remember something: 'remember that my main project is FLP360', 'I prefer short answers', 'from now on assume the Platform team'. " +
        "One fact per call, written briefly in the third person or as a plain statement (for example 'Main project is FLP360'). Never save something the user did not ask you to remember. " +
        "Notes cannot hold passwords, keys, long numbers or instructions; if the tool refuses, tell the user why.";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["note"] = new { type = "STRING", description = $"The fact to keep, at most {UserMemoryRules.MaxChars} characters." }
        },
        required = new[] { "note" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        if (Owner(arguments) is not { } owner) return Fail(UnknownCaller);
        var note = Str(arguments, "note");
        if (UserMemoryRules.Refusal(note) is { } refusal) return Fail(refusal);

        try
        {
            var outcome = await Store.AddAsync(owner.Tenant, owner.User, note!, cancellationToken);
            return outcome.Status switch
            {
                MemoryAddStatus.Added => Done(new { saved = true, id = outcome.Item!.Id, note = outcome.Item.Text, message = "Saved. I will use it in your future chats." }),
                MemoryAddStatus.Duplicate => Done(new { saved = false, message = "I already have that noted." }),
                _ => Fail($"I can keep {UserMemoryRules.MaxItems} notes and that is how many I have. Ask me to forget one first, or ask what I remember to pick which.")
            };
        }
        catch (UserMemoryUnavailableException ex)
        {
            return Fail(ex.Message);
        }
    }
}

public sealed class ListMemoriesTool(IUserMemoryStore store) : MemoryToolBase(store)
{
    public override string Name => "list_memories";
    public override string Description => "List the notes the user asked you to remember. Use it for 'what do you remember about me', 'show my notes' or before forgetting something.";
    public override object ParameterSchema => new { type = "OBJECT", properties = new Dictionary<string, object>() };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        if (Owner(arguments) is not { } owner) return Fail(UnknownCaller);
        try
        {
            var notes = await Store.ListAsync(owner.Tenant, owner.User, cancellationToken);
            return Done(new
            {
                count = notes.Count,
                limit = UserMemoryRules.MaxItems,
                notes = notes.Select(n => new { id = n.Id, note = n.Text }).ToArray(),
                message = notes.Count == 0 ? "I have no notes about you yet. Tell me to remember something and I will." : null
            });
        }
        catch (UserMemoryUnavailableException ex)
        {
            return Fail(ex.Message);
        }
    }
}

public sealed class ForgetMemoryTool(IUserMemoryStore store) : MemoryToolBase(store)
{
    public override string Name => "forget_memory";
    public override string Description =>
        "Remove one of the user's notes ('forget that my main project is FLP360'), or all of them when the user says to forget everything. " +
        "Pass `note` with some words from the note, or its `id` from list_memories. Set `all` to true ONLY when the user clearly asked to forget everything.";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["note"] = new { type = "STRING", description = "Some words from the note to forget." },
            ["id"] = new { type = "STRING", description = "The note's id from list_memories." },
            ["all"] = new { type = "BOOLEAN", description = "True to forget every note. Only when the user asked for that." }
        }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        if (Owner(arguments) is not { } owner) return Fail(UnknownCaller);

        try
        {
            if (Bool(arguments, "all", false))
            {
                var removed = await Store.ClearAsync(owner.Tenant, owner.User, cancellationToken);
                return Done(new { forgotten = removed, message = removed == 0 ? "There was nothing to forget." : $"Forgotten: all {removed} of your notes." });
            }

            var notes = await Store.ListAsync(owner.Tenant, owner.User, cancellationToken);
            var id = Str(arguments, "id");
            var words = Str(arguments, "note");
            if (id is null && words is null) return Fail("Which note should I forget? Say some words from it, or ask me what I remember first.");

            var matches = id is not null
                ? notes.Where(n => n.Id == id).ToList()
                : notes.Where(n => n.Text.Contains(words!, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matches.Count == 0) return Fail("I have no note like that. Ask me what I remember to see them.");
            if (matches.Count > 1)
                return Done(new { forgotten = 0, needsChoice = true, message = "More than one note matches. Which one?", matches = matches.Select(n => new { id = n.Id, note = n.Text }).ToArray() });

            await Store.DeleteAsync(owner.Tenant, owner.User, matches[0].Id, cancellationToken);
            return Done(new { forgotten = 1, note = matches[0].Text, message = "Forgotten." });
        }
        catch (UserMemoryUnavailableException ex)
        {
            return Fail(ex.Message);
        }
    }
}
