using Persona.Api.Services.Employees;
namespace Backend.Authorization.Tests;
public sealed class TestPhotoStorage : IEmployeePhotoStorage
{
 private readonly Dictionary<string,byte[]> photos=new();
 public async Task Put(string key,Stream stream,long size,CancellationToken ct,string contentType = "application/octet-stream"){using var buffer=new MemoryStream();await stream.CopyToAsync(buffer,ct);photos[key]=buffer.ToArray();}
 public Task<byte[]> Get(string key,CancellationToken ct)=>Task.FromResult(photos[key]);
 public Task Remove(string key,CancellationToken ct){photos.Remove(key);return Task.CompletedTask;}
 public async IAsyncEnumerable<string> OldObjects([System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken ct){await Task.CompletedTask;yield break;}
}
