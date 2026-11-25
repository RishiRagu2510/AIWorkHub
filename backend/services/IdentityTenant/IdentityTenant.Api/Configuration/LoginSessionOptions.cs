namespace IdentityTenant.Api.Configuration;

public sealed class LoginSessionOptions
{
    public int IdleHours { get; set; } = 24;
    public int AbsoluteDays { get; set; } = 7;
}