using Persona.Domain.Careers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Persona.Infrastructure.Persistence.Configurations;
public sealed class CareerProfileConfiguration : IEntityTypeConfiguration<CareerProfile>
{
    public void Configure(EntityTypeBuilder<CareerProfile> b)
    {
        b.ToTable("career_profiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => new { x.Code, x.Name }).IsUnique().HasDatabaseName("uq_career_profiles_code_name");
        b.HasIndex(x => x.Code).HasDatabaseName("IX_career_profiles_code");
    }
}
public sealed class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> b)
    {
        b.ToTable("grades");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Family).HasColumnName("family").HasMaxLength(100);
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(x => x.DisplayOrder).HasColumnName("display_order");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_grades_code");
    }
}
public sealed class CareerLevelConfiguration : IEntityTypeConfiguration<CareerLevel>
{
    public void Configure(EntityTypeBuilder<CareerLevel> b)
    {
        b.ToTable("career_levels");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.CareerProfileId).HasColumnName("career_profile_id");
        b.Property(x => x.GradeId).HasColumnName("grade_id");
        b.Property(x => x.CareerTitle).HasColumnName("career_title").HasMaxLength(200).IsRequired();
        b.Property(x => x.DisplayOrder).HasColumnName("display_order");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => new { x.CareerProfileId, x.GradeId }).IsUnique().HasDatabaseName("uq_career_levels_profile_grade");
        b.HasAlternateKey(x => new { x.CareerProfileId, x.Id }).HasName("uq_career_levels_profile_id");
        b.HasOne(x => x.CareerProfile).WithMany().HasForeignKey(x => x.CareerProfileId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Grade).WithMany().HasForeignKey(x => x.GradeId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> b)
    {
        b.ToTable("skills");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        b.Property(x => x.Category).HasColumnName("category").HasMaxLength(100);
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(4000);
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_skills_code");
    }
}
public sealed class CareerLevelSkillConfiguration : IEntityTypeConfiguration<CareerLevelSkill>
{
    public void Configure(EntityTypeBuilder<CareerLevelSkill> b)
    {
        b.ToTable("career_level_skills");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        b.Property(x => x.CareerLevelId).HasColumnName("career_level_id");
        b.Property(x => x.SkillId).HasColumnName("skill_id");
        b.Property(x => x.RequiredProficiency).HasColumnName("required_proficiency");
        b.Property(x => x.TargetProficiency).HasColumnName("target_proficiency");
        b.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        b.HasIndex(x => new { x.CareerLevelId, x.SkillId }).IsUnique().HasDatabaseName("uq_career_level_skills_level_skill");
        b.HasOne(x => x.CareerLevel).WithMany().HasForeignKey(x => x.CareerLevelId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Restrict);
    }
}
