using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace IdentityTenant.Api.Authentication;

public sealed class PortalCookieManager : ICookieManager
{
    private readonly ChunkingCookieManager inner = new();

    public string? GetRequestCookie(HttpContext context, string key)
    {
        return inner.GetRequestCookie(context, Key(context, key));
    }

    public void AppendResponseCookie(HttpContext context, string key, string? value, CookieOptions options)
    {
        inner.AppendResponseCookie(context, Key(context, key), value, options);
    }

    public void DeleteCookie(HttpContext context, string key, CookieOptions options)
    {
        inner.DeleteCookie(context, Key(context, key), options);
    }

    public static string Key(HttpContext context, string key)
    {
        var authority = (context.Request.Host.Value ?? "").ToLowerInvariant();
        var suffix = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authority)))[..24];
        return $"{key}.{suffix}";
    }
}