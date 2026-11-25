using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.History;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace AIAgent.Infrastructure.Auditing;

public sealed record AiActionLogOptions(int RetentionDays = 365);

[BsonIgnoreExtraElements]
internal sealed class ActionDocument
{
    [BsonId] public MongoDB.Bson.ObjectId Id { get; set; }
    [BsonElement("tenantId")] public long TenantId { get; set; }
    [BsonElement("userId")] public long UserId { get; set; }
    [BsonElement("userName")] public string UserName { get; set; } = "";
    [BsonElement("role")] public string Role { get; set; } = "";
    [BsonElement("sessionId")] public string SessionId { get; set; } = "";
    [BsonElement("tool")] public string Tool { get; set; } = "";
    [BsonElement("kind")] public string Kind { get; set; } = "";
    [BsonElement("outcome")] public string Outcome { get; set; } = "";
    [BsonElement("arguments")] public string Arguments { get; set; } = "{}";
    [BsonElement("detail")] public string? Detail { get; set; }
    [BsonElement("flagged")] public bool Flagged { get; set; }
    [BsonElement("at")] public DateTime At { get; set; }
    
    [BsonElement("expiresAt")] public DateTime ExpiresAt { get; set; }
}

public sealed class MongoAiActionLog : IAiActionLog
{
    private readonly IMongoCollection<ActionDocument> _actions;
    private readonly int _retentionDays;
    private readonly ILogger<MongoAiActionLog> _logger;
    private readonly Lazy<Task> _indexes;

    public MongoAiActionLog(ChatHistoryOptions mongo, AiActionLogOptions options, ILogger<MongoAiActionLog> logger)
    {
        if (string.IsNullOrWhiteSpace(mongo.ConnectionString))
            throw new ArgumentException("A MongoDB connection string is required.", nameof(mongo));

        var settings = MongoClientSettings.FromConnectionString(mongo.ConnectionString);
        
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(3);
        _actions = new MongoClient(settings).GetDatabase(mongo.Database).GetCollection<ActionDocument>("ai_actions");
        _retentionDays = Math.Max(1, options.RetentionDays);
        _logger = logger;
        _indexes = new Lazy<Task>(CreateIndexesAsync);
    }

    private async Task CreateIndexesAsync()
    {
        try
        {
            await _actions.Indexes.CreateManyAsync([
                new CreateIndexModel<ActionDocument>(
                    Builders<ActionDocument>.IndexKeys.Ascending(a => a.TenantId).Ascending(a => a.UserId).Descending(a => a.At),
                    new CreateIndexOptions { Name = "ix_actions_user_time" }),
                new CreateIndexModel<ActionDocument>(
                    Builders<ActionDocument>.IndexKeys.Ascending(a => a.TenantId).Ascending(a => a.Outcome).Descending(a => a.At),
                    new CreateIndexOptions { Name = "ix_actions_outcome_time" }),
                new CreateIndexModel<ActionDocument>(
                    Builders<ActionDocument>.IndexKeys.Ascending(a => a.ExpiresAt),
                    new CreateIndexOptions { Name = "ix_actions_ttl", ExpireAfter = TimeSpan.Zero })
            ]);
        }
        catch (Exception ex)
        {
            
            _logger.LogWarning(ex, "Could not create the AI action log indexes.");
        }
    }

    public async Task RecordAsync(AiActionRecord record, CancellationToken cancellationToken = default)
    {
        try
        {
            await _indexes.Value;
            await _actions.InsertOneAsync(new ActionDocument
            {
                TenantId = record.TenantId,
                UserId = record.UserId,
                UserName = record.UserName,
                Role = record.Role,
                SessionId = record.SessionId,
                Tool = record.ToolName,
                Kind = record.Kind.ToString(),
                Outcome = record.Outcome,
                Arguments = record.Arguments,
                Detail = record.Detail,
                Flagged = record.InjectionFlagged,
                At = record.OccurredAt,
                ExpiresAt = record.OccurredAt.AddDays(_retentionDays)
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            
            _logger.LogError(ex, "Could not record the AI action {Tool} for user {UserId}.", record.ToolName, record.UserId);
        }
    }

    public async Task<IReadOnlyList<AiActionRecord>> ListAsync(long tenantId, long? userId, string? outcome, int take, CancellationToken cancellationToken = default)
    {
        await _indexes.Value;
        var filter = Builders<ActionDocument>.Filter.Eq(a => a.TenantId, tenantId);
        if (userId is > 0) filter &= Builders<ActionDocument>.Filter.Eq(a => a.UserId, userId.Value);
        if (!string.IsNullOrWhiteSpace(outcome)) filter &= Builders<ActionDocument>.Filter.Eq(a => a.Outcome, outcome);

        var docs = await _actions.Find(filter)
            .SortByDescending(a => a.At)
            .Limit(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);

        return docs.Select(d => new AiActionRecord(
            d.TenantId, d.UserId, d.UserName, d.Role, d.SessionId, d.Tool,
            Enum.TryParse<ToolActivityKind>(d.Kind, out var kind) ? kind : ToolActivityKind.Write,
            d.Outcome, d.Arguments, d.Detail, d.Flagged,
            DateTime.SpecifyKind(d.At, DateTimeKind.Utc))).ToList();
    }
}
