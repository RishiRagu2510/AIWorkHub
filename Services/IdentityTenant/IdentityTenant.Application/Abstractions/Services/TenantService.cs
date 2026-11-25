using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.DTOs;

namespace IdentityTenant.Application.Services;

public sealed class TenantService
{
    private readonly ITenantRepository _tenantRepository;

    public TenantService(
        ITenantRepository tenantRepository)
    {
        _tenantRepository = tenantRepository;
    }

    public async Task<TenantDto?> GetByIdAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantRepository.GetByIdAsync(
            tenantId,
            cancellationToken);

        if (tenant is null)
        {
            return null;
        }

        return new TenantDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Status.ToString().ToUpperInvariant());
    }
}