using Jira.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Jira.Infrastructure.Persistence.Onboarding;
public sealed class EmployeeOnboarding
{
 public long IssueId {get;set;}
 public long TenantId {get;set;}
 public string Data {get;set;}="{}";
 public string Status {get;set;}="SUBMITTED";
 public long? ReviewedBy {get;set;}
 public DateTimeOffset? ReviewedAt {get;set;}
 public string? ReviewNote {get;set;}
 public long? EmployeeId {get;set;}
 public int Revision {get;set;}=1;
}
public sealed class EmployeeOnboardingConfiguration:IEntityTypeConfiguration<EmployeeOnboarding>
{
 public void Configure(EntityTypeBuilder<EmployeeOnboarding> b) {
  b.ToTable("employee_onboarding_requests");b.HasKey(x=>x.IssueId);
  b.Property(x=>x.IssueId).HasColumnName("issue_id").ValueGeneratedNever();b.Property(x=>x.TenantId).HasColumnName("tenant_id");
  b.Property(x=>x.Data).HasColumnName("data").HasColumnType("jsonb");b.Property(x=>x.Status).HasColumnName("status");
  b.Property(x=>x.ReviewedBy).HasColumnName("reviewed_by");b.Property(x=>x.ReviewedAt).HasColumnName("reviewed_at");b.Property(x=>x.ReviewNote).HasColumnName("review_note");
  b.Property(x=>x.EmployeeId).HasColumnName("employee_id");b.Property(x=>x.Revision).HasColumnName("revision").IsConcurrencyToken();
  b.HasOne<Issue>().WithOne().HasForeignKey<EmployeeOnboarding>(x=>new{x.TenantId,x.IssueId}).HasPrincipalKey<Issue>(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Cascade);
  b.HasIndex(x=>new{x.TenantId,x.Status});
 }
}
