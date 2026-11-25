using Audit.Contracts;
using Audit.Domain.Entities;
using Audit.Infrastructure.Persistence;
using Audit.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Audit.Infrastructure.Tests;

public sealed class AuditLogRepositoryQueryTests
{
    [Fact]
    public async Task SearchAsync_IsScopedToTenant()
    {
        await using var dbContext = CreateDbContext();

        dbContext.AuditLogs.AddRange(
            CreateLog(1, 100, "ISSUE_CREATED", "ISSUE", 10),
            CreateLog(2, 200, "ISSUE_CREATED", "ISSUE", 20),
            CreateLog(1, 101, "ISSUE_UPDATED", "ISSUE", 10));

        await dbContext.SaveChangesAsync();

        var repository = new AuditLogRepository(dbContext);

        var result = await repository.SearchAsync(
            1,
            new AuditLogQuery
            {
                Page = 1,
                PageSize = 20
            });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.Equal(1, item.TenantId));
    }

    [Fact]
    public async Task SearchAsync_AppliesEntityAndActionFilters()
    {
        await using var dbContext = CreateDbContext();

        dbContext.AuditLogs.AddRange(
            CreateLog(1, 100, "ISSUE_CREATED", "ISSUE", 10),
            CreateLog(1, 101, "ISSUE_UPDATED", "ISSUE", 10),
            CreateLog(1, 102, "ISSUE_CREATED", "ISSUE", 11),
            CreateLog(1, 103, "PROJECT_CREATED", "PROJECT", 5));

        await dbContext.SaveChangesAsync();

        var repository = new AuditLogRepository(dbContext);

        var result = await repository.SearchAsync(
            1,
            new AuditLogQuery
            {
                EntityType = "ISSUE",
                EntityId = 10,
                Action = "ISSUE_CREATED",
                Page = 1,
                PageSize = 20
            });

        var item = Assert.Single(result.Items);

        Assert.Equal("ISSUE_CREATED", item.Action);
        Assert.Equal("ISSUE", item.EntityType);
        Assert.Equal(10, item.EntityId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_ReturnsNewestRecordsFirst()
    {
        await using var dbContext = CreateDbContext();

        var oldest = CreateLog(
            1,
            100,
            "ISSUE_CREATED",
            "ISSUE",
            10,
            new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));

        var middle = CreateLog(
            1,
            102,
            "ISSUE_COMMENT_ADDED",
            "ISSUE",
            10,
            new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero));

        var newest = CreateLog(
            1,
            101,
            "ISSUE_UPDATED",
            "ISSUE",
            10,
            new DateTimeOffset(2026, 1, 3, 10, 0, 0, TimeSpan.Zero));

        dbContext.AuditLogs.AddRange(oldest, middle, newest);

        await dbContext.SaveChangesAsync();

        var repository = new AuditLogRepository(dbContext);

        var result = await repository.SearchAsync(
            1,
            new AuditLogQuery
            {
                Page = 1,
                PageSize = 20
            });

        Assert.Equal(3, result.TotalCount);
        Assert.Equal("ISSUE_UPDATED", result.Items[0].Action);
        Assert.Equal("ISSUE_COMMENT_ADDED", result.Items[1].Action);
        Assert.Equal("ISSUE_CREATED", result.Items[2].Action);
    }

    [Fact]
    public async Task SearchAsync_AppliesPagination()
    {
        await using var dbContext = CreateDbContext();

        for (var i = 1; i <= 5; i++)
        {
            dbContext.AuditLogs.Add(
                CreateLog(
                    1,
                    i,
                    $"ACTION_{i}",
                    "ISSUE",
                    i,
                    new DateTimeOffset(
                        2026,
                        1,
                        i,
                        10,
                        0,
                        0,
                        TimeSpan.Zero)));
        }

        await dbContext.SaveChangesAsync();

        var repository = new AuditLogRepository(dbContext);

        var result = await repository.SearchAsync(
            1,
            new AuditLogQuery
            {
                Page = 2,
                PageSize = 2
            });

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal("ACTION_3", result.Items[0].Action);
        Assert.Equal("ACTION_2", result.Items[1].Action);
    }

    [Fact]
    public async Task SearchAsync_ClampsInvalidPaginationValues()
    {
        await using var dbContext = CreateDbContext();

        dbContext.AuditLogs.Add(
            CreateLog(1, 100, "ISSUE_CREATED", "ISSUE", 10));

        await dbContext.SaveChangesAsync();

        var repository = new AuditLogRepository(dbContext);

        var result = await repository.SearchAsync(
            1,
            new AuditLogQuery
            {
                Page = 0,
                PageSize = 0
            });

        Assert.Equal(1, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Single(result.Items);
    }

    private static AuditDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AuditDbContext(options);
    }

    private static AuditLog CreateLog(
        long tenantId,
        long actorUserId,
        string action,
        string entityType,
        long entityId,
        DateTimeOffset? occurredAt = null)
    {
        return new AuditLog(
            Guid.NewGuid(),
            tenantId,
            actorUserId,
            action,
            entityType,
            entityId,
            "JIRA",
            null,
            null,
            """{"source":"JIRA"}""",
            occurredAt ?? DateTimeOffset.UtcNow);
    }
}
