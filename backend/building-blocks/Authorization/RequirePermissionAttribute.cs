using Microsoft.AspNetCore.Authorization;

namespace Authorization;

[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = true)]
public sealed class RequirePermissionAttribute
    : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permissionCode, params string[] alternatives)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
        {
            throw new ArgumentException(
                "Permission code is required.",
                nameof(permissionCode));
        }

        Policy =
            $"{PermissionPolicyProvider.PolicyPrefix}" +
            string.Join("|", new[] { permissionCode }.Concat(alternatives).Select(code =>
                !string.IsNullOrWhiteSpace(code) && !code.Contains('|') ? code.Trim().ToUpperInvariant() :
                throw new ArgumentException("Permission codes must be nonempty and cannot contain |.")));
    }
}