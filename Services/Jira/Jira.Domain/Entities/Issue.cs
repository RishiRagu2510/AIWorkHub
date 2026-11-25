using Jira.Domain.Common;
using Jira.Domain.Enums;

namespace Jira.Domain.Entities;

public sealed class Issue : Entity
{
    public uint Version { get; private set; }

    private Issue()
    {
    }

    public Issue(
        long tenantId,
        long projectId,
        IssueType issueType,
        string issueKey,
        string summary,
        string? description,
        string statusCode,
        string priority,
        long reporterUserId)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId));
        }

        if (projectId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(issueKey))
        {
            throw new ArgumentException(
                "Issue key is required.",
                nameof(issueKey));
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException(
                "Issue summary is required.",
                nameof(summary));
        }

        if (string.IsNullOrWhiteSpace(statusCode))
        {
            throw new ArgumentException(
                "Issue status is required.",
                nameof(statusCode));
        }

        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException(
                "Issue priority is required.",
                nameof(priority));
        }

        if (reporterUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reporterUserId));
        }

        TenantId = tenantId;
        ProjectId = projectId;
        IssueType = issueType;
        IssueKey = issueKey.Trim().ToUpperInvariant();
        Summary = summary.Trim();
        Description = description;
        StatusCode = statusCode.Trim().ToUpperInvariant();
        Priority = priority.Trim().ToUpperInvariant();
        ReporterUserId = reporterUserId;
    }

    public long TenantId { get; private set; }

    public long ProjectId { get; private set; }

    public long IssueNumber { get; private set; }

    public string IssueKey { get; private set; } = string.Empty;

    public IssueType IssueType { get; private set; }

    public string Summary { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string StatusCode { get; private set; } = string.Empty;

    public string Priority { get; private set; } = string.Empty;

    public long ReporterUserId { get; private set; }

    public long? AssigneeUserId { get; private set; }
    public List<IssueAssignee> Assignees { get; private set; } = [];

    public long? ParentIssueId { get; private set; }

    public long? ComponentId { get; private set; }

    public List<IssueLabel> Labels { get; private set; } = [];
    public List<IssueRelease> Releases { get; private set; } = [];
    public int OriginalEstimateMinutes { get; private set; }
    public int RemainingEstimateMinutes { get; private set; }
    public long LoggedTimeMinutes { get; private set; }
    public long BacklogOrder { get; private set; }
    public void SetPlanning(string[]? labels,long[]? fix,long[]? affects,int? original,int? remaining) {
        if (original < 0 || remaining < 0) throw new ArgumentException("Estimates cannot be negative.");
        if (labels is not null) {
            if(labels.Length > 30 || labels.Any(l=>string.IsNullOrWhiteSpace(l)||l.Trim().Length>100))throw new ArgumentException("Use at most 30 labels of 1–100 characters.");
            var normalized=labels.Select(l=>l.Trim().ToLowerInvariant()).Distinct().ToHashSet();
            Labels.RemoveAll(l=>!normalized.Contains(l.Label));
            Labels.AddRange(normalized.Where(l=>!Labels.Any(x=>x.Label==l)).Select(l=>new IssueLabel{TenantId=TenantId,Label=l}));
        }
        foreach(var (kind,ids) in new[]{("FIX",fix),("AFFECTS",affects)}) if(ids is not null) {
            if(ids.Length>30 || ids.Any(id=>id<=0))throw new ArgumentException("Use at most 30 valid versions.");
            Releases.RemoveAll(r=>r.Kind==kind&&!ids.Contains(r.ReleaseId));
            Releases.AddRange(ids.Distinct().Where(id=>!Releases.Any(r=>r.Kind==kind&&r.ReleaseId==id)).Select(id=>new IssueRelease{TenantId=TenantId,ProjectId=ProjectId,ReleaseId=id,Kind=kind}));
        }
        if(original.HasValue)OriginalEstimateMinutes=original.Value;
        if(remaining.HasValue)RemainingEstimateMinutes=remaining.Value;
        Touch();
    }
    public void AdjustLoggedTime(int delta) { LoggedTimeMinutes=checked(LoggedTimeMinutes+delta);if(LoggedTimeMinutes<0)throw new InvalidOperationException("Logged time cannot be negative.");RemainingEstimateMinutes=(int)Math.Min(int.MaxValue,Math.Max(0,(long)RemainingEstimateMinutes-delta));Touch(); }
    public void Rank(long rank) { BacklogOrder=rank;Touch(); }

    public int? StoryPoints { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public long? DeletedByUserId { get; private set; }

    public void UpdateSummary(string summary)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new ArgumentException(
                "Issue summary is required.",
                nameof(summary));
        }

        Summary = summary.Trim();
        Touch();
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
        Touch();
    }

    public void UpdatePriority(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException(
                "Issue priority is required.",
                nameof(priority));
        }

        Priority = priority.Trim().ToUpperInvariant();
        Touch();
    }

    public void AssignTo(long? userId)
    {
        if (userId.HasValue && userId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(userId));
        }

        AssigneeUserId = userId;
        Assignees.Clear();
        if (userId.HasValue)
        {
            Assignees.Add(new IssueAssignee
            {
                TenantId = TenantId,
                ProjectId = ProjectId,
                IssueId = Id,
                UserId = userId.Value,
                AssignedAt = DateTimeOffset.UtcNow
            });
        }
        Touch();
    }

    public void AssignTo(IEnumerable<long>? userIds)
    {
        if (userIds is null)
        {
            AssignTo((long?)null);
            return;
        }

        var distinctIds = userIds.Distinct().ToList();
        if (distinctIds.Any(id => id <= 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(userIds),
                "Assignee user IDs must be greater than zero.");
        }

        if (distinctIds.Count == 0)
        {
            AssignTo((long?)null);
            return;
        }

        AssigneeUserId = distinctIds[0];
        Assignees.RemoveAll(a => !distinctIds.Contains(a.UserId));
        foreach (var id in distinctIds)
        {
            if (!Assignees.Any(a => a.UserId == id))
            {
                Assignees.Add(new IssueAssignee
                {
                    TenantId = TenantId,
                    ProjectId = ProjectId,
                    IssueId = Id,
                    UserId = id,
                    AssignedAt = DateTimeOffset.UtcNow
                });
            }
        }
        Touch();
    }

    public void SetParentIssue(long? parentIssueId)
    {
        if (parentIssueId.HasValue &&
            parentIssueId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(parentIssueId));
        }

        ParentIssueId = parentIssueId;
        Touch();
    }

    public void SetComponent(long? componentId)
    {
        if (componentId.HasValue &&
            componentId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(componentId));
        }

        ComponentId = componentId;
        Touch();
    }

    public void SetStoryPoints(int? storyPoints)
    {
        if (storyPoints.HasValue &&
            storyPoints.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(storyPoints));
        }

        StoryPoints = storyPoints;
        Touch();
    }

    public void SetDueDate(DateOnly? dueDate)
    {
        DueDate = dueDate;
        Touch();
    }

    public void TransitionTo(string statusCode)
    {
        if (string.IsNullOrWhiteSpace(statusCode))
        {
            throw new ArgumentException(
                "Issue status is required.",
                nameof(statusCode));
        }

        StatusCode = statusCode.Trim().ToUpperInvariant();

        if (StatusCode == "DONE")
        {
            ResolvedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            ResolvedAt = null;
        }

        Touch();
    }

    public void SetIssueNumber(long issueNumber)
    {
        if (issueNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(issueNumber));
        }

        IssueNumber = issueNumber;
    }

    public void SetIssueKey(string issueKey)
    {
        if (string.IsNullOrWhiteSpace(issueKey))
        {
            throw new ArgumentException(
                "Issue key is required.",
                nameof(issueKey));
        }

        IssueKey = issueKey.Trim().ToUpperInvariant();
        Touch();
    }

    public void Delete(long deletedByUserId)
    {
        if (deletedByUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(deletedByUserId));
        }

        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = DateTimeOffset.UtcNow;
        DeletedByUserId = deletedByUserId;

        Touch();
    }
}