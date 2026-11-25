namespace IdentityTenant.Application.DTOs;

public sealed record TenantDto(
    long Id,
    string Name,
    string Slug,
    string Status);