using System.Runtime.CompilerServices;
using Minio;
using Minio.DataModel.Args;

namespace Persona.Api.Services.Employees;

public interface IEmployeePhotoStorage
{
    Task Put(string key, Stream stream, long size, CancellationToken ct,
        string contentType = "application/octet-stream");

    Task<byte[]> Get(string key, CancellationToken ct);
    IAsyncEnumerable<string> OldObjects(CancellationToken ct);
    Task Remove(string key, CancellationToken ct);
}

public sealed class EmployeePhotoStorage(IConfiguration config) : IEmployeePhotoStorage
{
    private string Bucket => config["EmployeePhotos:Bucket"] ?? "employee-photos";

    public async Task Put(string key, Stream stream, long size, CancellationToken ct,
        string contentType = "application/octet-stream")
    {
        using var client = Client();
        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(Bucket), ct))
            try
            {
                await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(Bucket), ct);
            }
            catch
            {
                if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(Bucket), ct)) throw;
            }

        await client.PutObjectAsync(
            new PutObjectArgs().WithBucket(Bucket).WithObject(key).WithStreamData(stream).WithObjectSize(size)
                .WithContentType(contentType), ct);
    }

    public async Task<byte[]> Get(string key, CancellationToken ct)
    {
        using var client = Client();
        using var result = new MemoryStream();
        await client.GetObjectAsync(
            new GetObjectArgs().WithBucket(Bucket).WithObject(key).WithCallbackStream(stream => stream.CopyTo(result)),
            ct);
        return result.ToArray();
    }

    public async IAsyncEnumerable<string> OldObjects([EnumeratorCancellation] CancellationToken ct)
    {
        using var client = Client();
        if (!await client.BucketExistsAsync(new BucketExistsArgs().WithBucket(Bucket), ct)) yield break;
        await foreach (var item in client.ListObjectsEnumAsync(
                           new ListObjectsArgs().WithBucket(Bucket).WithRecursive(true), ct))
            if (!item.IsDir && DateTimeOffset.TryParse(item.LastModified, out var modified) &&
                modified < DateTimeOffset.UtcNow.AddDays(-1))
                yield return item.Key;
    }

    public async Task Remove(string key, CancellationToken ct)
    {
        using var client = Client();
        await client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(Bucket).WithObject(key), ct);
    }

    private IMinioClient Client()
    {
        return new MinioClient().WithEndpoint(config["EmployeePhotos:Endpoint"] ?? "localhost:9000")
            .WithCredentials(
                config["EmployeePhotos:AccessKey"] ??
                throw new InvalidOperationException("Attachment storage is not configured."),
                config["EmployeePhotos:SecretKey"] ??
                throw new InvalidOperationException("Attachment storage is not configured."))
            .WithSSL(config.GetValue<bool>("EmployeePhotos:UseSsl")).Build();
    }
}