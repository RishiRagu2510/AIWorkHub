using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using TenantContext;

namespace IdentityTenant.Api.Authentication;

public sealed class CompactSessionTicketFormat(ISecureDataFormat<AuthenticationTicket> inner)
    : ISecureDataFormat<AuthenticationTicket>
{
    public string Protect(AuthenticationTicket data)
    {
        return inner.Protect(Compact(data));
    }

    public string Protect(AuthenticationTicket data, string? purpose)
    {
        return inner.Protect(Compact(data), purpose);
    }

    public AuthenticationTicket? Unprotect(string? text)
    {
        return inner.Unprotect(text);
    }

    public AuthenticationTicket? Unprotect(string? text, string? purpose)
    {
        return inner.Unprotect(text, purpose);
    }

    private static AuthenticationTicket Compact(AuthenticationTicket ticket)
    {
        var claims = ticket.Principal.Claims.Where(c => c.Type is
            TenantClaimTypes.UserId or TenantClaimTypes.TenantId or
            LoginSessionService.SessionClaim or "aiworkhub:company_verified" or "aiworkhub:login_method");
        var identity = new ClaimsIdentity(claims, ticket.AuthenticationScheme, "name", TenantClaimTypes.Role);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), ticket.Properties, ticket.AuthenticationScheme);
    }
}