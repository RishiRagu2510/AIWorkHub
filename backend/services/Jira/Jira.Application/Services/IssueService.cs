using System.Text.Json;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Abstractions.Services;
using Jira.Contracts.Issues;
using Jira.Domain.Entities;
using Jira.Domain.Enums;
using Messaging.Events;

namespace Jira.Application.Services;

public sealed class IssueService : IIssueService
{
    private static readonly HashSet<string> ValidPriorities =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "LOWEST",
            "LOW",
            "MEDIUM",
            "HIGH",
            "HIGHEST"
        };

    private readonly IIssueRepository _issueRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IJiraUnitOfWork _unitOfWork;
    private readonly INotificationWriter _notifications;
    private readonly IWorkflowValidator _workflow;
    private readonly IAssigneeValidator _assigneeValidator;

    public IssueService(
        IIssueRepository issueRepository,
        IProjectRepository projectRepository,
        IOutboxWriter outboxWriter,
        IJiraUnitOfWork unitOfWork,
        IAssigneeValidator assigneeValidator, INotificationWriter notifications, IWorkflowValidator workflow)
    {
        _issueRepository = issueRepository;
        _projectRepository = projectRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _workflow = workflow;
        _assigneeValidator = assigneeValidator;
    }

    public async Task<IssueDto> CreateAsync(
        long tenantId,
        long reporterUserId,
        CreateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (reporterUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reporterUserId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        if (request.ProjectId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.ProjectId));
        }

        var project =
            await _projectRepository.GetByIdAsync(
                tenantId,
                request.ProjectId,
                cancellationToken);

        if (project is null)
        {
            throw new ResourceNotFoundException();
        }

        if (!Enum.TryParse<IssueType>(
                request.IssueType,
                true,
                out var issueType) ||
            !Enum.IsDefined(issueType))
        {
            throw new ArgumentException(
                $"Invalid issue type: {request.IssueType}. " +
                "Allowed values are EPIC, STORY, TASK, BUG, SUBTASK.",
                nameof(request.IssueType));
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            throw new ArgumentException(
                "Issue summary is required.",
                nameof(request.Summary));
        }

        if (string.IsNullOrWhiteSpace(request.StatusCode))
        {
            throw new ArgumentException(
                "Issue status is required.",
                nameof(request.StatusCode));
        }

        ValidatePriority(request.Priority);
        ValidateStoryPoints(request.StoryPoints);

        Issue? issue = null;

        await _unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                await _issueRepository.ValidateParentAsync(tenantId, request.ProjectId, 0, request.ParentIssueId, transactionCancellationToken, issueType);
                await _workflow.ValidateAsync(tenantId, request.ProjectId, null, request.StatusCode, transactionCancellationToken);
                var temporaryIssueKey =
                    $"TEMP-{Guid.NewGuid():N}";

                issue = new Issue(
                    tenantId: tenantId,
                    projectId: request.ProjectId,
                    issueType: issueType,
                    issueKey: temporaryIssueKey,
                    summary: request.Summary,
                    description: request.Description,
                    statusCode: request.StatusCode,
                    priority: NormalizePriority(
                        request.Priority),
                    reporterUserId: reporterUserId);

                var createAssignees = request.AssigneeUserIds ?? (request.AssigneeUserId.HasValue ? [request.AssigneeUserId.Value] : null);
                if (createAssignees is not null)
                {
                    foreach (var assigneeId in createAssignees)
                    {
                        await _assigneeValidator.ValidateAsync(tenantId, issue.ProjectId, assigneeId, transactionCancellationToken);
                    }
                    issue.AssignTo(createAssignees);
                }

                issue.SetParentIssue(
                    request.ParentIssueId);

                await _issueRepository.ValidateComponentAsync(tenantId, issue.ProjectId, request.ComponentId, transactionCancellationToken);
                issue.SetComponent(
                    request.ComponentId);

                issue.SetStoryPoints(
                    request.StoryPoints);

                issue.SetDueDate(
                    request.DueDate);

                await _issueRepository.ValidateVersionsAsync(tenantId,issue.ProjectId,[..request.FixVersionIds??[],..request.AffectsVersionIds??[]],transactionCancellationToken);
                issue.SetPlanning(request.Labels,request.FixVersionIds,request.AffectsVersionIds,request.OriginalEstimateMinutes,request.RemainingEstimateMinutes);
                await _issueRepository.AddAsync(
                    issue,
                    transactionCancellationToken);

                
                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);

                if (issue.IssueNumber <= 0)
                {
                    throw new InvalidOperationException(
                        "Issue number was not generated by the database.");
                }

                var finalIssueKey =
                    $"{project.Key}-{issue.IssueNumber}";

                
                
                if (finalIssueKey.Length > 50) finalIssueKey = $"P{project.Id}-{issue.IssueNumber}";
                issue.SetIssueKey(finalIssueKey);

                
                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);

                var eventId = Guid.NewGuid();
                await _notifications.AssignmentAsync(issue, reporterUserId, eventId, transactionCancellationToken);
                var occurredAt = DateTimeOffset.UtcNow;

                var auditEvent = new IssueAuditEvent
                {
                    EventId = eventId,
                    EventType = "ISSUE_CREATED",
                    TenantId = issue.TenantId,
                    EntityType = "ISSUE",
                    EntityId = issue.Id,
                    EntityKey = issue.IssueKey,
                    PerformedByUserId = reporterUserId,
                    OldValues = null,
                    NewValues = JsonSerializer.Serialize(new
                    {
                        issueType = issue.IssueType
                            .ToString()
                            .ToUpperInvariant(),
                        summary = issue.Summary,
                        description = issue.Description,
                        priority = issue.Priority,
                        statusCode = issue.StatusCode
                    }),
                    Metadata = JsonSerializer.Serialize(new
                    {
                        source = "JIRA"
                    }),
                    OccurredAt = occurredAt
                };

                await _outboxWriter.AddAsync(
                    eventId,
                    "ISSUE_CREATED",
                    "aiworkhub.issue.audit",
                    JsonSerializer.Serialize(auditEvent),
                    occurredAt,
                    transactionCancellationToken);

                
                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);
            },
            cancellationToken);

        if (issue is null)
        {
            throw new InvalidOperationException(
                "Issue creation did not produce an issue.");
        }

        return Map(issue);
    }

    public async Task<IssueDto?> GetByIdAsync(
        long tenantId,
        long issueId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (issueId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(issueId));
        }

        var issue =
            await _issueRepository.GetByIdAsync(
                tenantId,
                issueId,
                cancellationToken);

        return issue is null
            ? null
            : Map(issue);
    }

    public async Task<IssueDto?> GetByKeyAsync(
        long tenantId,
        string issueKey,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(issueKey))
        {
            throw new ArgumentException(
                "Issue key is required.",
                nameof(issueKey));
        }

        var issue =
            await _issueRepository.GetByIssueKeyAsync(
                tenantId,
                issueKey,
                cancellationToken);

        return issue is null
            ? null
            : Map(issue);
    }

    public async Task<IReadOnlyList<IssueDto>> GetByProjectAsync(
        long tenantId,
        long projectId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (projectId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectId));
        }

        var issues =
            await _issueRepository.GetByProjectAsync(
                tenantId,
                projectId,
                cancellationToken);

        return issues
            .Select(Map)
            .ToList();
    }

    public async Task<IssueSearchResponse> SearchAsync(
        long tenantId,
        IssueSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        if (request.Page <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Page),
                "Page must be greater than zero.");
        }

        if (request.PageSize <= 0 ||
            request.PageSize > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.PageSize),
                "PageSize must be between 1 and 100.");
        }

        IssueType? issueType = null;

        if (!string.IsNullOrWhiteSpace(
                request.IssueType))
        {
            if (!Enum.TryParse<IssueType>(
                    request.IssueType,
                    true,
                    out var parsedIssueType) ||
                !Enum.IsDefined(parsedIssueType))
            {
                throw new ArgumentException(
                    $"Invalid issue type: {request.IssueType}. " +
                    "Allowed values are EPIC, STORY, TASK, BUG, SUBTASK.",
                    nameof(request.IssueType));
            }

            issueType = parsedIssueType;
        }

        if (!string.IsNullOrWhiteSpace(
                request.Priority))
        {
            ValidatePriority(request.Priority);
        }

        var sortBy =
            string.IsNullOrWhiteSpace(request.SortBy)
                ? "issueNumber"
                : request.SortBy.Trim();

        var sortDirection =
            string.IsNullOrWhiteSpace(request.SortDirection)
                ? "asc"
                : request.SortDirection.Trim();

        if (!string.Equals(
                sortDirection,
                "asc",
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(
                sortDirection,
                "desc",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "SortDirection must be either 'asc' or 'desc'.",
                nameof(request.SortDirection));
        }

        if (request.ParentIssueId <= 0 || request.DueFrom > request.DueTo)
            throw new ArgumentException("Invalid parent or due-date range.");
        if (request.WorkGroup is not null && !new[] { "OVERDUE", "DUE_SOON", "IN_PROGRESS", "OTHER" }.Contains(request.WorkGroup.ToUpperInvariant()))
            throw new ArgumentException("Invalid work group.");
        if (request.Today > DateOnly.MaxValue.AddDays(-7)) throw new ArgumentException("Invalid work date.");
        var criteria =
            new IssueSearchCriteria
            {
                Label = request.Label, ComponentId = request.ComponentId, FixVersionId = request.FixVersionId, AffectsVersionId = request.AffectsVersionId,
                ProjectId = request.ProjectId,
                ParentIssueId = request.ParentIssueId,
                RootOnly = request.RootOnly,
                DueFrom = request.DueFrom,
                DueTo = request.DueTo,
                WorkGroup = request.WorkGroup,
                Today = request.Today,
                IssueKey = request.IssueKey,
                IssueType = issueType,
                StatusCode = request.Status,
                Priority = request.Priority,
                AssigneeUserId = request.AssigneeUserId,
                ReporterUserId = request.ReporterUserId,
                Text = request.Text,
                Page = request.Page,
                PageSize = request.PageSize,
                SortBy = sortBy,
                SortDirection = sortDirection
            };

        var result =
            await _issueRepository.SearchAsync(
                tenantId,
                criteria,
                cancellationToken);

        var totalPages =
            result.TotalCount == 0
                ? 0
                : (int)Math.Ceiling(
                    result.TotalCount /
                    (double)request.PageSize);

        return new IssueSearchResponse
        {
            Items = result.Items
                .Select(Map)
                .ToList(),

            Page = request.Page,

            PageSize = request.PageSize,

            TotalCount = result.TotalCount,

            TotalPages = totalPages
        };
    }

    public async Task<IssueDto?> UpdateAsync(
        long tenantId,
        long actorUserId,
        long issueId,
        UpdateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (actorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actorUserId));
        }

        if (issueId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(issueId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        Issue? issue = null;

        await _unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                issue =
                    await _issueRepository.GetByIdAsync(
                        tenantId,
                        issueId,
                        transactionCancellationToken);

                if (issue is null)
                {
                    return;
                }

                var previousAssignee = issue.AssigneeUserId;
                
                var previouslyAssigned = issue.Assignees.Select(a => a.UserId).ToList();
                if (previousAssignee.HasValue && !previouslyAssigned.Contains(previousAssignee.Value)) previouslyAssigned.Add(previousAssignee.Value);
                var oldValues = JsonSerializer.Serialize(new
                {
                    issueType = issue.IssueType
                        .ToString()
                        .ToUpperInvariant(),
                    summary = issue.Summary,
                    description = issue.Description,
                    priority = issue.Priority,
                    statusCode = issue.StatusCode,
                    assigneeUserId = issue.AssigneeUserId,
                    assigneeUserIds = issue.Assignees.Select(a => a.UserId).ToArray(),
                    parentIssueId = issue.ParentIssueId,
                    labels = issue.Labels.Select(l=>l.Label).ToArray(),
                    fixVersionIds = issue.Releases.Where(r=>r.Kind=="FIX").Select(r=>r.ReleaseId).ToArray(),
                    affectsVersionIds = issue.Releases.Where(r=>r.Kind=="AFFECTS").Select(r=>r.ReleaseId).ToArray(),
                    originalEstimateMinutes = issue.OriginalEstimateMinutes,
                    remainingEstimateMinutes = issue.RemainingEstimateMinutes,
                    componentId = issue.ComponentId,
                    storyPoints = issue.StoryPoints,
                    dueDate = issue.DueDate,
                    resolvedAt = issue.ResolvedAt
                });

                if (request.Summary is not null)
                {
                    issue.UpdateSummary(
                        request.Summary);
                }

                if (request.Description is not null)
                {
                    issue.UpdateDescription(
                        request.Description);
                }

                if (request.Priority is not null)
                {
                    ValidatePriority(
                        request.Priority);

                    issue.UpdatePriority(
                        NormalizePriority(
                            request.Priority));
                }

                if ((request.ClearAssignee && (request.AssigneeUserId.HasValue || (request.AssigneeUserIds != null && request.AssigneeUserIds.Length > 0))) ||
                    (request.ClearParentIssue && request.ParentIssueId.HasValue) ||
                    (request.ClearComponent && request.ComponentId.HasValue) ||
                    (request.ClearStoryPoints && request.StoryPoints.HasValue) ||
                    (request.ClearDueDate && request.DueDate.HasValue))
                    throw new ArgumentException("A field cannot be set and cleared together.");
                if (request.ClearAssignee) issue.AssignTo((long?)null);
                if (request.ClearParentIssue) {
                    await _issueRepository.ValidateParentAsync(tenantId, issue.ProjectId, issue.Id, null, transactionCancellationToken, issue.IssueType);
                    issue.SetParentIssue(null);
                }
                if (request.ClearComponent) issue.SetComponent(null);
                if (request.ClearStoryPoints) issue.SetStoryPoints(null);
                if (request.ClearDueDate) issue.SetDueDate(null);
                var updateAssigneeIds = request.AssigneeUserIds ?? (request.AssigneeUserId.HasValue ? [request.AssigneeUserId.Value] : null);
                if (updateAssigneeIds is not null)
                {
                    foreach (var assigneeId in updateAssigneeIds)
                    {
                        await _assigneeValidator.ValidateAsync(tenantId, issue.ProjectId, assigneeId, transactionCancellationToken);
                    }
                    issue.AssignTo(updateAssigneeIds);
                }

                if (request.ParentIssueId.HasValue)
                {
                    await _issueRepository.ValidateParentAsync(tenantId, issue.ProjectId, issue.Id, request.ParentIssueId, transactionCancellationToken);
                    issue.SetParentIssue(
                        request.ParentIssueId);
                }

                if (request.ComponentId.HasValue)
                {
                    await _issueRepository.ValidateComponentAsync(tenantId, issue.ProjectId, request.ComponentId, transactionCancellationToken);
                issue.SetComponent(
                        request.ComponentId);
                }

                if (request.StoryPoints.HasValue)
                {
                    ValidateStoryPoints(
                        request.StoryPoints);

                    issue.SetStoryPoints(
                        request.StoryPoints);
                }

                if (request.DueDate.HasValue)
                {
                    issue.SetDueDate(
                        request.DueDate);
                }

                if (request.StatusCode is not null)
                {
                await _workflow.ValidateAsync(tenantId, issue.ProjectId, issue.StatusCode, request.StatusCode!, transactionCancellationToken, request.ConfirmSkip);
                issue.TransitionTo(
                        request.StatusCode);
                }

                await _issueRepository.ValidateVersionsAsync(tenantId,issue.ProjectId,[..request.FixVersionIds??[],..request.AffectsVersionIds??[]],transactionCancellationToken);
                issue.SetPlanning(request.Labels,request.FixVersionIds,request.AffectsVersionIds,request.OriginalEstimateMinutes,request.RemainingEstimateMinutes);
                
                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);

                var newValues = JsonSerializer.Serialize(new
                {
                    issueType = issue.IssueType
                        .ToString()
                        .ToUpperInvariant(),
                    summary = issue.Summary,
                    description = issue.Description,
                    priority = issue.Priority,
                    statusCode = issue.StatusCode,
                    assigneeUserId = issue.AssigneeUserId,
                    assigneeUserIds = issue.Assignees.Select(a => a.UserId).ToArray(),
                    parentIssueId = issue.ParentIssueId,
                    labels = issue.Labels.Select(l=>l.Label).ToArray(),
                    fixVersionIds = issue.Releases.Where(r=>r.Kind=="FIX").Select(r=>r.ReleaseId).ToArray(),
                    affectsVersionIds = issue.Releases.Where(r=>r.Kind=="AFFECTS").Select(r=>r.ReleaseId).ToArray(),
                    originalEstimateMinutes = issue.OriginalEstimateMinutes,
                    remainingEstimateMinutes = issue.RemainingEstimateMinutes,
                    componentId = issue.ComponentId,
                    storyPoints = issue.StoryPoints,
                    dueDate = issue.DueDate,
                    resolvedAt = issue.ResolvedAt
                });

                var eventId = Guid.NewGuid();
                await _notifications.AssignmentAsync(issue, actorUserId, eventId, transactionCancellationToken, previouslyAssigned);
                var occurredAt = DateTimeOffset.UtcNow;

                var auditEvent = new IssueAuditEvent
                {
                    EventId = eventId,
                    EventType = "ISSUE_UPDATED",
                    TenantId = issue.TenantId,
                    EntityType = "ISSUE",
                    EntityId = issue.Id,
                    EntityKey = issue.IssueKey,
                    PerformedByUserId = actorUserId,
                    OldValues = oldValues,
                    NewValues = newValues,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        source = "JIRA"
                    }),
                    OccurredAt = occurredAt
                };

                await _outboxWriter.AddAsync(
                    eventId,
                    "ISSUE_UPDATED",
                    "aiworkhub.issue.audit",
                    JsonSerializer.Serialize(auditEvent),
                    occurredAt,
                    transactionCancellationToken);

                
                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);
            },
            cancellationToken);

        return issue is null
            ? null
            : Map(issue);
    }

    public async Task<IssueDto?> AssignAsync(
        long tenantId,
        long actorUserId,
        long issueId,
        AssignIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (actorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actorUserId));
        }

        if (issueId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(issueId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        var hasMulti = request.AssigneeUserIds is not null && request.AssigneeUserIds.Length > 0;
        var hasSingle = request.AssigneeUserId.HasValue;

        if (request.ClearAssignee)
        {
            if (hasSingle || hasMulti)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.AssigneeUserId),
                    "Cannot assign and clear assignee at the same time.");
            }
        }
        else
        {
            if (!hasSingle && !hasMulti)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.AssigneeUserId),
                    "An assignee must be provided when not clearing assignee.");
            }
            if (hasSingle && request.AssigneeUserId <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.AssigneeUserId));
            }
            if (hasMulti && request.AssigneeUserIds!.Any(id => id <= 0))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(request.AssigneeUserIds));
            }
        }

        Issue? issue = null;

        await _unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                issue =
                    await _issueRepository.GetByIdAsync(
                        tenantId,
                        issueId,
                        transactionCancellationToken);

                if (issue is null)
                {
                    return;
                }

                var previousAssignee = issue.AssigneeUserId;
                
                var previouslyAssigned = issue.Assignees.Select(a => a.UserId).ToList();
                if (previousAssignee.HasValue && !previouslyAssigned.Contains(previousAssignee.Value)) previouslyAssigned.Add(previousAssignee.Value);
                var oldValues = JsonSerializer.Serialize(new
                {
                    assigneeUserId = issue.AssigneeUserId,
                    assigneeUserIds = issue.Assignees.Select(a => a.UserId).ToArray()
                });

                if (request.ClearAssignee)
                {
                    issue.AssignTo((IEnumerable<long>?)null);
                }
                else
                {
                    var assigneesToSet = request.AssigneeUserIds ?? (request.AssigneeUserId.HasValue ? [request.AssigneeUserId.Value] : null);
                    if (assigneesToSet is not null)
                    {
                        foreach (var assigneeId in assigneesToSet)
                        {
                            await _assigneeValidator.ValidateAsync(tenantId, issue.ProjectId, assigneeId, transactionCancellationToken);
                        }
                        issue.AssignTo(assigneesToSet);
                    }
                }

                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);

                var newValues = JsonSerializer.Serialize(new
                {
                    assigneeUserId = issue.AssigneeUserId,
                    assigneeUserIds = issue.Assignees.Select(a => a.UserId).ToArray()
                });

                var eventId = Guid.NewGuid();
                await _notifications.AssignmentAsync(issue, actorUserId, eventId, transactionCancellationToken, previouslyAssigned);
                var occurredAt = DateTimeOffset.UtcNow;

                var auditEvent = new IssueAuditEvent
                {
                    EventId = eventId,
                    EventType = "ISSUE_ASSIGNED",
                    TenantId = issue.TenantId,
                    EntityType = "ISSUE",
                    EntityId = issue.Id,
                    EntityKey = issue.IssueKey,
                    PerformedByUserId = actorUserId,
                    OldValues = oldValues,
                    NewValues = newValues,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        source = "JIRA"
                    }),
                    OccurredAt = occurredAt
                };

                await _outboxWriter.AddAsync(
                    eventId,
                    "ISSUE_ASSIGNED",
                    "aiworkhub.issue.audit",
                    JsonSerializer.Serialize(auditEvent),
                    occurredAt,
                    transactionCancellationToken);

                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);
            },
            cancellationToken);

        return issue is null
            ? null
            : Map(issue);
    }

    public async Task<IssueDto?> TransitionAsync(
        long tenantId,
        long actorUserId,
        long issueId,
        TransitionIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (actorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actorUserId));
        }

        if (issueId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(issueId));
        }

        if (request is null)
        {
            throw new ArgumentNullException(
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(
                request.StatusCode))
        {
            throw new ArgumentException(
                "Issue status is required.",
                nameof(request.StatusCode));
        }

        Issue? issue = null;

        await _unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                issue =
                    await _issueRepository.GetByIdAsync(
                        tenantId,
                        issueId,
                        transactionCancellationToken);

                if (issue is null)
                {
                    return;
                }

                var oldValues = JsonSerializer.Serialize(new
                {
                    statusCode = issue.StatusCode,
                    resolvedAt = issue.ResolvedAt
                });

                await _workflow.ValidateAsync(tenantId, issue.ProjectId, issue.StatusCode, request.StatusCode!, transactionCancellationToken, request.ConfirmSkip);
                issue.TransitionTo(
                    request.StatusCode);

                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);

                var newValues = JsonSerializer.Serialize(new
                {
                    statusCode = issue.StatusCode,
                    resolvedAt = issue.ResolvedAt
                });

                var eventId = Guid.NewGuid();
                var occurredAt = DateTimeOffset.UtcNow;

                var auditEvent = new IssueAuditEvent
                {
                    EventId = eventId,
                    EventType = "ISSUE_TRANSITIONED",
                    TenantId = issue.TenantId,
                    EntityType = "ISSUE",
                    EntityId = issue.Id,
                    EntityKey = issue.IssueKey,
                    PerformedByUserId = actorUserId,
                    OldValues = oldValues,
                    NewValues = newValues,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        source = "JIRA"
                    }),
                    OccurredAt = occurredAt
                };

                await _outboxWriter.AddAsync(
                    eventId,
                    "ISSUE_TRANSITIONED",
                    "aiworkhub.issue.audit",
                    JsonSerializer.Serialize(auditEvent),
                    occurredAt,
                    transactionCancellationToken);

                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);
            },
            cancellationToken);

        return issue is null
            ? null
            : Map(issue);
    }

    public async Task<IssueDto?> DeleteAsync(
        long tenantId,
        long actorUserId,
        long issueId,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (actorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(actorUserId));
        }

        if (issueId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(issueId));
        }

        Issue? issue = null;

        await _unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                issue =
                    await _issueRepository.GetByIdAsync(
                        tenantId,
                        issueId,
                        transactionCancellationToken);

                if (issue is null)
                {
                    return;
                }

                var oldValues = JsonSerializer.Serialize(new
                {
                    issueType = issue.IssueType
                        .ToString()
                        .ToUpperInvariant(),
                    summary = issue.Summary,
                    description = issue.Description,
                    statusCode = issue.StatusCode,
                    priority = issue.Priority,
                    reporterUserId = issue.ReporterUserId,
                    assigneeUserId = issue.AssigneeUserId,
                    assigneeUserIds = issue.Assignees.Select(a => a.UserId).ToArray(),
                    parentIssueId = issue.ParentIssueId,
                    labels = issue.Labels.Select(l=>l.Label).ToArray(),
                    fixVersionIds = issue.Releases.Where(r=>r.Kind=="FIX").Select(r=>r.ReleaseId).ToArray(),
                    affectsVersionIds = issue.Releases.Where(r=>r.Kind=="AFFECTS").Select(r=>r.ReleaseId).ToArray(),
                    originalEstimateMinutes = issue.OriginalEstimateMinutes,
                    remainingEstimateMinutes = issue.RemainingEstimateMinutes,
                    componentId = issue.ComponentId,
                    storyPoints = issue.StoryPoints,
                    dueDate = issue.DueDate,
                    resolvedAt = issue.ResolvedAt,
                    isDeleted = issue.IsDeleted
                });

                issue.Delete(actorUserId);

                var eventId = Guid.NewGuid();
                var occurredAt = DateTimeOffset.UtcNow;

                var newValues = JsonSerializer.Serialize(new
                {
                    issue.IsDeleted,
                    issue.DeletedAt,
                    issue.DeletedByUserId
                });

                var auditEvent = new IssueAuditEvent
                {
                    EventId = eventId,
                    EventType = "ISSUE_DELETED",
                    TenantId = issue.TenantId,
                    EntityType = "ISSUE",
                    EntityId = issue.Id,
                    EntityKey = issue.IssueKey,
                    PerformedByUserId = actorUserId,
                    OldValues = oldValues,
                    NewValues = newValues,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        source = "JIRA"
                    }),
                    OccurredAt = occurredAt
                };

                await _outboxWriter.AddAsync(
                    eventId,
                    "ISSUE_DELETED",
                    "aiworkhub.issue.audit",
                    JsonSerializer.Serialize(auditEvent),
                    occurredAt,
                    transactionCancellationToken);

                await _issueRepository.SaveChangesAsync(
                    transactionCancellationToken);
            },
            cancellationToken);

        return issue is null
            ? null
            : Map(issue);
    }

    private static void ValidatePriority(
        string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
        {
            throw new ArgumentException(
                "Issue priority is required.",
                nameof(priority));
        }

        if (!ValidPriorities.Contains(
                priority.Trim()))
        {
            throw new ArgumentException(
                $"Invalid issue priority: {priority}. " +
                "Allowed values are LOWEST, LOW, MEDIUM, HIGH, HIGHEST.",
                nameof(priority));
        }
    }

    private static string NormalizePriority(
        string priority)
    {
        return priority
            .Trim()
            .ToUpperInvariant();
    }

    private static void ValidateStoryPoints(
        int? storyPoints)
    {
        if (storyPoints.HasValue &&
            storyPoints.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(storyPoints),
                "Story points cannot be negative.");
        }
    }

    private static IssueDto Map(
        Issue issue)
    {
        return new IssueDto
        {
            Version = issue.Version,
            Labels = issue.Labels.Select(l=>l.Label).ToArray(),
            FixVersionIds = issue.Releases.Where(r=>r.Kind=="FIX").Select(r=>r.ReleaseId).ToArray(),
            AffectsVersionIds = issue.Releases.Where(r=>r.Kind=="AFFECTS").Select(r=>r.ReleaseId).ToArray(),
            OriginalEstimateMinutes = issue.OriginalEstimateMinutes,
            RemainingEstimateMinutes = issue.RemainingEstimateMinutes,
            LoggedTimeMinutes = issue.LoggedTimeMinutes,
            Id = issue.Id,
            TenantId = issue.TenantId,
            ProjectId = issue.ProjectId,
            IssueNumber = issue.IssueNumber,
            IssueKey = issue.IssueKey,
            IssueType =
                issue.IssueType
                    .ToString()
                    .ToUpperInvariant(),
            Summary = issue.Summary,
            Description = issue.Description,
            StatusCode = issue.StatusCode,
            Priority = issue.Priority,
            ReporterUserId = issue.ReporterUserId,
            AssigneeUserId = issue.AssigneeUserId,
            AssigneeUserIds = issue.Assignees.Select(a => a.UserId).ToArray(),
            ParentIssueId = issue.ParentIssueId,
            ComponentId = issue.ComponentId,
            StoryPoints = issue.StoryPoints,
            DueDate = issue.DueDate,
            ResolvedAt = issue.ResolvedAt,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt
        };
    }
}