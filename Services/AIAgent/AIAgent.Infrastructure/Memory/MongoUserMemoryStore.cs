using AIAgent.Application.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AIAgent.Infrastructure.Memory;

public sealed record UserMemoryOptions(string? ConnectionString, string Database = "ai_agent_db");

[BsonIgnoreExtraElements]
internal sealed class MemoryDocument
{
    [BsonId] public string Id { get; set; } = "";
    [BsonElement("tenantId")] public long TenantId { get; set; }
    [BsonElement("userId")] public long UserId { get; set; }
    [BsonElement("noteId")] public string NoteId { get; set; } = "";
    [BsonElement("text")] public string Text { get; set; } = "";
    [BsonElement("createdAt")] public long CreatedAt { get; set; }
}

public sealed class MongoUserMemoryStore : IUserMemoryStore
{
    private readonly IMongoCollection<MemoryDocument> _notes;
    private readonly ILogger<MongoUserMemoryStore> _logger;
    private readonly Lazy<Task> _indexes;

    public MongoUserMemoryStore(UserMemoryOptions options, ILogger<MongoUserMemoryStore> logger)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("A MongoDB connection string is required.", nameof(options));

        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);   
        _notes = new MongoClient(settings).GetDatabase(options.Database).GetCollection<MemoryDocument>("user_memory");
        _logger = logger;
        _indexes = new Lazy<Task>(CreateIndexesAsync);
    }

    private static string DocumentId(long tenantId, long userId, string noteId) => $"{tenantId}:{userId}:{noteId}";

    private async Task CreateIndexesAsync()
    {
        try
        {
            await _notes.Indexes.CreateOneAsync(new CreateIndexModel<MemoryDocument>(
                Builders<MemoryDocument>.IndexKeys.Ascending(n => n.TenantId).Ascending(n => n.UserId).Ascending(n => n.CreatedAt),
                new CreateIndexOptions { Name = "ix_memory_user_created" }));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not create the memory index.");   
        }
    }

    private async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            await _indexes.Value;
            return await action();
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            _logger.LogError(ex, "Memory storage failed.");
            throw new UserMemoryUnavailableException("Memory is not reachable right now.", ex);
        }
    }

    public Task<IReadOnlyList<MemoryItem>> ListAsync(long tenantId, long userId, CancellationToken cancellationToken = default) =>
        RunAsync<IReadOnlyList<MemoryItem>>(async () =>
        {
            var docs = await _notes.Find(n => n.TenantId == tenantId && n.UserId == userId)
                .SortBy(n => n.CreatedAt).Limit(UserMemoryRules.MaxItems).ToListAsync(cancellationToken);
            return docs.Select(d => new MemoryItem(d.NoteId, d.Text, d.CreatedAt)).ToList();
        });

    public Task<MemoryAddOutcome> AddAsync(long tenantId, long userId, string text, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var note = UserMemoryRules.Normalize(text);
            var noteId = UserMemoryRules.IdFor(note);
            var id = DocumentId(tenantId, userId, noteId);

            if (await _notes.Find(n => n.Id == id).AnyAsync(cancellationToken))
                return new MemoryAddOutcome(MemoryAddStatus.Duplicate, null);
            if (await _notes.CountDocumentsAsync(n => n.TenantId == tenantId && n.UserId == userId, cancellationToken: cancellationToken) >= UserMemoryRules.MaxItems)
                return new MemoryAddOutcome(MemoryAddStatus.Full, null);

            var item = new MemoryItem(noteId, note, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            try
            {
                await _notes.InsertOneAsync(new MemoryDocument { Id = id, TenantId = tenantId, UserId = userId, NoteId = noteId, Text = note, CreatedAt = item.CreatedAt }, cancellationToken: cancellationToken);
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                return new MemoryAddOutcome(MemoryAddStatus.Duplicate, null);   
            }
            return new MemoryAddOutcome(MemoryAddStatus.Added, item);
        });

    public Task<bool> DeleteAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            if (!UserMemoryRules.IsValidId(id)) return false;
            var documentId = DocumentId(tenantId, userId, id);
            var result = await _notes.DeleteOneAsync(n => n.Id == documentId && n.TenantId == tenantId && n.UserId == userId, cancellationToken);
            return result.DeletedCount > 0;
        });

    public Task<long> ClearAsync(long tenantId, long userId, CancellationToken cancellationToken = default) =>
        RunAsync(async () => (await _notes.DeleteManyAsync(n => n.TenantId == tenantId && n.UserId == userId, cancellationToken)).DeletedCount);
}

public sealed class UnavailableUserMemoryStore : IUserMemoryStore
{
    private static UserMemoryUnavailableException Unavailable() => new("Memory is not set up on this server.");

    public Task<IReadOnlyList<MemoryItem>> ListAsync(long tenantId, long userId, CancellationToken cancellationToken = default) => throw Unavailable();
    public Task<MemoryAddOutcome> AddAsync(long tenantId, long userId, string text, CancellationToken cancellationToken = default) => throw Unavailable();
    public Task<bool> DeleteAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default) => throw Unavailable();
    public Task<long> ClearAsync(long tenantId, long userId, CancellationToken cancellationToken = default) => throw Unavailable();
}
