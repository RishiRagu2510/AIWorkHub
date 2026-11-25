using AIAgent.Application.Interfaces;

namespace AIAgent.Application.Tests;


public sealed class FakeKnowledgeStore : IKnowledgeStore
{
    public bool Down { get; set; }
    public List<KnowledgePassage> Passages { get; } = [];
    public List<(long Tenant, long User, NewKnowledgeDocument Document)> Saved { get; } = [];
    public List<KnowledgeDocument> Documents { get; } = [];
    public KnowledgeSaveStatus SaveStatus { get; set; } = KnowledgeSaveStatus.Added;
    public (long Tenant, IReadOnlyCollection<string> Roles, bool SeesAll, string Query, int Limit)? LastSearch { get; private set; }
    public int Searches { get; private set; }
    public List<(long Tenant, long Id)> Deleted { get; } = [];

    private void Check()
    {
        if (Down) throw new KnowledgeUnavailableException("The company documents can't be reached right now.");
    }

    public Task<KnowledgeSaveOutcome> SaveAsync(long tenantId, long userId, NewKnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        Check();
        Saved.Add((tenantId, userId, document));
        var saved = new KnowledgeDocument(Saved.Count, document.Title, document.FileName, document.AllowedRoles, userId, DateTimeOffset.UtcNow, 1, document.Chunks.Count, document.Text.Length);
        return Task.FromResult(new KnowledgeSaveOutcome(SaveStatus, SaveStatus == KnowledgeSaveStatus.Full ? null : saved));
    }

    public Task<IReadOnlyList<KnowledgeDocument>> ListAsync(long tenantId, CancellationToken cancellationToken = default)
    {
        Check();
        return Task.FromResult<IReadOnlyList<KnowledgeDocument>>(Documents.ToList());
    }

    public Task<KnowledgeDocumentDetail?> GetAsync(long tenantId, long documentId, CancellationToken cancellationToken = default)
    {
        Check();
        var doc = Documents.FirstOrDefault(d => d.Id == documentId);
        if (doc is null) return Task.FromResult<KnowledgeDocumentDetail?>(null);
        return Task.FromResult<KnowledgeDocumentDetail?>(new KnowledgeDocumentDetail(
            doc.Id, doc.Title, doc.FileName, doc.AllowedRoles, doc.UploadedBy, doc.UpdatedAt, doc.Version, doc.Passages, doc.Characters, "Sample content for test."));
    }

    public Task<bool> DeleteAsync(long tenantId, long documentId, CancellationToken cancellationToken = default)
    {
        Check();
        Deleted.Add((tenantId, documentId));
        return Task.FromResult(Documents.RemoveAll(d => d.Id == documentId) > 0);
    }

    public Task<IReadOnlyList<KnowledgePassage>> SearchAsync(long tenantId, IReadOnlyCollection<string> roles, bool seesAll, string query, int limit, CancellationToken cancellationToken = default)
    {
        Check();
        Searches++;
        LastSearch = (tenantId, roles, seesAll, query, limit);
        return Task.FromResult<IReadOnlyList<KnowledgePassage>>(Passages.ToList());
    }
}
