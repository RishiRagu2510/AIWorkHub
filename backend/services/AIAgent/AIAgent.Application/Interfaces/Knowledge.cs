using System.Text.RegularExpressions;

namespace AIAgent.Application.Interfaces;


public record KnowledgeDocument(
    long Id,
    string Title,
    string FileName,
    IReadOnlyList<string> AllowedRoles,
    long UploadedBy,
    DateTimeOffset UpdatedAt,
    int Version,
    int Passages,
    int Characters);


public record KnowledgeDocumentDetail(
    long Id,
    string Title,
    string FileName,
    IReadOnlyList<string> AllowedRoles,
    long UploadedBy,
    DateTimeOffset UpdatedAt,
    int Version,
    int Passages,
    int Characters,
    string Content);


public record KnowledgePassage(
    long DocumentId,
    string Title,
    string? Section,
    string Text,
    double Rank,
    int Version,
    DateTimeOffset UpdatedAt);


public record NewKnowledgeDocument(
    string Title,
    string FileName,
    string ContentType,
    IReadOnlyList<string> AllowedRoles,
    string Text,
    IReadOnlyList<KnowledgeChunk> Chunks);

public record KnowledgeChunk(string? Heading, string Text);

public enum KnowledgeSaveStatus { Added, Replaced, Unchanged, Full }

public record KnowledgeSaveOutcome(KnowledgeSaveStatus Status, KnowledgeDocument? Document);


public interface IKnowledgeStore
{
    
    Task<KnowledgeSaveOutcome> SaveAsync(long tenantId, long userId, NewKnowledgeDocument document, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeDocument>> ListAsync(long tenantId, CancellationToken cancellationToken = default);

    Task<KnowledgeDocumentDetail?> GetAsync(long tenantId, long documentId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(long tenantId, long documentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgePassage>> SearchAsync(long tenantId, IReadOnlyCollection<string> roles, bool seesAll, string query, int limit, CancellationToken cancellationToken = default);
}





public interface IKnowledgeEmbedder
{
    bool Enabled { get; }
    string Model { get; }
    int Dimensions { get; }

    
    Task<float[]?> EmbedQueryAsync(string text, CancellationToken cancellationToken = default);

    
    Task<IReadOnlyList<float[]?>> EmbedPassagesAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}


public sealed class NoKnowledgeEmbedder : IKnowledgeEmbedder
{
    public bool Enabled => false;
    public string Model => "none";
    public int Dimensions => 0;
    public Task<float[]?> EmbedQueryAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<float[]?>(null);
    public Task<IReadOnlyList<float[]?>> EmbedPassagesAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<float[]?>>(texts.Select(_ => (float[]?)null).ToArray());
}


public interface IKnowledgeIndexer
{
    
    Task<int> IndexPendingAsync(int max, CancellationToken cancellationToken = default);
}


public sealed class KnowledgeUnavailableException(string message, Exception? inner = null) : Exception(message, inner);


public static partial class KnowledgeRules
{
    public const int MaxFileBytes = 10 * 1024 * 1024;
    public const int MaxTitleChars = 200;
    public const int MaxTextChars = 1_500_000;
    public const int MaxDocuments = 200;
    public const int MaxRoles = 10;
    public const int MaxPassages = 5;
    public const int MaxQueryChars = 300;

    public static readonly string[] Extensions = [".pdf", ".docx", ".txt", ".md"];

    
    private static readonly HashSet<string> Managers = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMIN", "HR", "HR_ADMIN", "HUMAN_RESOURCE", "HUMAN_RESOURCES"
    };

    public static bool CanManage(IEnumerable<string> roles) => roles.Any(r => Managers.Contains(r.Trim()));

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,49}$")]
    private static partial Regex RoleShape();

    
    public static (IReadOnlyList<string> Roles, string? Problem) NormalizeRoles(IEnumerable<string>? requested)
    {
        var roles = new List<string>();
        foreach (var raw in requested ?? [])
        {
            foreach (var part in (raw ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var code = part.ToUpperInvariant();
                if (code is "EVERYONE" or "ALL") return ([], null);
                if (!RoleShape().IsMatch(code)) return ([], $"'{part}' is not a role name.");
                if (!roles.Contains(code)) roles.Add(code);
            }
        }
        return roles.Count > MaxRoles ? ([], $"Choose at most {MaxRoles} roles.") : (roles, null);
    }

    public static string? TitleProblem(string? title)
    {
        var t = (title ?? "").Trim();
        if (t.Length == 0) return "Give the document a title.";
        if (t.Length > MaxTitleChars) return $"Keep the title under {MaxTitleChars} characters.";
        return t.Any(char.IsControl) ? "The title cannot contain control characters." : null;
    }

    
    public static bool ExtensionAllowed(string fileName) =>
        Extensions.Contains(Path.GetExtension(fileName ?? "").ToLowerInvariant());

    public static string TypeName(string fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".md" => "text/markdown",
        _ => "text/plain"
    };
}
