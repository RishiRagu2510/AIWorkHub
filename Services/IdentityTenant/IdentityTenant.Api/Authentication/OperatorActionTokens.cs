using System.Security.Cryptography;
using System.Text;

namespace IdentityTenant.Api.Authentication;

public enum OperatorAction
{
    Approve,
    Decline
}

public sealed record OperatorActionClaim(
    long TenantId,
    string RequestId,
    OperatorAction Action,
    DateTimeOffset ExpiresAt);

public sealed class OperatorActionTokens(IConfiguration configuration)
{
    private const string Context = "aiworkhub.access-request-action.v1\n";
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private byte[]? Key =>
        configuration["Platform:OperatorKey"] is { Length: > 0 } key && !string.IsNullOrWhiteSpace(key)
            ? Encoding.UTF8.GetBytes(key)
            : null;

    public bool IsAvailable => Key is not null;

    public string? Create(long tenantId, string requestId, OperatorAction action, DateTimeOffset now)
    {
        if (Key is null) return null;
        var payload =
            Encoding.UTF8.GetBytes(
                $"v1|{tenantId}|{requestId}|{(action == OperatorAction.Approve ? "approve" : "decline")}|{now.Add(Lifetime).ToUnixTimeSeconds()}");
        return Base64Url(payload) + "." + Base64Url(Mac(Key, payload));
    }


    public OperatorActionClaim? Verify(string? token, DateTimeOffset now)
    {
        if (Key is not { } key || string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 2) return null;
            var payload = FromBase64Url(parts[0]);
            if (!CryptographicOperations.FixedTimeEquals(Mac(key, payload), FromBase64Url(parts[1]))) return null;

            var fields = Encoding.UTF8.GetString(payload).Split('|');
            if (fields.Length != 5 || fields[0] != "v1") return null;
            var action = fields[3] switch
            {
                "approve" => OperatorAction.Approve, "decline" => OperatorAction.Decline, _ => (OperatorAction?)null
            };
            if (action is null || !long.TryParse(fields[1], out var tenantId) ||
                !long.TryParse(fields[4], out var expires)) return null;
            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expires);
            return expiresAt <= now ? null : new OperatorActionClaim(tenantId, fields[2], action.Value, expiresAt);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentOutOfRangeException or ArgumentException)
        {
            return null;
        }
    }

    private static byte[] Mac(byte[] key, byte[] payload)
    {
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Context).Concat(payload).ToArray());
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] FromBase64Url(string text)
    {
        var padded = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}