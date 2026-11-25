using System.Text.RegularExpressions;

namespace AIAgent.Application.Interfaces;


public record ChatAttachment(string Id, string Name, string ContentType, long Size);

public interface IChatAttachmentStore
{
    Task<ChatAttachment> SaveAsync(long tenantId, long userId, string name, string contentType, Stream content, long size, CancellationToken cancellationToken = default);

    
    Task<(ChatAttachment Info, Stream Content)?> OpenAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default);
}

public sealed class ChatAttachmentUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public static partial class ChatAttachmentRules
{
    public const long DefaultMaxBytes = 10L * 1024 * 1024;
    public const int MaxNameChars = 120;
    public const int MaxFilesPerMessage = 5;
    public const int HeadBytes = 16;
    
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".csv"] = "text/csv",
        [".md"] = "text/markdown",
        [".json"] = "application/json",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    [GeneratedRegex("^[0-9a-f]{32}$")]
    private static partial Regex IdPattern();

    public static bool IsValidId(string? id) => !string.IsNullOrEmpty(id) && IdPattern().IsMatch(id);

    public static string NewId() => Guid.NewGuid().ToString("N");

    public static string AllowedExtensions => string.Join(", ", Types.Keys.OrderBy(k => k));

    public static bool IsImage(string contentType) => contentType.StartsWith("image/", StringComparison.Ordinal);

    public static bool TryContentType(string fileName, out string contentType)
    {
        contentType = Types.GetValueOrDefault(Path.GetExtension(fileName ?? "") ?? "") ?? "";
        return contentType.Length > 0;
    }
    
    public static string SafeName(string? raw)
    {
        var name = (raw ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var cleaned = new string(name.Where(c => !char.IsControl(c) && c != '"' && c != '<' && c != '>' && c != ':' && c != '|' && c != '?' && c != '*').ToArray()).Trim().Trim('.');
        if (cleaned.Length == 0) return "file";
        if (cleaned.Length <= MaxNameChars) return cleaned;
        var ext = Path.GetExtension(cleaned);
        if (ext.Length > 10) ext = "";
        return cleaned[..(MaxNameChars - ext.Length)] + ext;
    }
    
    public static string? Validate(string fileName, long size, ReadOnlySpan<byte> head, long maxBytes)
    {
        if (size <= 0) return "That file is empty.";
        if (size > maxBytes) return $"That file is too large. The limit is {maxBytes / (1024 * 1024)} MB.";
        if (!TryContentType(fileName, out var type))
            return $"That file type is not allowed. You can attach: {AllowedExtensions}.";
        return MatchesType(Path.GetExtension(fileName).ToLowerInvariant(), type, head)
            ? null
            : "The contents of that file do not match its type.";
    }

    private static bool StartsWith(ReadOnlySpan<byte> head, params byte[] prefix) => head.Length >= prefix.Length && head[..prefix.Length].SequenceEqual(prefix);

    private static bool MatchesType(string extension, string contentType, ReadOnlySpan<byte> head) => extension switch
    {
        ".png" => StartsWith(head, 0x89, 0x50, 0x4E, 0x47),
        ".jpg" or ".jpeg" => StartsWith(head, 0xFF, 0xD8, 0xFF),
        ".gif" => StartsWith(head, (byte)'G', (byte)'I', (byte)'F', (byte)'8'),
        ".webp" => head.Length >= 12 && StartsWith(head, (byte)'R', (byte)'I', (byte)'F', (byte)'F') && head.Slice(8, 4).SequenceEqual("WEBP"u8),
        ".pdf" => StartsWith(head, (byte)'%', (byte)'P', (byte)'D', (byte)'F'),
        ".docx" or ".xlsx" or ".pptx" => StartsWith(head, 0x50, 0x4B, 0x03, 0x04),
        ".doc" or ".xls" or ".ppt" => StartsWith(head, 0xD0, 0xCF, 0x11, 0xE0),
        
        _ => head.IndexOf((byte)0) < 0,
    };
}
