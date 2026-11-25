using System.ComponentModel.DataAnnotations;
namespace Jira.Contracts.Timesheets;
public sealed record WorkLogRequest([Range(1,long.MaxValue)] long IssueId, DateOnly WorkDate,
    [Range(0,1440)] int DurationMinutes, [StringLength(4000)] string? Description, bool Billable, TimeOnly? StartTime=null, TimeOnly? EndTime=null);
public sealed record WorkLogDto(long Id,long UserId,long IssueId,long ProjectId,long PeriodId,DateOnly WorkDate,
    int DurationMinutes,string? Description,bool Billable,DateTimeOffset CreatedAt,DateTimeOffset UpdatedAt,uint Version,
    string IssueKey,string IssueSummary,string ProjectName,string PeriodStatus,TimeOnly? StartTime=null,TimeOnly? EndTime=null) { public string Status => PeriodStatus; }
public sealed class TimesheetQuery {
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public long? UserId { get; set; }
    public long? ProjectId { get; set; }
    public long? IssueId { get; set; }
    public bool? Billable { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; }=1;
    public int PageSize { get; set; }=25;
}
public sealed record WorkLogPage(IReadOnlyList<WorkLogDto> Items,int Page,int PageSize,int TotalCount,int TotalPages);
public sealed record TimeTotal(long TotalMinutes,long BillableMinutes,long NonBillableMinutes);
public sealed record DayTotal(DateOnly Date,long Minutes,long BillableMinutes);
public sealed record GroupTotal(long Id,long Minutes,long BillableMinutes);
public sealed record PeriodDto(long Id,long UserId,DateOnly PeriodStart,DateOnly PeriodEnd,string Status,
    DateTimeOffset? SubmittedAt,long? ApproverUserId,long? ApprovedByUserId,DateTimeOffset? ApprovedAt,string? RejectionReason,uint Version,long TotalMinutes,long BillableMinutes=0,IReadOnlyList<PeriodProjectDto>? Projects=null);
public sealed record PeriodProjectDto(long ProjectId,string ProjectName,long Minutes,long BillableMinutes);
public sealed record TimesheetReport(DateOnly From,DateOnly To,long TotalMinutes,long BillableMinutes,long NonBillableMinutes,
    IReadOnlyList<DayTotal> DailyTotals,IReadOnlyList<GroupTotal> UserTotals,IReadOnlyList<GroupTotal> ProjectTotals,
    IReadOnlyList<GroupTotal> IssueTotals,WorkLogPage WorkLogs,IReadOnlyList<PeriodDto> Periods,int PeriodTotalCount);
public sealed record PeriodAction([Range(1,long.MaxValue)] long PeriodId,long? ApproverUserId=null,[StringLength(2000)] string? Reason=null);
public sealed record TimesheetApprover(long Id,string DisplayName,string[] Roles);

public sealed record TimesheetHierarchyNode(long IssueId, string IssueKey, string Summary, string IssueType,
    long? ParentIssueId, long Minutes, long BillableMinutes);
public sealed record TimesheetProjectGroup(long UserId, long ProjectId, string ProjectName, long Minutes,
    long BillableMinutes, IReadOnlyList<TimesheetHierarchyNode> Issues);
public sealed record TimesheetHierarchyPage(IReadOnlyList<TimesheetProjectGroup> Items, int Page, int PageSize, int TotalCount, int TotalPages);
