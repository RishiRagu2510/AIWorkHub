namespace IdentityTenant.Application.Services;

public sealed class LoginRejectedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
