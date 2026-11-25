namespace Jira.Domain.Enums;

public static class IssuePriority
{
    public const string Lowest = "LOWEST";
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";
    public const string Highest = "HIGHEST";

    public static bool IsValid(string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            return false;
        }

        return priority.Trim().ToUpperInvariant() switch
        {
            Lowest => true,
            Low => true,
            Medium => true,
            High => true,
            Highest => true,
            _ => false
        };
    }

    public static string Normalize(string priority)
    {
        return priority.Trim().ToUpperInvariant();
    }
}