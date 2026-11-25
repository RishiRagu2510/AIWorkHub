using Audit.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Audit.Infrastructure.Persistence;

public sealed class AuditDbContext : DbContext
{
    public AuditDbContext(
        DbContextOptions<AuditDbContext> options)
        : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs =>
        Set<AuditLog>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AuditDbContext).Assembly);
    }
}