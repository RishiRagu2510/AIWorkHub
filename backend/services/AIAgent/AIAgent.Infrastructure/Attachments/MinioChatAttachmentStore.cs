using AIAgent.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace AIAgent.Infrastructure.Attachments;

public sealed record ChatAttachmentOptions(string? Endpoint, string? AccessKey, string? SecretKey, string Bucket = "athenna-chat-files", bool UseSsl = false);

public sealed class MinioChatAttachmentStore : IChatAttachmentStore
{
    private const string NameHeader = "X-Amz-Meta-Filename";
    private readonly IMinioClient _client;
    private readonly string _bucket;
    private readonly ILogger<MinioChatAttachmentStore> _logger;

    public MinioChatAttachmentStore(ChatAttachmentOptions options, ILogger<MinioChatAttachmentStore> logger)
    {
        if (string.IsNullOrWhiteSpace(options.AccessKey) || string.IsNullOrWhiteSpace(options.SecretKey))
            throw new ArgumentException("MinIO credentials are required.", nameof(options));
        _client = new MinioClient()
            .WithEndpoint(string.IsNullOrWhiteSpace(options.Endpoint) ? "localhost:9000" : options.Endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(options.UseSsl)
            .WithTimeout(10_000)
            .Build();
        _bucket = options.Bucket;
        _logger = logger;
    }

    private static string Key(long tenantId, long userId, string id) => $"{tenantId}/{userId}/{id}";

    private async Task<T> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (MinioException ex) when (ex is not ObjectNotFoundException)
        {
            _logger.LogError(ex, "Chat attachment storage failed.");
            throw new ChatAttachmentUnavailableException("File storage is not reachable right now.", ex);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or TaskCanceledException or IOException)
        {
            _logger.LogError(ex, "Chat attachment storage failed.");
            throw new ChatAttachmentUnavailableException("File storage is not reachable right now.", ex);
        }
    }

    public Task<ChatAttachment> SaveAsync(long tenantId, long userId, string name, string contentType, Stream content, long size, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            var id = ChatAttachmentRules.NewId();
            await _client.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(Key(tenantId, userId, id))
                .WithStreamData(content)
                .WithObjectSize(size)
                .WithContentType(contentType)
                .WithHeaders(new Dictionary<string, string> { [NameHeader] = Uri.EscapeDataString(name) }), cancellationToken);
            return new ChatAttachment(id, name, contentType, size);
        });

    public Task<(ChatAttachment Info, Stream Content)?> OpenAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default) =>
        RunAsync<(ChatAttachment Info, Stream Content)?>(async () =>
        {
            if (!ChatAttachmentRules.IsValidId(id)) return null;
            var key = Key(tenantId, userId, id);
            try
            {
                var stat = await _client.StatObjectAsync(new StatObjectArgs().WithBucket(_bucket).WithObject(key), cancellationToken);
                var buffer = new MemoryStream();
                await _client.GetObjectAsync(new GetObjectArgs().WithBucket(_bucket).WithObject(key).WithCallbackStream(s => s.CopyTo(buffer)), cancellationToken);
                buffer.Position = 0;

                var stored = stat.MetaData?.FirstOrDefault(kv => kv.Key.Equals("filename", StringComparison.OrdinalIgnoreCase) || kv.Key.EndsWith("-filename", StringComparison.OrdinalIgnoreCase)).Value;
                var name = string.IsNullOrEmpty(stored) ? id : ChatAttachmentRules.SafeName(Uri.UnescapeDataString(stored));
                return (new ChatAttachment(id, name, stat.ContentType ?? "application/octet-stream", stat.Size), buffer);
            }
            catch (ObjectNotFoundException)
            {
                return null;
            }
        });

    public Task<bool> DeleteAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            if (!ChatAttachmentRules.IsValidId(id)) return false;
            var key = Key(tenantId, userId, id);
            try
            {
                await _client.StatObjectAsync(new StatObjectArgs().WithBucket(_bucket).WithObject(key), cancellationToken);
            }
            catch (ObjectNotFoundException)
            {
                return false;
            }
            await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(key), cancellationToken);
            return true;
        });
}


public sealed class UnavailableChatAttachmentStore : IChatAttachmentStore
{
    private static ChatAttachmentUnavailableException Off() => new("File attachments are not set up on this server.");
    public Task<ChatAttachment> SaveAsync(long tenantId, long userId, string name, string contentType, Stream content, long size, CancellationToken cancellationToken = default) => throw Off();
    public Task<(ChatAttachment Info, Stream Content)?> OpenAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default) => throw Off();
    public Task<bool> DeleteAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default) => throw Off();
}
