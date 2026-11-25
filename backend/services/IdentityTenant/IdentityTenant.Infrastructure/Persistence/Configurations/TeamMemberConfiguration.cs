using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityTenant.Infrastructure.Persistence.Configurations;

public sealed class TeamMemberConfiguration :
    IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("team_members");

        builder.HasKey(x => new
        {
            x.TeamId,
            x.UserId
        });

        builder.Property(x => x.TeamId)
            .HasColumnName("team_id");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasConversion(
                value => value.ToString().ToUpperInvariant(),
                value => Enum.Parse<TeamMemberRole>(value, true))
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        builder.HasOne<Team>()
            .WithMany()
            .HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}