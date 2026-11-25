using Audit.Api.Controllers;
using Authorization;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;

namespace Audit.Api.Tests;

public sealed class AuditLogsControllerAuthorizationTests
{
    [Fact]
    public void Controller_RequiresAuthentication()
    {
        var authorizeAttribute =
            typeof(AuditLogsController)
                .GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorizeAttribute);
    }

    [Fact]
    public void Search_RequiresPermissionAttribute()
    {
        var method =
            typeof(AuditLogsController)
                .GetMethod("Search");

        Assert.NotNull(method);

        var permissionAttribute =
            method!.GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(permissionAttribute);
    }

    [Fact]
    public void Search_RequiresAuditViewPermission()
    {
        var method =
            typeof(AuditLogsController)
                .GetMethod("Search");

        Assert.NotNull(method);

        var permissionAttribute =
            method!.GetCustomAttribute<RequirePermissionAttribute>();

        Assert.NotNull(permissionAttribute);
        Assert.NotNull(permissionAttribute!.Policy);
        Assert.EndsWith(
            "AUDIT_VIEW",
            permissionAttribute.Policy,
            StringComparison.Ordinal);
    }
}
