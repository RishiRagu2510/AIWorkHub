using Audit.Contracts;
using Audit.Domain.Entities;
using Audit.Infrastructure.Persistence;
using Audit.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Audit.Infrastructure.Tests;

public sealed class AuditLogRepositoryPostgreSqlTests
{
    [Fact]
    public async Task SearchAsync_WorksAgainstRealPostgreSql()
    {
        await using var postgres =
            new PostgreSqlBuilder("postgres:17-alpine")
                .WithDatabase("audit_test")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

        await postgres.StartAsync();

        var options =
            new DbContextOptionsBuilder<AuditDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .Options;

        await using var dbContext = new AuditDbContext(options);

        await dbContext.Database.EnsureCreatedAsync();

        var occurredAt1 =
            new DateTimeOffset(
                2026,
                1,
                1,
                10,
                0,
                0,
                TimeSpan.Zero);

        var occurredAt2 =
            new DateTimeOffset(
                2026,
                1,
                2,
                10,
                0,
                0,
                TimeSpan.Zero);

        var occurredAt3 =
            new DateTimeOffset(
                2026,
                1,
                3,
                10,
                0,
                0,
                TimeSpan.Zero);

        dbContext.AuditLogs.AddRange(
            CreateLog(
                tenantId: 1,
                actorUserId: 100,
                action: "ISSUE_CREATED",
                entityType: "ISSUE",
                entityId: 10,
                occurredAt: occurredAt1),

            CreateLog(
                tenantId: 1,
                actorUserId: 101,
                action: "ISSUE_UPDATED",
                entityType: "ISSUE",
                entityId: 10,
                occurredAt: occurredAt2),

            CreateLog(
                tenantId: 2,
                actorUserId: 200,
                action: "ISSUE_CREATED",
                entityType: "ISSUE",
                entityId: 20,
                occurredAt: occurredAt3));

        await dbContext.SaveChangesAsync();

        var repository =
            new AuditLogRepository(dbContext);

        var result =
            await repository.SearchAsync(
                tenantId: 1,
                new AuditLogQuery
                {
                    Page = 1,
                    PageSize = 20,
                    EntityType = "ISSUE"
                });

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);

        Assert.Equal(
            "ISSUE_UPDATED",
            result.Items[0].Action);

        Assert.Equal(
            "ISSUE_CREATED",
            result.Items[1].Action);

        Assert.All(
            result.Items,
            item => Assert.Equal(1, item.TenantId));

        var filtered =
            await repository.SearchAsync(
                tenantId: 1,
                new AuditLogQuery
                {
                    Page = 1,
                    PageSize = 20,
                    EntityType = "ISSUE",
                    EntityId = 10,
                    Action = "ISSUE_CREATED"
                });

        var filteredItem =
            Assert.Single(filtered.Items);

        Assert.Equal(1, filtered.TotalCount);
        Assert.Equal("ISSUE_CREATED", filteredItem.Action);
        Assert.Equal(10, filteredItem.EntityId);
        Assert.Equal(100, filteredItem.ActorUserId);
    }

    private static AuditLog CreateLog(
        long tenantId,
        long actorUserId,
        string action,
        string entityType,
        long entityId,
        DateTimeOffset occurredAt)
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
            """{"test":"old"}""",
            """{"source":"JIRA","test":"new"}""",
            occurredAt);
    }
}
