using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Authentication;


public sealed record OnboardingApprovalClaims(long TenantId, long IssueId, string WorkEmail, long? TeamId, DateTimeOffset ExpiresAt);

public sealed class OnboardingApprovalSigner
{
    public const string Header = "X-Onboarding-Approval";
    public const string ConfigKey = "Services:OnboardingApprovalKey";
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);
    private const int Version = 1;
    private const int MinimumKeyBytes = 32;
    private static readonly byte[] Context = Encoding.UTF8.GetBytes("aiworkhub.onboarding-approval.v1\n");

    private readonly byte[] _key;

    public OnboardingApprovalSigner(string? key)
    {
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes)
            throw new InvalidOperationException($"{ConfigKey} must be set to a secret of at least {MinimumKeyBytes} characters, the same in Jira and Identity.");
        _key = Encoding.UTF8.GetBytes(key);
    }

    private sealed record Payload(int V, long T, long I, string E, long? M, long X);

    public string Sign(long tenantId, long issueId, string workEmail, long? teamId, DateTimeOffset now)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Payload(Version, tenantId, issueId, workEmail, teamId, now.Add(Lifetime).ToUnixTimeSeconds()));
        return $"{Base64Url(payload)}.{Base64Url(Mac(payload))}";
    }

    
    public OnboardingApprovalClaims? Verify(string? token, long tenantId, long issueId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var parts = token.Split('.');
        if (parts.Length != 2) return null;
        try
        {
            var payload = FromBase64Url(parts[0]);
            var signature = FromBase64Url(parts[1]);
            if (!CryptographicOperations.FixedTimeEquals(Mac(payload), signature)) return null;
            var claims = JsonSerializer.Deserialize<Payload>(payload);
            if (claims is null || claims.V != Version || claims.T != tenantId || claims.I != issueId || string.IsNullOrWhiteSpace(claims.E)) return null;
            var expires = DateTimeOffset.FromUnixTimeSeconds(claims.X);
            return expires > now ? new OnboardingApprovalClaims(claims.T, claims.I, claims.E, claims.M, expires) : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return null;   
        }
    }

    private byte[] Mac(byte[] payload) => HMACSHA256.HashData(_key, Context.Concat(payload).ToArray());
    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}

public static class OnboardingApprovalExtensions
{
    
    public static IServiceCollection AddOnboardingApproval(this IServiceCollection services, IConfiguration configuration) =>
        services.AddSingleton(new OnboardingApprovalSigner(configuration[OnboardingApprovalSigner.ConfigKey]));
}
