using Jira.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Jira.Infrastructure.Persistence.Configurations;
public sealed class WorkLogConfiguration : IEntityTypeConfiguration<WorkLog>, IEntityTypeConfiguration<TimesheetPeriod>
{
    private static void Columns<T>(EntityTypeBuilder<T> b) where T:class {
        foreach(var p in b.Metadata.GetProperties()) p.SetColumnName(System.Text.RegularExpressions.Regex.Replace(p.Name,"(?<!^)([A-Z])","_$1").ToLowerInvariant());
    }
    public void Configure(EntityTypeBuilder<WorkLog> b) {
        b.ToTable("work_logs",t=>t.HasCheckConstraint("ck_work_log_duration","duration_minutes BETWEEN 1 AND 1440")); b.HasKey(x=>x.Id);
        b.Property(x=>x.Description).HasMaxLength(4000); Columns(b); b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
        b.HasOne<Issue>().WithMany().HasForeignKey(x=>x.IssueId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Project>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ProjectId}).HasPrincipalKey(x=>new{x.TenantId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TimesheetPeriod>().WithMany().HasForeignKey(x=>new{x.TenantId,x.UserId,x.PeriodId}).HasPrincipalKey(x=>new{x.TenantId,x.UserId,x.Id}).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x=>new{x.TenantId,x.UserId,x.WorkDate}); b.HasIndex(x=>new{x.TenantId,x.ProjectId,x.WorkDate}); b.HasIndex(x=>new{x.TenantId,x.IssueId});
    }
    public void Configure(EntityTypeBuilder<TimesheetPeriod> b) {
        b.ToTable("timesheet_periods");b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.TenantId,x.UserId,x.Id});
        b.Property(x=>x.Status).HasMaxLength(20);b.Property(x=>x.RejectionReason).HasMaxLength(2000); Columns(b); b.Property(x=>x.Version).HasColumnName("xmin").IsRowVersion();
        b.HasIndex(x=>new{x.TenantId,x.UserId,x.PeriodStart}).IsUnique();b.HasIndex(x=>new{x.TenantId,x.Status,x.PeriodStart});
    }
}
