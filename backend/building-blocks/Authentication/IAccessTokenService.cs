using System.Security.Claims;

namespace Authentication;

public interface IAccessTokenService
{
    string CreateAccessToken(ClaimsPrincipal principal);
}