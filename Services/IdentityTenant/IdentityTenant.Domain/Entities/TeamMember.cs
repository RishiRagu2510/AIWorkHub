using IdentityTenant.Domain.Enums;

namespace IdentityTenant.Domain.Entities;

public sealed class TeamMember
{
    private TeamMember()
    {
    }

    public TeamMember(
        long teamId,
        long userId,
        TeamMemberRole role = TeamMemberRole.Member)
    {
        if (teamId <= 0) throw new ArgumentOutOfRangeException(nameof(teamId));

        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));

        TeamId = teamId;
        UserId = userId;
        Role = role;
        JoinedAt = DateTimeOffset.UtcNow;
    }

    public long TeamId { get; private set; }

    public long UserId { get; private set; }

    public TeamMemberRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }
}