using Microsoft.AspNetCore.Authorization;

namespace Authorization;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            throw new ArgumentException(
                "Permission code is required.",
                nameof(permissionCode));
        }

        PermissionCode = permissionCode
            .Trim()
            .ToUpperInvariant();
    }

    public string PermissionCode { get; }
}