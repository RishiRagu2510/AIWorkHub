namespace IdentityTenant.Application.Abstractions.Services;

public interface IUserProvisioningService
{
    Task<UserProvisioningResult> ProvisionAsync(
        string providerExternalSubject,
        string email,
        string displayName,
        bool emailVerified,
        CancellationToken cancellationToken, string? clientId = null);
}

public sealed record UserProvisioningResult(
    long UserId,
    long TenantId,
    string Email,
    string DisplayName);