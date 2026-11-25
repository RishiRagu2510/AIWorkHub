using System.Security.Claims;
using Authorization;
using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Application.Abstractions.Services;
using IdentityTenant.Application.Plans;
using IdentityTenant.Application.Services;
using IdentityTenant.Domain.Enums;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace IdentityTenant.Api.Authentication;

public sealed class CompanyPrincipalService(
    IUserProvisioningService provisioning,
    IUserRepository users,
    ITenantRepository tenants,
    IUserRoleRepository roles,
    IUserPermissionRepository permissions,
    IdentityTenantDbContext database,
    IConfiguration configuration)
{
    public async Task<ClaimsPrincipal> SignInGoogleAsync(ClaimsPrincipal google, CancellationToken ct)
    {
        var result = await provisioning.ProvisionAsync(
            google.FindFirstValue(ClaimTypes.NameIdentifier) ?? google.FindFirstValue("sub") ?? "",
            google.FindFirstValue(ClaimTypes.Email) ?? google.FindFirstValue("email") ?? "",
            google.FindFirstValue("name") ?? google.FindFirstValue(ClaimTypes.Name) ?? "Google user",
            string.Equals(google.FindFirstValue("email_verified"), "true", StringComparison.OrdinalIgnoreCase), ct,
            configuration["Authentication:Google:ClientId"]);
        return await BuildAsync(result.UserId, result.TenantId, result.Email, result.DisplayName, ct);
    }


    public async Task<ClaimsPrincipal> SignInPasswordAsync(long userId, long tenantId, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct);
        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (user is null || user.TenantId != tenantId || user.Status != UserStatus.Active || !user.EmailVerified ||
            tenant?.Status != TenantStatus.Active)
            throw new LoginRejectedException("INVALID_CREDENTIALS", "Email or password is incorrect.");
        var principal = await BuildAsync(userId, tenantId, user.Email, user.DisplayName, ct);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("aiworkhub:login_method", "PASSWORD"));
        return principal;
    }

    public async Task<ClaimsPrincipal?> RefreshAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        if (principal.FindFirstValue("aiworkhub:company_verified") != "1" ||
            principal.FindAll(TenantClaimTypes.UserId).Count() != 1 ||
            principal.FindAll(TenantClaimTypes.TenantId).Count() != 1 ||
            !long.TryParse(principal.FindFirstValue(TenantClaimTypes.UserId), out var userId) ||
            !long.TryParse(principal.FindFirstValue(TenantClaimTypes.TenantId), out var tenantId)) return null;
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || user.TenantId != tenantId || user.Status != UserStatus.Active ||
            !user.EmailVerified) return null;
        var tenant = await tenants.GetByIdAsync(tenantId, ct);
        if (tenant is null || tenant.Status != TenantStatus.Active) return null;
        try
        {
            return await BuildAsync(user.Id, tenant.Id, user.Email, user.DisplayName, ct);
        }
        catch (LoginRejectedException)
        {
            return null;
        }
    }

    private async Task<ClaimsPrincipal> BuildAsync(long userId, long tenantId, string email, string name,
        CancellationToken ct)
    {
        var assignedRoles = await roles.GetRoleCodesAsync(tenantId, userId, ct);
        var assignedPermissions = await permissions.GetPermissionCodesAsync(tenantId, userId, ct);

        var identity = new ClaimsIdentity("AIWorkHub", "name", TenantClaimTypes.Role);
        identity.AddClaim(new Claim(TenantClaimTypes.UserId, userId.ToString()));
        identity.AddClaim(new Claim(TenantClaimTypes.TenantId, tenantId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Email, email));
        identity.AddClaim(new Claim("name", name));
        identity.AddClaim(new Claim("aiworkhub:company_verified", "1"));
        foreach (var role in assignedRoles.Distinct()) identity.AddClaim(new Claim(TenantClaimTypes.Role, role));


        var tenantSettings = await database.TenantSettings
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Settings)
            .FirstOrDefaultAsync(ct);
        var licensedPortals = TenantPlanSettings.ReadList(tenantSettings, TenantPlanSettings.PortalsKey);
        foreach (var portal in licensedPortals) identity.AddClaim(new Claim(TenantClaimTypes.Portal, portal));

        foreach (var feature in TenantPlanSettings.ReadList(tenantSettings, TenantPlanSettings.FeaturesKey))
            identity.AddClaim(new Claim(TenantClaimTypes.Feature, feature));


        foreach (var permission in assignedPermissions.Distinct()
                     .Where(p => PortalLicense.IsPermissionLicensed(licensedPortals, p)))
            identity.AddClaim(new Claim(TenantClaimTypes.Permission, permission));

        return new ClaimsPrincipal(identity);
    }
}