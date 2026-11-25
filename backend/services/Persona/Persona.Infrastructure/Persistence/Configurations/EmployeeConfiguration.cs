using Persona.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Persona.Infrastructure.Persistence.Configurations;
public sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
 public void Configure(EntityTypeBuilder<Employee> b) {
 b.Property(x=>x.CareerProfileId).HasColumnName("career_profile_id");
 b.Property(x=>x.CareerLevelId).HasColumnName("career_level_id");
 b.HasOne(x=>x.CareerProfile).WithMany().HasForeignKey(x=>x.CareerProfileId).OnDelete(DeleteBehavior.Restrict);
 b.HasOne(x=>x.CareerLevel).WithMany().HasForeignKey(x=>new{x.CareerProfileId,x.CareerLevelId})
  .HasPrincipalKey(x=>new{x.CareerProfileId,x.Id}).OnDelete(DeleteBehavior.Restrict);
 b.HasIndex(x=>new{x.CareerProfileId,x.CareerLevelId}).HasDatabaseName("ix_employees_career");
 b.ToTable("employees", t=>t.HasCheckConstraint("ck_employees_career_profile", "career_level_id IS NULL OR career_profile_id IS NOT NULL")); b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Property(x=>x.Id).ValueGeneratedOnAdd();
 b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
 b.Property(x=>x.UserId).HasColumnName("user_id");
 b.Property(x=>x.EmployeeCode).HasColumnName("employee_code").HasMaxLength(4000);
 b.Property(x=>x.FirstName).HasColumnName("first_name").HasMaxLength(4000);
 b.Property(x=>x.LastName).HasColumnName("last_name").HasMaxLength(4000);
 b.Property(x=>x.PreferredName).HasColumnName("preferred_name").HasMaxLength(4000);
 b.Property(x=>x.WorkAddress).HasColumnName("work_address").HasMaxLength(1000);
 b.Property(x=>x.AadharNumber).HasColumnName("aadhar_number").HasMaxLength(50);
 b.Property(x=>x.PermanentAddress).HasColumnName("permanent_address").HasMaxLength(1000);
 b.Property(x=>x.EmergencyContactName).HasColumnName("emergency_contact_name").HasMaxLength(200);
 b.Property(x=>x.EmergencyContactPhone).HasColumnName("emergency_contact_phone").HasMaxLength(50);
 b.Property(x=>x.EmergencyContactRelation).HasColumnName("emergency_contact_relation").HasMaxLength(100);
 b.Property(x=>x.WorkEmail).HasColumnName("work_email").HasMaxLength(4000);
 b.Property(x=>x.Phone).HasColumnName("phone").HasMaxLength(4000);
 b.Property(x=>x.ProfilePhotoUrl).HasColumnName("profile_photo_url").HasMaxLength(4000);
 b.Property(x=>x.DepartmentId).HasColumnName("department_id");
 b.Property(x=>x.DisciplineId).HasColumnName("discipline_id");
 b.Property(x=>x.JobTitleId).HasColumnName("job_title_id");
 b.Property(x=>x.DesignationId).HasColumnName("designation_id");
 b.Property(x=>x.GradeId).HasColumnName("grade_id");
 b.Property(x=>x.LocationId).HasColumnName("location_id");
 b.Property(x=>x.ManagerEmployeeId).HasColumnName("manager_employee_id");
 b.Property(x=>x.EmploymentType).HasColumnName("employment_type").HasMaxLength(4000);
 b.Property(x=>x.EmploymentStatus).HasColumnName("employment_status").HasMaxLength(4000);
 b.Property(x=>x.JoiningDate).HasColumnName("joining_date");
 b.Property(x=>x.EndDate).HasColumnName("end_date");
 b.Property(x=>x.IsDeleted).HasColumnName("is_deleted");
 b.Property(x=>x.CreatedAt).HasColumnName("created_at");
 b.Property(x=>x.UpdatedAt).HasColumnName("updated_at");
 b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.TenantId).HasColumnName("tenant_id");
 b.HasIndex(x=>new{x.TenantId,x.EmployeeCode}).IsUnique(); b.HasIndex(x=>new{x.TenantId,x.UserId}).IsUnique();
 }
}
public sealed class EmployeeReferenceConfiguration : IEntityTypeConfiguration<EmployeeReference>
{
 public void Configure(EntityTypeBuilder<EmployeeReference> b) {
 b.ToTable("employee_references"); b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Property(x=>x.Id).ValueGeneratedOnAdd();
 b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
 b.Property(x=>x.Kind).HasColumnName("kind").HasMaxLength(4000);
 b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(4000);
 b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.TenantId).HasColumnName("tenant_id");
 }
}
public sealed class EmployeeSkillConfiguration : IEntityTypeConfiguration<EmployeeSkill>
{
 public void Configure(EntityTypeBuilder<EmployeeSkill> b) {
 b.ToTable("employee_skills"); b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Property(x=>x.Id).ValueGeneratedOnAdd();
 b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
 b.Property(x=>x.EmployeeId).HasColumnName("employee_id");
 b.Property(x=>x.SkillId).HasColumnName("skill_id");
 b.HasOne(x=>x.Skill).WithMany().HasForeignKey(x=>x.SkillId).OnDelete(DeleteBehavior.Restrict);
 b.HasIndex(x=>new{x.TenantId,x.EmployeeId,x.SkillId}).IsUnique().HasDatabaseName("uq_employee_skill_master");
 b.Property(x=>x.SkillName).HasColumnName("skill_name").HasMaxLength(4000);
 b.Property(x=>x.Category).HasColumnName("category").HasMaxLength(4000);
 b.Property(x=>x.Proficiency).HasColumnName("proficiency");
 b.Property(x=>x.YearsOfExperience).HasColumnName("years_of_experience").HasPrecision(18,2);
 b.Property(x=>x.LastUsedDate).HasColumnName("last_used_date");
 b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.TenantId).HasColumnName("tenant_id");
 }
}
public sealed class EmployeeEducationConfiguration : IEntityTypeConfiguration<EmployeeEducation>
{
 public void Configure(EntityTypeBuilder<EmployeeEducation> b) {
 b.ToTable("employee_educations"); b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Property(x=>x.Id).ValueGeneratedOnAdd();
 b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
 b.Property(x=>x.EmployeeId).HasColumnName("employee_id");
 b.Property(x=>x.Institution).HasColumnName("institution").HasMaxLength(4000);
 b.Property(x=>x.FieldOfStudy).HasColumnName("field_of_study").HasMaxLength(200);
 b.Property(x=>x.Qualification).HasColumnName("qualification").HasMaxLength(4000);
 b.Property(x=>x.StartDate).HasColumnName("start_date");
 b.Property(x=>x.EndDate).HasColumnName("end_date");
 b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.TenantId).HasColumnName("tenant_id");
 }
}
public sealed class EmployeeCertificationConfiguration : IEntityTypeConfiguration<EmployeeCertification>
{
 public void Configure(EntityTypeBuilder<EmployeeCertification> b) {
 b.ToTable("employee_certifications"); b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Property(x=>x.Id).ValueGeneratedOnAdd();
 b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
 b.Property(x=>x.EmployeeId).HasColumnName("employee_id");
 b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(4000);
 b.Property(x=>x.Issuer).HasColumnName("issuer").HasMaxLength(4000);
 b.Property(x=>x.IssuedDate).HasColumnName("issued_date");
 b.Property(x=>x.ExpiryDate).HasColumnName("expiry_date");
 b.Property(x=>x.CredentialId).HasColumnName("credential_id").HasMaxLength(200);
 b.Property(x=>x.CredentialUrl).HasColumnName("credential_url").HasMaxLength(4000);
 b.Property(x=>x.FileUrl).HasColumnName("file_url").HasMaxLength(4000);
 b.Property(x=>x.FileName).HasColumnName("file_name").HasMaxLength(255);
 b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.TenantId).HasColumnName("tenant_id");
 }
}
public sealed class EmployeeExperienceConfiguration : IEntityTypeConfiguration<EmployeeExperience>
{
 public void Configure(EntityTypeBuilder<EmployeeExperience> b) {
 b.ToTable("employee_experiences"); b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Property(x=>x.Id).ValueGeneratedOnAdd();
 b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
 b.Property(x=>x.EmployeeId).HasColumnName("employee_id");
 b.Property(x=>x.Company).HasColumnName("company").HasMaxLength(4000);
 b.Property(x=>x.Title).HasColumnName("title").HasMaxLength(4000);
 b.Property(x=>x.Description).HasColumnName("description").HasMaxLength(4000);
 b.Property(x=>x.StartDate).HasColumnName("start_date");
 b.Property(x=>x.EndDate).HasColumnName("end_date");
 b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.TenantId).HasColumnName("tenant_id");
 }
}
public sealed class EmployeeLanguageConfiguration : IEntityTypeConfiguration<EmployeeLanguage>
{
 public void Configure(EntityTypeBuilder<EmployeeLanguage> b) {
 b.ToTable("employee_languages"); b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Property(x=>x.Id).ValueGeneratedOnAdd();
 b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
 b.Property(x=>x.EmployeeId).HasColumnName("employee_id");
 b.Property(x=>x.Name).HasColumnName("name").HasMaxLength(4000);
 b.Property(x=>x.Proficiency).HasColumnName("proficiency").HasMaxLength(4000);
 b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.TenantId).HasColumnName("tenant_id");
 }
}
public sealed class EmployeeCompensationConfiguration : IEntityTypeConfiguration<EmployeeCompensation>
{
 public void Configure(EntityTypeBuilder<EmployeeCompensation> b) {
 b.ToTable("employee_compensations"); b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.Id});
 b.Property(x=>x.Id).ValueGeneratedOnAdd();
 b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
 b.Property(x=>x.EmployeeId).HasColumnName("employee_id");
 b.Property(x=>x.Ctc).HasColumnName("ctc").HasPrecision(18,2);
 b.Property(x=>x.Bonus).HasColumnName("bonus").HasPrecision(18,2);
 b.Property(x=>x.Currency).HasColumnName("currency").HasMaxLength(4000);
 b.Property(x=>x.EffectiveFrom).HasColumnName("effective_from");
 b.Property(x=>x.EffectiveTo).HasColumnName("effective_to");
 b.Property(x=>x.Id).HasColumnName("id"); b.Property(x=>x.TenantId).HasColumnName("tenant_id");
 }
}
