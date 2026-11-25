using Authorization;
using Persona.Api.Services.Employees;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Persona.Api.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = "Bearer")]
[EmployeeErrors]
[Route("api/employees/{id:long}/photo")]
public sealed class EmployeePhotosController(EmployeePhotoService photos) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(2200000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 2200000)]
    public async Task<IActionResult> Upload(long id, IFormFile file, CancellationToken ct)
    {
        return Ok(await photos.Upload(id, file.Length, (target, token) => file.CopyToAsync(target, token), ct));
    }

    [HttpGet("{image:guid}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> Read(long id, Guid image, CancellationToken ct)
    {
        var photo = await photos.Read(id, image, ct);
        if (photo is null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(photo.Bytes, photo.ContentType);
    }
}