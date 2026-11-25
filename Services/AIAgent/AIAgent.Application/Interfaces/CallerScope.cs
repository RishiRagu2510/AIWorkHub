namespace AIAgent.Application.Interfaces;


public record VerifiedCaller(
    long UserId,
    string Role,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    long TenantId = 0,
    IReadOnlyCollection<string>? AllowedFeatures = null,
    IReadOnlyCollection<string>? AllowedPortals = null);

public interface ICallerIdentityProvider
{
    
    bool HasCredentials { get; }

    Task<VerifiedCaller?> VerifyAsync(CancellationToken cancellationToken = default);
}

public static class IssueVisibility
{
    
    public const string OwnerArgument = "__visibleToUserId";

    
    public const string UnknownCallerArgument = "__unknownCaller";

    
    public const string CallerUserIdArgument = "__callerUserId";

    
    public const string CallerRolesArgument = "__callerRoles";

    
    public const string CallerTenantIdArgument = "__callerTenantId";

    public static readonly string[] ReservedArguments =
        [OwnerArgument, UnknownCallerArgument, CallerUserIdArgument, CallerRolesArgument, CallerTenantIdArgument];

    private static readonly HashSet<string> BroadRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADMIN", "MANAGER", "PEOPLE_MANAGER", "PROJECT_MANAGER", "ENGINEERING_MANAGER", "EM", "PM", "PGM", "CEO", "CTO"
    };

    public static bool CanSeeAllIssues(IEnumerable<string> roles) => roles.Any(r => BroadRoles.Contains(r.Trim()));

    public static Dictionary<string, object?> ArgumentsFor(IDictionary<string, object?> given, IReadOnlyCollection<string> roles, long? userId, long tenantId = 0)
    {
        var args = new Dictionary<string, object?>(given);
        foreach (var reserved in ReservedArguments) args.Remove(reserved);
        args[CallerRolesArgument] = string.Join(",", roles);
        if (userId.HasValue) args[CallerUserIdArgument] = userId.Value;
        if (tenantId > 0) args[CallerTenantIdArgument] = tenantId;
        if (!CanSeeAllIssues(roles))
        {
            if (userId.HasValue) args[OwnerArgument] = userId.Value;
            else args[UnknownCallerArgument] = true;
        }
        return args;
    }
}
