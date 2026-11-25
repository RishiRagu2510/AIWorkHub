using System;
using Jira.Application.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jira.Application.Abstractions.Persistence;
using Jira.Domain.Entities;
using Jira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jira.Infrastructure.Persistence.Repositories;

public sealed class IssueRepository : IIssueRepository
{
    private readonly JiraDbContext _dbContext;
    private readonly JiraResourceScope _scope;
    private readonly Jira.Application.Services.WritePrecondition _precondition;

    public IssueRepository(JiraDbContext dbContext, JiraResourceScope scope, Jira.Application.Services.WritePrecondition precondition)
    {
        _dbContext = dbContext;
        _scope = scope;
        _precondition = precondition;
    }

    public async Task ValidateVersionsAsync(long tenant,long project,long[] ids,CancellationToken ct) {
        if(ids.Length>60 || ids.Any(x=>x<=0))throw new ArgumentException("Invalid versions.");
        if(await _dbContext.Set<ProjectRelease>().CountAsync(r=>r.TenantId==tenant&&r.ProjectId==project&&ids.Contains(r.Id),ct)!=ids.Distinct().Count())throw new ResourceNotFoundException();
    }
    public async Task ValidateComponentAsync(long tenantId, long projectId, long? componentId, CancellationToken ct)
    {
        if (componentId is null) return;
        if (componentId <= 0) throw new ArgumentException("A valid component is required.");
        if (!await _dbContext.Database.SqlQuery<long>($"SELECT id AS \"Value\" FROM components WHERE id = {componentId} AND tenant_id = {tenantId} AND project_id = {projectId}").AnyAsync(ct))
            throw new Jira.Application.Services.ResourceNotFoundException();
    }
    public async Task ValidateParentAsync(long tenantId, long projectId, long issueId, long? parentId, CancellationToken ct, Jira.Domain.Enums.IssueType? childType = null)
    {
        if (_dbContext.Database.CurrentTransaction is null) throw new InvalidOperationException("Parent validation requires a transaction.");
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-tenantId})", ct);
        if (childType is null && issueId > 0)
            childType = await _dbContext.Issues.Where(i => i.Id == issueId && i.TenantId == tenantId).Select(i => i.IssueType).SingleAsync(ct);
        if (parentId is null) {
            if (childType == Jira.Domain.Enums.IssueType.SubTask) throw new ArgumentException("A subtask requires a Task or Bug parent.");
            return;
        }
        if (parentId <= 0 || parentId == issueId) throw new ArgumentException("Invalid parent issue.");
        var parent = await _scope.Issues(tenantId).AsNoTracking().SingleOrDefaultAsync(i => i.Id == parentId, ct)
            ?? throw new Jira.Application.Services.ResourceNotFoundException();
        if (parent.ProjectId != projectId) throw new ArgumentException("Parent must belong to the same project.");
        var valid = childType switch {
            Jira.Domain.Enums.IssueType.Epic => false,
            Jira.Domain.Enums.IssueType.Story => parent.IssueType == Jira.Domain.Enums.IssueType.Epic,
            Jira.Domain.Enums.IssueType.Task or Jira.Domain.Enums.IssueType.Bug => parent.IssueType == Jira.Domain.Enums.IssueType.Story,
            Jira.Domain.Enums.IssueType.SubTask => parent.IssueType is Jira.Domain.Enums.IssueType.Task or Jira.Domain.Enums.IssueType.Bug,
            _ => false
        };
        if (!valid) throw new ArgumentException("Allowed hierarchy: Epic → Story → Task/Bug → Subtask.");
        var seen = new HashSet<long>();
        long? current = parentId;
        while (current.HasValue)
        {
            if (current == issueId || !seen.Add(current.Value)) throw new ArgumentException("Issue hierarchy cannot contain a cycle.");
            var ancestor = await _dbContext.Issues.AsNoTracking().Where(i => i.TenantId == tenantId && i.ProjectId == projectId && !i.IsDeleted && i.Id == current)
                .Select(i => new { i.ParentIssueId }).SingleOrDefaultAsync(ct);
            if (ancestor is null) throw new Jira.Application.Services.ResourceNotFoundException();
            current = ancestor.ParentIssueId;
        }
    }

    public async Task<Issue?> GetByIdAsync(
        long tenantId,
        long issueId,
        CancellationToken cancellationToken = default)
    {
        
        if (_dbContext.Database.CurrentTransaction is not null)
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-tenantId})", cancellationToken);
        var issue = await _scope.Issues(tenantId)
            .FirstOrDefaultAsync(
                issue =>
                    issue.TenantId == tenantId &&
                    issue.Id == issueId &&
                    !issue.IsDeleted,
                cancellationToken);
        if (issue is not null) _precondition.Check(issue.Version);
        return issue;
    }

    public async Task<IReadOnlyList<Issue>> GetByProjectAsync(
        long tenantId,
        long projectId,
        CancellationToken cancellationToken = default)
    {
        return await _scope.Issues(tenantId)
            .Where(
                issue =>
                    issue.TenantId == tenantId &&
                    issue.ProjectId == projectId &&
                    !issue.IsDeleted)
            .OrderBy(issue => issue.IssueNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<Issue?> GetByIssueKeyAsync(
        long tenantId,
        string issueKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedIssueKey = issueKey
            .Trim()
            .ToUpperInvariant();

        return await _scope.Issues(tenantId)
            .FirstOrDefaultAsync(
                issue =>
                    issue.TenantId == tenantId &&
                    issue.IssueKey == normalizedIssueKey &&
                    !issue.IsDeleted,
                cancellationToken);
    }

    public async Task<IssueSearchResult> SearchAsync(
        long tenantId,
        IssueSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Issue> query = _scope.Issues(tenantId)
            .AsNoTracking()
            .Where(
                issue =>
                    issue.TenantId == tenantId &&
                    !issue.IsDeleted);

        if (criteria.RootOnly) {
            var visible = _scope.Issues(tenantId);
            query = query.Where(i => i.ParentIssueId == null || !visible.Any(p => p.Id == i.ParentIssueId));
        }
        if (criteria.ParentIssueId.HasValue) query = query.Where(i => i.ParentIssueId == criteria.ParentIssueId);
        if (criteria.DueFrom.HasValue) query = query.Where(i => i.DueDate >= criteria.DueFrom);
        if (criteria.DueTo.HasValue) query = query.Where(i => i.DueDate <= criteria.DueTo);
        if (!string.IsNullOrWhiteSpace(criteria.WorkGroup))
        {
            var today = criteria.Today ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var end = today.AddDays(7);
            query = query.Where(i => i.StatusCode != "DONE" && i.StatusCode != "CANCELLED");
            query = criteria.WorkGroup.ToUpperInvariant() switch
            {
                "OVERDUE" => query.Where(i => i.DueDate < today),
                "DUE_SOON" => query.Where(i => i.DueDate >= today && i.DueDate <= end),
                "IN_PROGRESS" => query.Where(i => (i.DueDate == null || i.DueDate > end) && (i.StatusCode == "IN_PROGRESS" || i.StatusCode == "CODE_REVIEW" || i.StatusCode == "TESTING")),
                "OTHER" => query.Where(i => (i.DueDate == null || i.DueDate > end) && i.StatusCode != "IN_PROGRESS" && i.StatusCode != "CODE_REVIEW" && i.StatusCode != "TESTING"),
                _ => throw new ArgumentException("Invalid work group.")
            };
        }
        if (criteria.ProjectId.HasValue)
        {
            var projectId = criteria.ProjectId.Value;

            query = query.Where(
                issue => issue.ProjectId == projectId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.IssueKey))
        {
            var issueKey = criteria.IssueKey
                .Trim()
                .ToUpperInvariant();

            query = query.Where(
                issue => issue.IssueKey == issueKey);
        }

        if (criteria.IssueType.HasValue)
        {
            var issueType = criteria.IssueType.Value;

            query = query.Where(
                issue => issue.IssueType == issueType);
        }

        if (!string.IsNullOrWhiteSpace(criteria.StatusCode))
        {
            var statusCode = criteria.StatusCode
                .Trim()
                .ToUpperInvariant();

            query = query.Where(
                issue => issue.StatusCode == statusCode);
        }

        if(criteria.ComponentId.HasValue)query=query.Where(i=>i.ComponentId==criteria.ComponentId);
        if(criteria.Label is not null){var label=criteria.Label.Trim().ToLowerInvariant();query=query.Where(i=>i.Labels.Any(l=>l.Label==label));}
        if(criteria.FixVersionId.HasValue)query=query.Where(i=>i.Releases.Any(r=>r.Kind=="FIX"&&r.ReleaseId==criteria.FixVersionId));
        if(criteria.AffectsVersionId.HasValue)query=query.Where(i=>i.Releases.Any(r=>r.Kind=="AFFECTS"&&r.ReleaseId==criteria.AffectsVersionId));
        if (!string.IsNullOrWhiteSpace(criteria.Priority))
        {
            var priority = criteria.Priority
                .Trim()
                .ToUpperInvariant();

            query = query.Where(
                issue => issue.Priority == priority);
        }

        if (criteria.AssigneeUserId.HasValue)
        {
            var assigneeUserId = criteria.AssigneeUserId.Value;

            query = query.Where(
                issue => issue.AssigneeUserId == assigneeUserId || issue.Assignees.Any(a => a.UserId == assigneeUserId));
        }

        if (criteria.ReporterUserId.HasValue)
        {
            var reporterUserId = criteria.ReporterUserId.Value;

            query = query.Where(
                issue => issue.ReporterUserId == reporterUserId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Text))
        {
            var pattern = $"%{criteria.Text.Trim()}%";

            query = query.Where(
                issue =>
                    EF.Functions.ILike(
                        issue.IssueKey,
                        pattern)
                    ||
                    EF.Functions.ILike(
                        issue.Summary,
                        pattern)
                    ||
                    (
                        issue.Description != null &&
                        EF.Functions.ILike(
                            issue.Description,
                            pattern)
                    ));
        }

        var totalCount = await query.CountAsync(
            cancellationToken);

        var page = criteria.Page < 1
            ? 1
            : criteria.Page;

        var pageSize = criteria.PageSize < 1
            ? 20
            : criteria.PageSize > 100
                ? 100
                : criteria.PageSize;

        var skip = checked((page - 1) * pageSize);

        var sortBy = string.IsNullOrWhiteSpace(criteria.SortBy)
            ? "issueNumber"
            : criteria.SortBy.Trim();

        var sortDescending =
            string.Equals(
                criteria.SortDirection,
                "desc",
                StringComparison.OrdinalIgnoreCase);

        IQueryable<Issue> orderedQuery;

        switch (sortBy.ToLowerInvariant())
        {
            case "issuenumber":
                orderedQuery = sortDescending
                    ? query
                        .OrderByDescending(
                            issue => issue.IssueNumber)
                        .ThenByDescending(
                            issue => issue.Id)
                    : query
                        .OrderBy(
                            issue => issue.IssueNumber)
                        .ThenBy(
                            issue => issue.Id);
                break;

            case "issuekey":
                orderedQuery = sortDescending
                    ? query
                        .OrderByDescending(
                            issue => issue.IssueKey)
                        .ThenByDescending(
                            issue => issue.Id)
                    : query
                        .OrderBy(
                            issue => issue.IssueKey)
                        .ThenBy(
                            issue => issue.Id);
                break;

            case "summary":
                orderedQuery = sortDescending
                    ? query
                        .OrderByDescending(
                            issue => issue.Summary)
                        .ThenByDescending(
                            issue => issue.Id)
                    : query
                        .OrderBy(
                            issue => issue.Summary)
                        .ThenBy(
                            issue => issue.Id);
                break;

            case "priority":
                orderedQuery = sortDescending
                    ? query
                        .OrderByDescending(
                            issue => issue.Priority)
                        .ThenByDescending(
                            issue => issue.Id)
                    : query
                        .OrderBy(
                            issue => issue.Priority)
                        .ThenBy(
                            issue => issue.Id);
                break;

            case "status":
            case "statuscode":
                orderedQuery = sortDescending
                    ? query
                        .OrderByDescending(
                            issue => issue.StatusCode)
                        .ThenByDescending(
                            issue => issue.Id)
                    : query
                        .OrderBy(
                            issue => issue.StatusCode)
                        .ThenBy(
                            issue => issue.Id);
                break;

            case "createdat":
                orderedQuery = sortDescending
                    ? query
                        .OrderByDescending(
                            issue => issue.CreatedAt)
                        .ThenByDescending(
                            issue => issue.Id)
                    : query
                        .OrderBy(
                            issue => issue.CreatedAt)
                        .ThenBy(
                            issue => issue.Id);
                break;

            case "updatedat":
                orderedQuery = sortDescending
                    ? query
                        .OrderByDescending(
                            issue => issue.UpdatedAt)
                        .ThenByDescending(
                            issue => issue.Id)
                    : query
                        .OrderBy(
                            issue => issue.UpdatedAt)
                        .ThenBy(
                            issue => issue.Id);
                break;

            default:
                throw new ArgumentException(
                    $"Unsupported sort field: {sortBy}.",
                    nameof(criteria.SortBy));
        }

        var items = await orderedQuery
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new IssueSearchResult
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task AddAsync(
        Issue issue,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Issues.AddAsync(
            issue,
            cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(
            cancellationToken);
    }
}