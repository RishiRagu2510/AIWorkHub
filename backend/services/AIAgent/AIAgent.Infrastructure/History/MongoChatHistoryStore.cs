using AIAgent.Application.Interfaces;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AIAgent.Infrastructure.History;

public sealed record ChatHistoryOptions(string? ConnectionString, string Database = "ai_agent_db", int RetentionDays = 90);

[BsonIgnoreExtraElements]
internal sealed class ChatDocument
{
    [BsonId] public string Id { get; set; } = "";
    [BsonElement("tenantId")] public long TenantId { get; set; }
    [BsonElement("userId")] public long UserId { get; set; }
    [BsonElement("chatId")] public string ChatId { get; set; } = "";
    [BsonElement("title")] public string Title { get; set; } = "";
    [BsonElement("createdAt")] public DateTime CreatedAt { get; set; }
    [BsonElement("updatedAt")] public long UpdatedAt { get; set; }
    
    [BsonElement("expiresAt")] public DateTime ExpiresAt { get; set; }
    [BsonElement("messageCount")] public int MessageCount { get; set; }
    [BsonElement("messages")] public List<MessageDocument> Messages { get; set; } = [];
}

[BsonIgnoreExtraElements]
internal sealed class MessageDocument
{
    [BsonElement("role")] public string Role { get; set; } = "";
    [BsonElement("content")] public string Content { get; set; } = "";
    [BsonElement("time")] public string? Time { get; set; }
    
    
    [BsonElement("extra")] public string? Extra { get; set; }
}

[BsonIgnoreExtraElements]
internal sealed class ConsentDocument
{
    [BsonId] public string Id { get; set; } = "";
    [BsonElement("tenantId")] public long TenantId { get; set; }
    [BsonElement("userId")] public long UserId { get; set; }
    [BsonElement("value")] public string Value { get; set; } = "";
    [BsonElement("updatedAt")] public DateTime UpdatedAt { get; set; }
}

public sealed class MongoChatHistoryStore : IChatHistoryStore
{
    private readonly IMongoCollection<ChatDocument> _chats;
    private readonly IMongoCollection<ConsentDocument> _consent;
    private readonly int _retentionDays;
    private readonly ILogger<MongoChatHistoryStore> _logger;
    private readonly Lazy<Task> _indexes;

    public MongoChatHistoryStore(ChatHistoryOptions options, ILogger<MongoChatHistoryStore> logger)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
            throw new ArgumentException("A MongoDB connection string is required.", nameof(options));

        var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
        
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
        var database = new MongoClient(settings).GetDatabase(options.Database);

        _chats = database.GetCollection<ChatDocument>("conversations");
        _consent = database.GetCollection<ConsentDocument>("chat_history_consent");
        _retentionDays = Math.Max(1, options.RetentionDays);
        _logger = logger;
        _indexes = new Lazy<Task>(CreateIndexesAsync);
    }

    private static string DocumentId(long tenantId, long userId, string chatId) => $"{tenantId}:{userId}:{chatId}";

    private async Task CreateIndexesAsync()
    {
        try
        {
            await _chats.Indexes.CreateManyAsync([
                new CreateIndexModel<ChatDocument>(
                    Builders<ChatDocument>.IndexKeys.Ascending(c => c.TenantId).Ascending(c => c.UserId).Descending(c => c.UpdatedAt),
                    new CreateIndexOptions { Name = "ix_history_user_updated" }),
                new CreateIndexModel<ChatDocument>(
                    Builders<ChatDocument>.IndexKeys.Ascending(c => c.ExpiresAt),
                    new CreateIndexOptions { Name = "ix_history_ttl", ExpireAfter = TimeSpan.Zero })
            ]);
        }
        catch (Exception ex)
        {
            
            _logger.LogWarning(ex, "Could not create the chat history indexes.");
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
            _logger.LogError(ex, "Chat history storage failed.");
            throw new ChatHistoryUnavailableException("Chat history storage is not reachable right now.", ex);
        }
    }

    public Task<string?> GetConsentAsync(long tenantId, long userId, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var id = $"{tenantId}:{userId}";
            var doc = await _consent.Find(c => c.Id == id && c.TenantId == tenantId && c.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            return doc?.Value is "yes" or "no" ? doc.Value : null;
        });

    public Task SetConsentAsync(long tenantId, long userId, string value, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var id = $"{tenantId}:{userId}";
            await _consent.ReplaceOneAsync(
                c => c.Id == id,
                new ConsentDocument { Id = id, TenantId = tenantId, UserId = userId, Value = value, UpdatedAt = DateTime.UtcNow },
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
            return true;
        });

    public Task<IReadOnlyList<ChatSummary>> ListAsync(long tenantId, long userId, CancellationToken cancellationToken = default) =>
        RunAsync<IReadOnlyList<ChatSummary>>(async () =>
        {
            var docs = await _chats.Find(c => c.TenantId == tenantId && c.UserId == userId)
                .SortByDescending(c => c.UpdatedAt)
                .Limit(ChatHistoryRules.MaxChats)
                .Project(c => new ChatDocument { Id = c.Id, ChatId = c.ChatId, Title = c.Title, UpdatedAt = c.UpdatedAt, MessageCount = c.MessageCount })
                .ToListAsync(cancellationToken);
            return docs.Select(d => new ChatSummary(d.ChatId, d.Title, d.UpdatedAt, d.MessageCount)).ToList();
        });

    public Task<StoredChat?> GetAsync(long tenantId, long userId, string chatId, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var id = DocumentId(tenantId, userId, chatId);
            var doc = await _chats.Find(c => c.Id == id && c.TenantId == tenantId && c.UserId == userId).FirstOrDefaultAsync(cancellationToken);
            return doc is null
                ? null
                : new StoredChat(doc.ChatId, doc.Title, doc.UpdatedAt, doc.Messages.Select(m => new StoredMessage(m.Role, m.Content, m.Time, m.Extra)).ToList());
        });

    public Task SaveAsync(long tenantId, long userId, StoredChat chat, int maxChats, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var id = DocumentId(tenantId, userId, chat.Id);
            var now = DateTime.UtcNow;
            var update = Builders<ChatDocument>.Update
                .Set(c => c.TenantId, tenantId)
                .Set(c => c.UserId, userId)
                .Set(c => c.ChatId, chat.Id)
                .Set(c => c.Title, chat.Title)
                .Set(c => c.UpdatedAt, chat.UpdatedAt)
                .Set(c => c.ExpiresAt, now.AddDays(_retentionDays))
                .Set(c => c.MessageCount, chat.Messages.Count)
                .Set(c => c.Messages, chat.Messages.Select(m => new MessageDocument { Role = m.Role, Content = m.Content, Time = m.Time, Extra = m.ExtraJson }).ToList())
                .SetOnInsert(c => c.CreatedAt, now);
            await _chats.UpdateOneAsync(c => c.Id == id, update, new UpdateOptions { IsUpsert = true }, cancellationToken);

            
            var surplus = await _chats.Find(c => c.TenantId == tenantId && c.UserId == userId)
                .SortByDescending(c => c.UpdatedAt)
                .Skip(Math.Max(1, maxChats))
                .Project(c => c.Id)
                .ToListAsync(cancellationToken);
            if (surplus.Count > 0)
                await _chats.DeleteManyAsync(c => surplus.Contains(c.Id) && c.TenantId == tenantId && c.UserId == userId, cancellationToken);
            return true;
        });

    public Task<bool> DeleteAsync(long tenantId, long userId, string chatId, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var id = DocumentId(tenantId, userId, chatId);
            var result = await _chats.DeleteOneAsync(c => c.Id == id && c.TenantId == tenantId && c.UserId == userId, cancellationToken);
            return result.DeletedCount > 0;
        });

    public Task<long> ClearAsync(long tenantId, long userId, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var result = await _chats.DeleteManyAsync(c => c.TenantId == tenantId && c.UserId == userId, cancellationToken);
            return result.DeletedCount;
        });
}


public sealed class UnavailableChatHistoryStore : IChatHistoryStore
{
    private static ChatHistoryUnavailableException Off() =>
        new("Chat history storage is not configured. Set Mongo:ConnectionString for the AI agent service.");

    public Task<string?> GetConsentAsync(long tenantId, long userId, CancellationToken cancellationToken = default) => throw Off();
    public Task SetConsentAsync(long tenantId, long userId, string value, CancellationToken cancellationToken = default) => throw Off();
    public Task<IReadOnlyList<ChatSummary>> ListAsync(long tenantId, long userId, CancellationToken cancellationToken = default) => throw Off();
    public Task<StoredChat?> GetAsync(long tenantId, long userId, string chatId, CancellationToken cancellationToken = default) => throw Off();
    public Task SaveAsync(long tenantId, long userId, StoredChat chat, int maxChats, CancellationToken cancellationToken = default) => throw Off();
    public Task<bool> DeleteAsync(long tenantId, long userId, string chatId, CancellationToken cancellationToken = default) => throw Off();
    public Task<long> ClearAsync(long tenantId, long userId, CancellationToken cancellationToken = default) => throw Off();
}
