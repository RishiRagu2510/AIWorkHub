using System.Text;
using Persona.Contracts;
using Persona.Domain.Employees;
using Persona.Infrastructure.Persistence;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed record EmployeePhotoFile(byte[] Bytes, string ContentType);

public sealed class EmployeePhotoService(
    PersonaDbContext db,
    EmployeeAccess access,
    ITenantContext actor,
    IEmployeePhotoStorage storage,
    EmployeeEvents events)
{
    public const long MaxBytes = 2 * 1024 * 1024;

    public static string? ImageType(byte[] b)
    {
        if (b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return "image/png";
        if (b.Length >= 3 && b[0] == 255 && b[1] == 216 && b[2] == 255) return "image/jpeg";
        if (b.Length >= 12 && Encoding.ASCII.GetString(b, 0, 4) == "RIFF" &&
            Encoding.ASCII.GetString(b, 8, 4) == "WEBP") return "image/webp";
        return null;
    }

    public async Task<EmployeePhotoDto> Upload(long id, long length, Func<Stream, CancellationToken, Task> copyTo,
        CancellationToken ct)
    {
        if (length == 0 || length > MaxBytes) throw new EmployeeProblem(400, "Choose an image up to 2 MB.");
        using var buffer = new MemoryStream();
        await copyTo(buffer, ct);
        var bytes = buffer.ToArray();
        if (ImageType(bytes) is null) throw new EmployeeProblem(400, "Choose a PNG, JPEG or WebP image.");
        var image = Guid.NewGuid();
        var key = $"{actor.TenantId}/{id}/{image:N}";
        var uploaded = false;
        Employee employee = null!;
        try
        {
            await db.ExecuteInTransactionAsync(async t =>
            {
                await access.Lock(t);
                employee = await access.Get(id, t);
                await access.RequireProfile(employee, true, t);
                access.CheckVersion(employee);
                buffer.Position = 0;
                await storage.Put(key, buffer, bytes.Length, t);
                uploaded = true;
                employee.ProfilePhotoUrl = $"/api/employees/{id}/photo/{image:N}";
                employee.UpdatedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(t);
                await events.Write(employee, "EMPLOYEE_PHOTO_UPDATED", null, new { employee.Id }, t);
            }, ct);
        }
        catch
        {
            if (uploaded)
                try
                {
                    await storage.Remove(key, CancellationToken.None);
                }
                catch
                {
                }

            throw;
        }

        return new EmployeePhotoDto(employee.ProfilePhotoUrl, employee.Version);
    }


    public async Task<EmployeePhotoFile?> Read(long id, Guid image, CancellationToken ct)
    {
        var employee = await access.Get(id, ct);
        if (employee.ProfilePhotoUrl != $"/api/employees/{id}/photo/{image:N}") return null;
        var bytes = await storage.Get($"{actor.TenantId}/{id}/{image:N}", ct);
        var type = ImageType(bytes);
        return type is null ? null : new EmployeePhotoFile(bytes, type);
    }
}