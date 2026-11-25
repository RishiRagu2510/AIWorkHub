using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AIAgent.Application.Interfaces;

public record ChatSummary(string Id, string Title, long UpdatedAt, int MessageCount);

public record StoredMessage(string Role, string Content, string? Time, string? ExtraJson);

public record StoredChat(string Id, string Title, long UpdatedAt, IReadOnlyList<StoredMessage> Messages);

public interface IChatHistoryStore
{
    
    Task<string?> GetConsentAsync(long tenantId, long userId, CancellationToken cancellationToken = default);

    Task SetConsentAsync(long tenantId, long userId, string value, CancellationToken cancellationToken = default);

    
    Task<IReadOnlyList<ChatSummary>> ListAsync(long tenantId, long userId, CancellationToken cancellationToken = default);

    Task<StoredChat?> GetAsync(long tenantId, long userId, string chatId, CancellationToken cancellationToken = default);

    
    Task SaveAsync(long tenantId, long userId, StoredChat chat, int maxChats, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long tenantId, long userId, string chatId, CancellationToken cancellationToken = default);

    
    Task<long> ClearAsync(long tenantId, long userId, CancellationToken cancellationToken = default);
}

public sealed class ChatHistoryUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public static partial class ChatHistoryRules
{
    public const int MaxChats = 30;
    public const int MaxMessages = 80;
    public const int MaxContentChars = 20_000;
    public const int MaxExtraChars = 120_000;
    public const int MaxTitleChars = 120;
    public const int MaxChatIdChars = 64;

    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex IdPattern();

    public static bool IsValidChatId(string? id) => !string.IsNullOrEmpty(id) && IdPattern().IsMatch(id);

    public static bool IsValidConsent(string? value) => value is "yes" or "no";

    public static StoredChat? TryBuild(string chatId, string? title, long? updatedAt, IReadOnlyList<JsonElement>? messages, long nowMs, out string error)
    {
        error = "";
        if (!IsValidChatId(chatId)) { error = "The chat id must be 1 to 64 letters, digits, dashes or underscores."; return null; }
        if (messages is null || messages.Count == 0) { error = "A chat needs at least one message."; return null; }
        if (messages.Count > MaxMessages) { error = $"A chat can hold at most {MaxMessages} messages."; return null; }

        var stored = new List<StoredMessage>(messages.Count);
        foreach (var message in messages)
        {
            if (message.ValueKind != JsonValueKind.Object) { error = "Each message must be an object."; return null; }

            var role = message.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null;
            if (role is not ("user" or "assistant")) { error = "Each message needs a role of user or assistant."; return null; }

            var content = message.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
            if (content.Length > MaxContentChars) { error = $"A message is longer than {MaxContentChars} characters."; return null; }

            var time = message.TryGetProperty("time", out var t) && t.ValueKind == JsonValueKind.String ? Truncate(t.GetString(), 32) : null;

            var extra = new JsonObject();
            foreach (var property in message.EnumerateObject())
            {
                if (property.Name is "role" or "content" or "time" or "restored") continue;
                extra[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
            var extraJson = extra.Count == 0 ? null : extra.ToJsonString();
            if (extraJson is { Length: > MaxExtraChars }) { error = "A message carries too much data to save."; return null; }

            stored.Add(new StoredMessage(role, content, time, extraJson));
        }

        var name = (title ?? "").Trim();
        if (name.Length == 0) name = stored.FirstOrDefault(m => m.Role == "user" && m.Content.Trim().Length > 0)?.Content.Trim() ?? "New chat";
        name = Truncate(Regex.Replace(name, @"\s+", " "), MaxTitleChars)!;

        
        var stamp = updatedAt is > 0 && updatedAt <= nowMs + 86_400_000 ? updatedAt.Value : nowMs;
        return new StoredChat(chatId, name, stamp, stored);
    }

    public static JsonObject ToClientMessage(StoredMessage message)
    {
        var result = new JsonObject();
        if (message.ExtraJson is not null && JsonNode.Parse(message.ExtraJson) is JsonObject extra)
        {
            foreach (var (name, value) in extra.ToList())
            {
                extra.Remove(name);
                result[name] = value;
            }
        }
        result["role"] = message.Role;
        result["content"] = message.Content;
        if (message.Time is not null) result["time"] = message.Time;
        return result;
    }

    private static string? Truncate(string? text, int max) => text is null || text.Length <= max ? text : text[..max];
}
