using System.Security.Claims;
using IdentityTenant.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using TenantContext;

namespace Backend.Authorization.Tests;
public sealed class CompactSessionTicketTests
{
    [Fact]
    public void CookieRemainsSmallAndPreservesSessionIdentityAndExpiry()
    {
        var format = new CompactSessionTicketFormat(new TicketDataFormat(new EphemeralDataProtectionProvider().CreateProtector("test")));
        var claims = new List<Claim> {
            new(TenantClaimTypes.UserId,"1"), new(TenantClaimTypes.TenantId,"1"),
            new(LoginSessionService.SessionClaim,new string('A',64)), new("aiworkhub:company_verified","1") };
        claims.AddRange(Enumerable.Range(0,100).Select(n=>new Claim(TenantClaimTypes.Permission,$"PERMISSION_{n}")));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims,"Cookies"));
        var expires = DateTimeOffset.UtcNow.AddDays(1);
        var ticket = new AuthenticationTicket(principal,new AuthenticationProperties { ExpiresUtc=expires },"Cookies");
        var cookie = format.Protect(ticket);
        Assert.True(cookie.Length < 2000);
        var restored = format.Unprotect(cookie)!;
        Assert.Equal("1",restored.Principal.FindFirstValue(TenantClaimTypes.UserId));
        Assert.Equal(new string('A',64),restored.Principal.FindFirstValue(LoginSessionService.SessionClaim));
        Assert.Empty(restored.Principal.FindAll(TenantClaimTypes.Permission));
        Assert.Equal(100,principal.FindAll(TenantClaimTypes.Permission).Count());
        Assert.Equal(ticket.Properties.ExpiresUtc,restored.Properties.ExpiresUtc);
    }
}
