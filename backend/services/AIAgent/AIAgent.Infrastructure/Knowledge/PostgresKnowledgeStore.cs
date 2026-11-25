using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace AIAgent.Infrastructure.Knowledge;

public sealed class KnowledgeBaseOptions
{
    public const string Section = "KnowledgeBase";

    
    public string ConnectionString { get; set; } = "";

    
    public double MinSimilarity { get; set; } = 0.6;
}

public sealed class PostgresKnowledgeStore : IKnowledgeStore, IKnowledgeIndexer, IAsyncDisposable
{
    private const string SourceName = "Company documents";
    private const int KeptVersions = 5;

    private readonly NpgsqlDataSource _db;
    private readonly IKnowledgeEmbedder _embedder;
    private readonly double _minSimilarity;

    public PostgresKnowledgeStore(IOptions<KnowledgeBaseOptions> options, IKnowledgeEmbedder? embedder = null)
    {
        _embedder = embedder ?? new NoKnowledgeEmbedder();
        _minSimilarity = Math.Clamp(options.Value.MinSimilarity, 0.0, 0.99);
        var builder = new NpgsqlConnectionStringBuilder(options.Value.ConnectionString) { Timeout = 3, CommandTimeout = 20 };
        _db = NpgsqlDataSource.Create(builder.ConnectionString);
    }

    public ValueTask DisposeAsync() => _db.DisposeAsync();

    private static async Task<T> GuardAsync<T>(Func<Task<T>> work)
    {
        try
        {
            return await work();
        }
        catch (Exception ex) when (ex is NpgsqlException or TimeoutException or System.Net.Sockets.SocketException or InvalidOperationException { Source: "Npgsql" })
        {
            throw new KnowledgeUnavailableException("The company documents can't be reached right now. Please try again in a moment.", ex);
        }
    }

    public Task<KnowledgeSaveOutcome> SaveAsync(long tenantId, long userId, NewKnowledgeDocument document, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document.Text)));
            var roles = document.AllowedRoles.Select(r => r.ToUpperInvariant()).ToArray();

            await using var conn = await _db.OpenConnectionAsync(ct);
            await using var tx = await conn.BeginTransactionAsync(ct);

            await using (var make = new NpgsqlCommand(
                "INSERT INTO knowledge_sources (tenant_id, source_type, name, description) VALUES (@t, 'FILE', @n, 'Files uploaded by Admin and HR') ON CONFLICT DO NOTHING", conn, tx))
            {
                make.Parameters.AddWithValue("t", tenantId);
                make.Parameters.AddWithValue("n", SourceName);
                await make.ExecuteNonQueryAsync(ct);
            }

            
            long sourceId;
            await using (var find = new NpgsqlCommand(
                "SELECT id FROM knowledge_sources WHERE tenant_id = @t AND source_type = 'FILE' AND name = @n FOR UPDATE", conn, tx))
            {
                find.Parameters.AddWithValue("t", tenantId);
                find.Parameters.AddWithValue("n", SourceName);
                sourceId = (long)(await find.ExecuteScalarAsync(ct))!;
            }

            long? documentId = null;
            var latestVersion = 0;
            string? latestChecksum = null;
            string[] latestRoles = [];
            await using (var existing = new NpgsqlCommand(
                @"SELECT d.id, d.allowed_roles,
                         coalesce((SELECT max(version_number) FROM document_versions WHERE document_id = d.id), 0),
                         (SELECT checksum FROM document_versions WHERE document_id = d.id ORDER BY version_number DESC LIMIT 1)
                  FROM documents d
                  WHERE d.source_id = @s AND lower(d.title) = lower(@title) AND d.uploaded_by IS NOT NULL", conn, tx))
            {
                existing.Parameters.AddWithValue("s", sourceId);
                existing.Parameters.AddWithValue("title", document.Title);
                await using var reader = await existing.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    documentId = reader.GetInt64(0);
                    latestRoles = reader.GetFieldValue<string[]>(1);
                    latestVersion = reader.GetInt32(2);
                    latestChecksum = reader.IsDBNull(3) ? null : reader.GetString(3);
                }
            }

            var metadata = JsonSerializer.Serialize(new { fileName = document.FileName });

            if (documentId is null)
            {
                await using var count = new NpgsqlCommand("SELECT count(*) FROM documents WHERE tenant_id = @t AND uploaded_by IS NOT NULL", conn, tx);
                count.Parameters.AddWithValue("t", tenantId);
                if ((long)(await count.ExecuteScalarAsync(ct))! >= KnowledgeRules.MaxDocuments)
                    return new KnowledgeSaveOutcome(KnowledgeSaveStatus.Full, null);

                await using var insert = new NpgsqlCommand(
                    @"INSERT INTO documents (tenant_id, source_id, title, document_type, mime_type, status, checksum, metadata, allowed_roles, uploaded_by)
                      VALUES (@t, @s, @title, 'UPLOADED_FILE', @mime, 'ACTIVE', @sum, @meta, @roles, @user) RETURNING id", conn, tx);
                insert.Parameters.AddWithValue("t", tenantId);
                insert.Parameters.AddWithValue("s", sourceId);
                insert.Parameters.AddWithValue("title", document.Title);
                insert.Parameters.AddWithValue("mime", document.ContentType);
                insert.Parameters.AddWithValue("sum", checksum);
                insert.Parameters.Add(new NpgsqlParameter("meta", NpgsqlDbType.Jsonb) { Value = metadata });
                insert.Parameters.AddWithValue("roles", roles);
                insert.Parameters.AddWithValue("user", userId);
                documentId = (long)(await insert.ExecuteScalarAsync(ct))!;
            }
            else
            {
                var sameAudience = new HashSet<string>(latestRoles).SetEquals(roles);
                var sameText = string.Equals(latestChecksum, checksum, StringComparison.Ordinal);
                if (sameText && sameAudience)
                {
                    await tx.RollbackAsync(ct);
                    return new KnowledgeSaveOutcome(KnowledgeSaveStatus.Unchanged, (await ListAsync(tenantId, ct)).FirstOrDefault(d => d.Id == documentId));
                }

                await using var update = new NpgsqlCommand(
                    @"UPDATE documents SET title = @title, mime_type = @mime, checksum = @sum, metadata = @meta, allowed_roles = @roles,
                                           uploaded_by = @user, status = 'ACTIVE', updated_at = CURRENT_TIMESTAMP
                      WHERE id = @id AND tenant_id = @t", conn, tx);
                update.Parameters.AddWithValue("title", document.Title);
                update.Parameters.AddWithValue("mime", document.ContentType);
                update.Parameters.AddWithValue("sum", checksum);
                update.Parameters.Add(new NpgsqlParameter("meta", NpgsqlDbType.Jsonb) { Value = metadata });
                update.Parameters.AddWithValue("roles", roles);
                update.Parameters.AddWithValue("user", userId);
                update.Parameters.AddWithValue("id", documentId.Value);
                update.Parameters.AddWithValue("t", tenantId);
                await update.ExecuteNonQueryAsync(ct);

                
                if (sameText)
                {
                    await tx.CommitAsync(ct);
                    return new KnowledgeSaveOutcome(KnowledgeSaveStatus.Replaced, (await ListAsync(tenantId, ct)).FirstOrDefault(d => d.Id == documentId));
                }
            }

            var version = latestVersion + 1;
            long versionId;
            await using (var add = new NpgsqlCommand(
                @"INSERT INTO document_versions (tenant_id, document_id, version_number, content, checksum, content_format)
                  VALUES (@t, @d, @v, @content, @sum, @format) RETURNING id", conn, tx))
            {
                add.Parameters.AddWithValue("t", tenantId);
                add.Parameters.AddWithValue("d", documentId.Value);
                add.Parameters.AddWithValue("v", version);
                add.Parameters.AddWithValue("content", document.Text);
                add.Parameters.AddWithValue("sum", checksum);
                add.Parameters.AddWithValue("format", document.FileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? "MARKDOWN" : "TEXT");
                versionId = (long)(await add.ExecuteScalarAsync(ct))!;
            }

            if (document.Chunks.Count > 0)
            {
                await using var chunks = new NpgsqlCommand(
                    @"INSERT INTO document_chunks (tenant_id, document_version_id, chunk_index, content, token_count, metadata)
                      SELECT @t, @v, x.i, x.c, x.n, x.m::jsonb
                      FROM unnest(@idx, @content, @tokens, @meta) AS x(i, c, n, m)", conn, tx);
                chunks.Parameters.AddWithValue("t", tenantId);
                chunks.Parameters.AddWithValue("v", versionId);
                chunks.Parameters.AddWithValue("idx", Enumerable.Range(0, document.Chunks.Count).ToArray());
                chunks.Parameters.AddWithValue("content", document.Chunks.Select(KnowledgeChunkText).ToArray());
                chunks.Parameters.AddWithValue("tokens", document.Chunks.Select(c => (KnowledgeChunkText(c).Length + 3) / 4).ToArray());
                chunks.Parameters.AddWithValue("meta", document.Chunks.Select(c => JsonSerializer.Serialize(new { heading = c.Heading })).ToArray());
                await chunks.ExecuteNonQueryAsync(ct);
            }

            
            await using (var retire = new NpgsqlCommand(
                @"DELETE FROM document_chunks c USING document_versions v
                  WHERE c.document_version_id = v.id AND v.document_id = @d AND v.version_number < @v;
                  DELETE FROM document_versions WHERE document_id = @d AND version_number <= @old", conn, tx))
            {
                retire.Parameters.AddWithValue("d", documentId.Value);
                retire.Parameters.AddWithValue("v", version);
                retire.Parameters.AddWithValue("old", version - KeptVersions);
                await retire.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
            var saved = (await ListAsync(tenantId, ct)).FirstOrDefault(d => d.Id == documentId);
            return new KnowledgeSaveOutcome(latestVersion == 0 ? KnowledgeSaveStatus.Added : KnowledgeSaveStatus.Replaced, saved);
        });

    private static string KnowledgeChunkText(KnowledgeChunk chunk) => KnowledgeChunker.Stored(chunk);

    public Task<IReadOnlyList<KnowledgeDocument>> ListAsync(long tenantId, CancellationToken ct = default) =>
        GuardAsync<IReadOnlyList<KnowledgeDocument>>(async () =>
        {
            await using var cmd = _db.CreateCommand(
                @"SELECT d.id, d.title, coalesce(d.metadata->>'fileName', ''), d.allowed_roles, coalesce(d.uploaded_by, 0), d.updated_at,
                         v.version_number, (SELECT count(*) FROM document_chunks c WHERE c.document_version_id = v.id), char_length(v.content)
                  FROM documents d
                  JOIN LATERAL (SELECT id, version_number, content FROM document_versions WHERE document_id = d.id ORDER BY version_number DESC LIMIT 1) v ON true
                  WHERE d.tenant_id = @t AND d.uploaded_by IS NOT NULL AND d.status = 'ACTIVE'
                  ORDER BY lower(d.title)");
            cmd.Parameters.AddWithValue("t", tenantId);
            var result = new List<KnowledgeDocument>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result.Add(new KnowledgeDocument(
                    reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<string[]>(3), reader.GetInt64(4),
                    new DateTimeOffset(reader.GetFieldValue<DateTime>(5), TimeSpan.Zero), reader.GetInt32(6), (int)reader.GetInt64(7), reader.GetInt32(8)));
            }
            return result;
        });

    public Task<KnowledgeDocumentDetail?> GetAsync(long tenantId, long documentId, CancellationToken ct = default) =>
        GuardAsync<KnowledgeDocumentDetail?>(async () =>
        {
            await using var cmd = _db.CreateCommand(
                @"SELECT d.id, d.title, coalesce(d.metadata->>'fileName', ''), d.allowed_roles, coalesce(d.uploaded_by, 0), d.updated_at,
                         v.version_number, (SELECT count(*) FROM document_chunks c WHERE c.document_version_id = v.id), char_length(v.content), v.content
                  FROM documents d
                  JOIN LATERAL (SELECT id, version_number, content FROM document_versions WHERE document_id = d.id ORDER BY version_number DESC LIMIT 1) v ON true
                  WHERE d.id = @id AND d.tenant_id = @t AND d.uploaded_by IS NOT NULL AND d.status = 'ACTIVE'");
            cmd.Parameters.AddWithValue("id", documentId);
            cmd.Parameters.AddWithValue("t", tenantId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new KnowledgeDocumentDetail(
                    reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<string[]>(3), reader.GetInt64(4),
                    new DateTimeOffset(reader.GetFieldValue<DateTime>(5), TimeSpan.Zero), reader.GetInt32(6), (int)reader.GetInt64(7), reader.GetInt32(8),
                    reader.GetString(9));
            }
            return null;
        });

    public Task<bool> DeleteAsync(long tenantId, long documentId, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            await using var cmd = _db.CreateCommand("DELETE FROM documents WHERE id = @id AND tenant_id = @t AND uploaded_by IS NOT NULL");
            cmd.Parameters.AddWithValue("id", documentId);
            cmd.Parameters.AddWithValue("t", tenantId);
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        });

    private const string StrictQuery = "plainto_tsquery('english', @q)";

    
    private const string LooseQuery = "to_tsquery('english', replace(plainto_tsquery('english', @q)::text, ' & ', ' | '))";

    private const int FusionK = 60;

    public Task<IReadOnlyList<KnowledgePassage>> SearchAsync(long tenantId, IReadOnlyCollection<string> roles, bool seesAll, string query, int limit, CancellationToken ct = default) =>
        GuardAsync<IReadOnlyList<KnowledgePassage>>(async () =>
        {
            var text = (query ?? "").Trim();
            if (text.Length == 0) return [];
            if (text.Length > KnowledgeRules.MaxQueryChars) text = text[..KnowledgeRules.MaxQueryChars];
            var upper = roles.Select(r => r.Trim().ToUpperInvariant()).Where(r => r.Length > 0).Distinct().ToArray();
            var take = Math.Clamp(limit, 1, 20);

            
            var vectorTask = _embedder.Enabled ? _embedder.EmbedQueryAsync(text, ct) : Task.FromResult<float[]?>(null);

            var keyword = await RunKeywordAsync(StrictQuery);
            if (keyword.Count == 0) keyword = await RunKeywordAsync(LooseQuery);

            var vector = await vectorTask;
            if (vector is null) return keyword.Take(take).ToList();

            var meaning = await RunMeaningAsync(vector);
            if (meaning.Count == 0) return keyword.Take(take).ToList();
            return Fuse(keyword, meaning).Take(take).ToList();

            async Task<IReadOnlyList<KnowledgePassage>> RunKeywordAsync(string tsquery)
            {
                await using var cmd = _db.CreateCommand(
                    $@"WITH q AS (SELECT {tsquery} AS query)
                       SELECT d.id, d.title, c.metadata->>'heading', c.content, ts_rank_cd(c.search_vector, q.query)::float8, v.version_number, d.updated_at
                       FROM q, document_chunks c
                       JOIN document_versions v ON v.id = c.document_version_id
                       JOIN documents d ON d.id = v.document_id
                       WHERE c.tenant_id = @t AND d.tenant_id = @t AND d.status = 'ACTIVE'
                         AND c.search_vector @@ q.query
                         AND (@all OR cardinality(d.allowed_roles) = 0 OR d.allowed_roles && @roles)
                       ORDER BY 5 DESC, d.id, c.chunk_index
                       LIMIT @limit");
                cmd.Parameters.AddWithValue("q", text);
                cmd.Parameters.AddWithValue("t", tenantId);
                cmd.Parameters.AddWithValue("all", seesAll);
                cmd.Parameters.AddWithValue("roles", upper);
                cmd.Parameters.AddWithValue("limit", take);
                return await ReadPassagesAsync(cmd, ct);
            }

            
            async Task<IReadOnlyList<KnowledgePassage>> RunMeaningAsync(float[] question)
            {
                await using var cmd = _db.CreateCommand(
                    @"SELECT d.id, d.title, c.metadata->>'heading', c.content, (1 - (e.embedding <=> @vec::vector))::float8, v.version_number, d.updated_at
                      FROM embeddings e
                      JOIN document_chunks c ON c.id = e.document_chunk_id
                      JOIN document_versions v ON v.id = c.document_version_id
                      JOIN documents d ON d.id = v.document_id
                      WHERE e.tenant_id = @t AND c.tenant_id = @t AND d.tenant_id = @t AND d.status = 'ACTIVE' AND d.uploaded_by IS NOT NULL
                        AND e.embedding_model = @model AND e.embedding_dimension = @dim
                        AND (@all OR cardinality(d.allowed_roles) = 0 OR d.allowed_roles && @roles)
                      ORDER BY e.embedding <=> @vec::vector
                      LIMIT @limit");
                cmd.Parameters.AddWithValue("vec", VectorText(question));
                cmd.Parameters.AddWithValue("t", tenantId);
                cmd.Parameters.AddWithValue("model", _embedder.Model);
                cmd.Parameters.AddWithValue("dim", _embedder.Dimensions);
                cmd.Parameters.AddWithValue("all", seesAll);
                cmd.Parameters.AddWithValue("roles", upper);
                cmd.Parameters.AddWithValue("limit", take);
                var near = await ReadPassagesAsync(cmd, ct);
                return near.Where(p => p.Rank >= _minSimilarity).ToList();
            }
        });

    private static async Task<IReadOnlyList<KnowledgePassage>> ReadPassagesAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var result = new List<KnowledgePassage>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var section = reader.IsDBNull(2) ? null : reader.GetString(2);
            var content = reader.GetString(3);
            
            if (!string.IsNullOrEmpty(section) && content.StartsWith(section + "\n", StringComparison.Ordinal))
                content = content[(section.Length + 1)..];
            result.Add(new KnowledgePassage(
                reader.GetInt64(0), reader.GetString(1), section, content, reader.GetDouble(4), reader.GetInt32(5),
                new DateTimeOffset(reader.GetFieldValue<DateTime>(6), TimeSpan.Zero)));
        }
        return result;
    }

    
    public static List<KnowledgePassage> Fuse(IReadOnlyList<KnowledgePassage> keyword, IReadOnlyList<KnowledgePassage> meaning)
    {
        var scores = new Dictionary<(long, string), (KnowledgePassage Passage, double Score)>();
        void Add(IReadOnlyList<KnowledgePassage> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var key = (list[i].DocumentId, list[i].Text);
                var gain = 1.0 / (FusionK + i + 1);
                scores[key] = scores.TryGetValue(key, out var seen) ? (seen.Passage, seen.Score + gain) : (list[i], gain);
            }
        }
        Add(keyword);
        Add(meaning);
        return scores.Values.OrderByDescending(x => x.Score).ThenBy(x => x.Passage.DocumentId)
            .Select(x => x.Passage with { Rank = x.Score }).ToList();
    }

    private static string VectorText(float[] v) =>
        "[" + string.Join(',', v.Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "]";

    public Task<int> IndexPendingAsync(int max, CancellationToken ct = default) =>
        GuardAsync(async () =>
        {
            if (!_embedder.Enabled) return 0;
            var ids = new List<long>(); var tenants = new List<long>(); var texts = new List<string>();
            await using (var pending = _db.CreateCommand(
                @"SELECT c.id, c.tenant_id, c.content
                  FROM document_chunks c
                  JOIN document_versions v ON v.id = c.document_version_id
                  JOIN documents d ON d.id = v.document_id
                  WHERE d.uploaded_by IS NOT NULL AND d.status = 'ACTIVE'
                    AND NOT EXISTS (SELECT 1 FROM embeddings e WHERE e.document_chunk_id = c.id AND e.embedding_model = @model AND e.embedding_dimension = @dim)
                  ORDER BY c.id LIMIT @max"))
            {
                pending.Parameters.AddWithValue("model", _embedder.Model);
                pending.Parameters.AddWithValue("dim", _embedder.Dimensions);
                pending.Parameters.AddWithValue("max", Math.Clamp(max, 1, 500));
                await using var reader = await pending.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct)) { ids.Add(reader.GetInt64(0)); tenants.Add(reader.GetInt64(1)); texts.Add(reader.GetString(2)); }
            }
            if (ids.Count == 0) return 0;

            var vectors = await _embedder.EmbedPassagesAsync(texts, ct);
            var keep = Enumerable.Range(0, ids.Count).Where(i => vectors[i] is { Length: > 0 }).ToArray();
            if (keep.Length == 0) return 0;

            await using var save = _db.CreateCommand(
                @"INSERT INTO embeddings (tenant_id, document_chunk_id, embedding_model, embedding_provider, embedding_dimension, embedding)
                  SELECT x.t, x.c, @model, 'gemini', @dim, x.v::vector FROM unnest(@tenants, @chunks, @vecs) AS x(t, c, v)");
            save.Parameters.AddWithValue("model", _embedder.Model);
            save.Parameters.AddWithValue("dim", _embedder.Dimensions);
            save.Parameters.AddWithValue("tenants", keep.Select(i => tenants[i]).ToArray());
            save.Parameters.AddWithValue("chunks", keep.Select(i => ids[i]).ToArray());
            save.Parameters.AddWithValue("vecs", keep.Select(i => VectorText(vectors[i]!)).ToArray());
            await save.ExecuteNonQueryAsync(ct);
            return keep.Length;
        });
}


public sealed class UnavailableKnowledgeStore : IKnowledgeStore
{
    private static KnowledgeUnavailableException Down() =>
        new("Company documents are not set up on this server yet.");

    public Task<KnowledgeSaveOutcome> SaveAsync(long tenantId, long userId, NewKnowledgeDocument document, CancellationToken cancellationToken = default) => throw Down();
    public Task<IReadOnlyList<KnowledgeDocument>> ListAsync(long tenantId, CancellationToken cancellationToken = default) => throw Down();
    public Task<KnowledgeDocumentDetail?> GetAsync(long tenantId, long documentId, CancellationToken cancellationToken = default) => throw Down();
    public Task<bool> DeleteAsync(long tenantId, long documentId, CancellationToken cancellationToken = default) => throw Down();
    public Task<IReadOnlyList<KnowledgePassage>> SearchAsync(long tenantId, IReadOnlyCollection<string> roles, bool seesAll, string query, int limit, CancellationToken cancellationToken = default) => throw Down();
}
