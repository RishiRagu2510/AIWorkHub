using IdentityTenant.Api.Authentication;
using Microsoft.AspNetCore.Http;

namespace Backend.Authorization.Tests;

public sealed class PortalCookieTests
{
    private static DefaultHttpContext Context(int port, string? cookies = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Host = new HostString("localhost", port);
        if (cookies is not null) context.Request.Headers.Cookie = cookies;
        return context;
    }

    [Fact]
    public void LoginAndLogoutOnlyChangeTheCurrentPortalsCookie()
    {
        var manager = new PortalCookieManager();
        var cookies = new List<string>();
        foreach (var port in new[] { 5171, 5173, 5174, 5175 })
        {
            var context = Context(port);
            manager.AppendResponseCookie(context, "AIWorkHub.Session", $"session-{port}", new CookieOptions { HttpOnly = true });
            cookies.Add(context.Response.Headers.SetCookie.ToString().Split(';')[0]);
        }
        Assert.Equal(4, cookies.Select(c => c.Split('=')[0]).Distinct().Count());
        var jar = string.Join("; ", cookies);
        foreach (var port in new[] { 5171, 5173, 5174, 5175 })
            Assert.Equal($"session-{port}", manager.GetRequestCookie(Context(port, jar), "AIWorkHub.Session"));
        var logout = Context(5174, jar);
        manager.DeleteCookie(logout, "AIWorkHub.Session", new CookieOptions());
        var deletedName = logout.Response.Headers.SetCookie.ToString().Split('=')[0];
        Assert.Equal(cookies[2].Split('=')[0], deletedName);
        var remaining = string.Join("; ", cookies.Where(c => !c.StartsWith(deletedName + "=")));
        Assert.Null(manager.GetRequestCookie(Context(5174, remaining), "AIWorkHub.Session"));
        Assert.Equal("session-5171", manager.GetRequestCookie(Context(5171, remaining), "AIWorkHub.Session"));
        Assert.Equal("session-5173", manager.GetRequestCookie(Context(5173, remaining), "AIWorkHub.Session"));
        Assert.Equal("session-5175", manager.GetRequestCookie(Context(5175, remaining), "AIWorkHub.Session"));
    }

    [Fact]
    public void LegacySharedCookieIsNotUsedAsAPortalSession()
    {
        var manager = new PortalCookieManager();
        Assert.Null(manager.GetRequestCookie(Context(5174, "AIWorkHub.Session=old-shared-session"), "AIWorkHub.Session"));
    }
}
