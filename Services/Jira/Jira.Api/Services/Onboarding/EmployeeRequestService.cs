using System.Text.Json;
using Jira.Api.Onboarding;
using IdentityTenant.Contracts;
using Jira.Api.Controllers;
using Jira.Application.Abstractions.Persistence;
using Jira.Application.Abstractions.Services;
using Jira.Application.Services;
using Jira.Contracts.Issues;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Onboarding;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Jira.Api.Services.Onboarding;

public sealed class EmployeeRequestService(
    JiraDbContext db, JiraResourceScope scope, ITenantContext actor, IIssueService issues, IJiraUnitOfWork unit, IOutboxWriter outbox,
    EmployeeOnboardingClient identity, global::Authentication.OnboardingApprovalSigner signer, INotificationWriter notifications, ProjectAuditWriter projectAudit)
{
    private IQueryable<EmployeeOnboarding> Visible(long id) => db.Set<EmployeeOnboarding>().Where(x => x.TenantId == actor.TenantId && x.IssueId == id && scope.Issues(actor.TenantId).Any(i => i.Id == x.IssueId));

    private Task Lock(long id, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-id})", ct);

    private async Task Audit(EmployeeOnboarding row, string action, CancellationToken ct, long[]? recipients = null)
    {
        
        var issue = await db.Issues.SingleAsync(i => i.TenantId == actor.TenantId && i.Id == row.IssueId, ct);
        var ev = new IssueAuditEvent
        {
            EventId = Guid.NewGuid(), EventType = action, TenantId = actor.TenantId, EntityType = "ISSUE", EntityId = row.IssueId, EntityKey = issue.IssueKey,
            PerformedByUserId = actor.UserId, OccurredAt = DateTimeOffset.UtcNow,
            NewValues = JsonSerializer.Serialize(new { requestType = "ADD_EMPLOYEE", row.Status, row.EmployeeId, row.Revision })
        };
        if (action == "EMPLOYEE_REQUEST_CREATED") await notifications.OnboardingAsync(issue, actor.UserId, ev.EventId, "EMPLOYEE_ONBOARDING_REQUESTED", recipients ?? [], ct);
        if (action == "EMPLOYEE_REQUEST_COMPLETED") await notifications.OnboardingAsync(issue, actor.UserId, ev.EventId, "EMPLOYEE_ONBOARDING_COMPLETED", [issue.ReporterUserId], ct);
        await outbox.AddAsync(ev.EventId, action, "aiworkhub.issue.audit", JsonSerializer.Serialize(ev), ev.OccurredAt, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CreateEmployeeRequestResult> Create(CreateEmployeeRequest input, CancellationToken ct)
    {
        var checkedData = await identity.Send("api/employees/onboarding/validate", input.Proposal, ct);
        if (checkedData.Status != 200) return new(null, new IdentityRejection(checkedData.Status, checkedData.Body));
        IssueDto issue = null!;
        await unit.ExecuteInTransactionAsync(async t =>
        {
            issue = await issues.CreateAsync(actor.TenantId, actor.UserId, input.Issue, t);
            var row = new EmployeeOnboarding { IssueId = issue.Id, TenantId = actor.TenantId, Data = checkedData.Body.GetProperty("validated").GetRawText() };
            db.Add(row);
            var reviewers = new List<long>();
            foreach (var reviewer in checkedData.Body.GetProperty("reviewers").EnumerateArray())
            {
                var recipient = reviewer.GetProperty("userId").GetInt64();
                var recipientContext = new TenantContext.TenantContext();
                recipientContext.Set(recipient, actor.TenantId, reviewer.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!));
                
                if (await new JiraResourceScope(db, recipientContext).Issues(actor.TenantId).AnyAsync(i => i.Id == issue.Id, t)) reviewers.Add(recipient);
            }
            await Audit(row, "EMPLOYEE_REQUEST_CREATED", t, reviewers.ToArray());
        }, ct);
        return new(issue, null);
    }

    



    public async Task<IEmployeeRequestView?> Get(long id, CancellationToken ct)
    {
        if (!await scope.Issues(actor.TenantId).AnyAsync(i => i.Id == id, ct)) return null;
        var row = await Visible(id).AsNoTracking().SingleOrDefaultAsync(ct);
        if (row is null) return new NotAnEmployeeRequestDto();
        var issue = await scope.Issues(actor.TenantId).Where(i => i.Id == id).Select(i => new { i.ReporterUserId, i.CreatedAt }).SingleAsync(ct);
        return new EmployeeRequestDto(row.IssueId, row.Status, row.Revision, row.ReviewedBy, row.ReviewedAt, row.ReviewNote, row.EmployeeId, issue.ReporterUserId, issue.CreatedAt, JsonSerializer.Deserialize<JsonElement>(row.Data));
    }

    public async Task<IEmployeeRequestView?> Review(long id, ReviewEmployeeRequest input, CancellationToken ct)
    {
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(id, t);
            var row = await Visible(id).SingleOrDefaultAsync(t) ?? throw new ResourceNotFoundException();
            if (row.Revision != input.Revision || row.Status != "SUBMITTED") throw new ProjectConflictException("Request changed or has already been reviewed. Reload it.");
            row.Status = input.Decision;
            row.ReviewedBy = actor.UserId;
            row.ReviewedAt = DateTimeOffset.UtcNow;
            row.ReviewNote = input.Note;
            row.Revision++;
            await Audit(row, "EMPLOYEE_REQUEST_" + input.Decision, t);
        }, ct);
        return await Get(id, ct);
    }

    public async Task<CompleteEmployeeRequestResult> Complete(long id, EmployeeRequest input, CancellationToken ct)
    {
        IdentityRejection? rejection = null;
        await unit.ExecuteInTransactionAsync(async t =>
        {
            await Lock(id, t);
            var row = await Visible(id).SingleOrDefaultAsync(t) ?? throw new ResourceNotFoundException();
            if (row.Status == "COMPLETED") return;
            if (row.Status != "APPROVED") throw new ProjectConflictException("Approve the request before creating the employee.");
            
            var proposal = JsonDocument.Parse(row.Data).RootElement.GetProperty("proposal");
            var approval = signer.Sign(actor.TenantId, id, proposal.GetProperty("workEmail").GetString()!,
                proposal.TryGetProperty("teamId", out var team) && team.ValueKind == JsonValueKind.Number ? team.GetInt64() : null, DateTimeOffset.UtcNow);
            var created = await identity.Send($"api/employees/from-onboarding/{id}", input, t, new(global::Authentication.OnboardingApprovalSigner.Header, approval));
            if (created.Status is not (200 or 201)) { rejection = new IdentityRejection(created.Status, created.Body); return; }
            var employeeId = created.Body.GetProperty("id").GetInt64();
            long? userId = created.Body.TryGetProperty("userId", out var uProp) && uProp.ValueKind == JsonValueKind.Number ? uProp.GetInt64() : null;
            row.EmployeeId = employeeId;
            row.Status = "COMPLETED";
            row.Revision++;
            if (userId.HasValue && userId.Value > 0)
            {
                var issue = await db.Issues.SingleAsync(i => i.TenantId == actor.TenantId && i.Id == row.IssueId, t);
                var changed = await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO project_members (tenant_id, project_id, user_id, role, created_at) VALUES ({actor.TenantId}, {issue.ProjectId}, {userId.Value}, 'DEVELOPER', CURRENT_TIMESTAMP) ON CONFLICT (project_id, user_id) DO NOTHING", t);
                if (changed > 0)
                {
                    var project = await db.Projects.SingleOrDefaultAsync(p => p.TenantId == actor.TenantId && p.Id == issue.ProjectId, t);
                    if (project is not null) await projectAudit.RecordAsync(project, "PROJECT_MEMBER_ADDED", null, new { userId = userId.Value, role = "DEVELOPER" }, t);
                }
            }
            await Audit(row, "EMPLOYEE_REQUEST_COMPLETED", t);
        }, ct);
        return rejection is not null ? new(null, rejection) : new(await Get(id, ct), null);
    }
}
