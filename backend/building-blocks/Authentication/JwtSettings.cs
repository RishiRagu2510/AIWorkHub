namespace Authentication;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public const string KeyId = "AIWorkHub-JWT-V1";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    public string SigningKey { get; init; } = string.Empty;

    public int ExpirationMinutes { get; init; } = 30;
}