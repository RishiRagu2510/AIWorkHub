using System.Text.Json;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Abstractions.Services;
using Jira.Contracts.Issues;
using Jira.Domain.Entities;
using Messaging.Events;

namespace Jira.Application.Services;

public sealed class IssueCommentService : IIssueCommentService
{
    private readonly IIssueCommentRepository _commentRepository;
    private readonly IIssueRepository _issueRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IJiraUnitOfWork _unitOfWork;
    private readonly INotificationWriter _notifications;
    private readonly IMentionService _mentions;

    public IssueCommentService(
        IIssueCommentRepository commentRepository,
        IIssueRepository issueRepository,
        IOutboxWriter outboxWriter,
        IJiraUnitOfWork unitOfWork, INotificationWriter notifications, IMentionService mentions)
    {
        _commentRepository = commentRepository;
        _issueRepository = issueRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _notifications = notifications;
        _mentions = mentions;
    }

    public async Task<IssueCommentDto> CreateAsync(
        long tenantId,
        long authorUserId,
        long issueId,
        CreateIssueCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tenantId));
        }

        if (authorUserId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(authorUserId));
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

        if (string.IsNullOrWhiteSpace(request.Body) || request.Body.Length > 20000)
        {
            throw new ArgumentException(
                "Comment body is required.",
                nameof(request.Body));
        }

        IssueComment? comment = null;

        await _unitOfWork.ExecuteInTransactionAsync(
            async transactionCancellationToken =>
            {
                var issue =
                    await _issueRepository.GetByIdAsync(
                        tenantId,
                        issueId,
                        transactionCancellationToken);

                if (issue is null)
                {
                    throw new ResourceNotFoundException();
                }

                var mentions = request.MentionedUserIds ?? [];
                if (mentions.Length > 10 || mentions.Any(id => id <= 0))
                    throw new ArgumentException("Choose at most 10 valid mention recipients.");
                comment = new IssueComment(
                    tenantId: tenantId,
                    issueId: issueId,
                    authorUserId: authorUserId,
                    body: request.Body);

                await _commentRepository.AddAsync(
                    comment,
                    transactionCancellationToken);

                
                await _commentRepository.SaveChangesAsync(
                    transactionCancellationToken);

                var eventId = Guid.NewGuid();
                await _mentions.NotifyAsync(issue, authorUserId, request.Body, mentions, eventId, transactionCancellationToken);
                await _notifications.CommentAsync(issue, authorUserId, eventId, transactionCancellationToken, mentions);
                var occurredAt = DateTimeOffset.UtcNow;

                var auditEvent = new IssueAuditEvent
                {
                    EventId = eventId,
                    EventType = "ISSUE_COMMENT_ADDED",
                    TenantId = tenantId,
                    EntityType = "ISSUE",
                    EntityId = issue.Id,
                    EntityKey = issue.IssueKey,
                    PerformedByUserId = authorUserId,
                    OldValues = null,
                    NewValues = JsonSerializer.Serialize(new
                    {
                        commentId = comment.Id,
            Version = comment.Version,
                        body = comment.Body
                    }),
                    Metadata = JsonSerializer.Serialize(new
                    {
                        source = "JIRA"
                    }),
                    OccurredAt = occurredAt
                };

                await _outboxWriter.AddAsync(
                    eventId,
                    "ISSUE_COMMENT_ADDED",
                    "aiworkhub.issue.audit",
                    JsonSerializer.Serialize(auditEvent),
                    occurredAt,
                    transactionCancellationToken);

                
                await _commentRepository.SaveChangesAsync(
                    transactionCancellationToken);
            },
            cancellationToken);

        if (comment is null)
        {
            throw new InvalidOperationException(
                "Comment creation did not produce a comment.");
        }

        return Map(comment);
    }

    public async Task<IReadOnlyList<IssueCommentDto>> GetByIssueAsync(
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

        if (issue is null)
        {
            throw new ResourceNotFoundException();
        }

        var comments =
            await _commentRepository.GetByIssueAsync(
                tenantId,
                issueId,
                cancellationToken);

        return comments
            .Select(Map)
            .ToList();
    }

    private static IssueCommentDto Map(
        IssueComment comment)
    {
        return new IssueCommentDto
        {
            Id = comment.Id,
            Version = comment.Version,
            TenantId = comment.TenantId,
            IssueId = comment.IssueId,
            AuthorUserId = comment.AuthorUserId,
            Body = comment.Body,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };
    }
}