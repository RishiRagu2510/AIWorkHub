using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AIAgent.Application.Services;

namespace AIAgent.Application.Interfaces;

public record MemoryItem(string Id, string Text, long CreatedAt);

public enum MemoryAddStatus { Added, Duplicate, Full }

public record MemoryAddOutcome(MemoryAddStatus Status, MemoryItem? Item);

public interface IUserMemoryStore
{
    
    Task<IReadOnlyList<MemoryItem>> ListAsync(long tenantId, long userId, CancellationToken cancellationToken = default);

    
    Task<MemoryAddOutcome> AddAsync(long tenantId, long userId, string text, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default);

    
    Task<long> ClearAsync(long tenantId, long userId, CancellationToken cancellationToken = default);
}


public sealed class UserMemoryUnavailableException(string message, Exception? inner = null) : Exception(message, inner);


public static partial class UserMemoryRules
{
    public const int MaxItems = 20;
    public const int MaxChars = 200;

    [GeneratedRegex(@"^[0-9a-f]{12}$")]
    private static partial Regex IdPattern();

    [GeneratedRegex(@"\b(password|passwd|passcode|secret|api[\s_-]?key|access[\s_-]?key|token|otp|cvv|pin\s*(number|code)?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SecretWord();

    [GeneratedRegex(@"\d(?:[\s-]?\d){8,}")]
    private static partial Regex LongNumber();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    public static bool IsValidId(string? id) => !string.IsNullOrEmpty(id) && IdPattern().IsMatch(id);

    
    public static string Normalize(string? text) => Spaces().Replace(text ?? "", " ").Trim().TrimEnd('.', ' ');

    
    public static string Key(string text) => Normalize(text).ToLowerInvariant();

    
    public static string IdFor(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Key(text))))[..12].ToLowerInvariant();

    
    public static string? Refusal(string? text)
    {
        var note = Normalize(text);
        if (note.Length == 0) return "There is nothing to remember. Tell me what to keep, for example \"remember that my main project is FLP360\".";
        if (note.Length > MaxChars) return $"That note is too long to keep ({note.Length} characters; the limit is {MaxChars}). Say it more briefly.";
        if (SecretWord().IsMatch(note) || LongNumber().IsMatch(note))
            return "I don't keep passwords, keys, tokens or long numbers (ID, card or phone numbers). Notes are for preferences and facts about your work.";
        if (PromptInjectionGuard.Scan(note) != null)
            return "I can't keep a note that gives me instructions. Notes are facts about you, such as which project you work on.";
        return null;
    }
}
