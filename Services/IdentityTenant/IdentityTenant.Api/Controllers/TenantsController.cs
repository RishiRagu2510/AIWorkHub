using Authorization;
using IdentityTenant.Application.DTOs;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace IdentityTenant.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class TenantsController : ControllerBase
{
    private readonly IdentityTenantDbContext _db;
    private readonly ITenantContext _tenantContext;
    private readonly TenantService _tenantService;

    public TenantsController(
        TenantService tenantService,
        ITenantContext tenantContext,
        IdentityTenantDbContext db)
    {
        _tenantService = tenantService;
        _tenantContext = tenantContext;
        _db = db;
    }

    [HttpGet("theme")]
    public async Task<IActionResult> GetTheme(CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();

        var settings = await _db.TenantSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == _tenantContext.TenantId, cancellationToken);

        var theme = "light";
        if (settings?.Settings != null &&
            settings.Settings.TryGetValue("theme", out var themeObj) &&
            themeObj is not null)
        {
            var raw = themeObj.ToString()?.Trim().ToLowerInvariant();
            if (raw == "dark") theme = "dark";
        }

        return Ok(new { theme });
    }

    [HttpPut("theme")]
    public async Task<IActionResult> UpdateTheme(
        [FromBody] ThemeUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();

        if (!_tenantContext.Roles.Any(r => string.Equals(r, "ADMIN", StringComparison.OrdinalIgnoreCase)))
            return Forbid();

        var desired = request?.Theme?.Trim().ToLowerInvariant() == "dark" ? "dark" : "light";

        var settings = await _db.TenantSettings
            .FirstOrDefaultAsync(s => s.TenantId == _tenantContext.TenantId, cancellationToken);

        if (settings is null)
        {
            settings = new TenantSettings(_tenantContext.TenantId);
            settings.Update(settings.Timezone, settings.Locale,
                new Dictionary<string, object?> { ["theme"] = desired });
            _db.TenantSettings.Add(settings);
        }
        else
        {
            var dict = new Dictionary<string, object?>(settings.Settings)
            {
                ["theme"] = desired
            };
            settings.Update(settings.Timezone, settings.Locale, dict);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new { theme = desired });
    }

    [RequirePermission("TENANT_VIEW")]
    [HttpGet("{tenantId:long}")]
    public async Task<ActionResult<TenantDto>> GetById(
        long tenantId,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();

        if (tenantId != _tenantContext.TenantId) return Forbid();

        var tenant = await _tenantService.GetByIdAsync(
            tenantId,
            cancellationToken);

        if (tenant is null) return NotFound();

        return Ok(tenant);
    }

    [RequirePermission("TENANT_VIEW")]
    [HttpGet("me")]
    public async Task<ActionResult<TenantDto>> GetCurrentTenant(
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsAuthenticated) return Unauthorized();

        var tenant = await _tenantService.GetByIdAsync(
            _tenantContext.TenantId,
            cancellationToken);

        if (tenant is null) return NotFound();

        return Ok(tenant);
    }

    public sealed record ThemeUpdateRequest(string? Theme);
}