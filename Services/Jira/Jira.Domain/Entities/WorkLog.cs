using Jira.Domain.Common;
namespace Jira.Domain.Entities;
public sealed class WorkLog : Entity
{
    public long TenantId { get; set; }
    public long UserId { get; set; }
    public long IssueId { get; set; }
    public long ProjectId { get; set; }
    public long PeriodId { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }
    public bool Billable { get; set; }
    public uint Version { get; private set; }
    public void Update(DateOnly date, int minutes, string? description, bool billable, TimeOnly? startTime=null, TimeOnly? endTime=null) {
        if (startTime.HasValue != endTime.HasValue) throw new ArgumentException("Provide both start and end times.");
        if (startTime.HasValue) {
            if (endTime <= startTime || startTime.Value.Ticks % TimeSpan.TicksPerMinute != 0 || endTime!.Value.Ticks % TimeSpan.TicksPerMinute != 0) throw new ArgumentException("End time must be after start time on the same day, using whole minutes.");
            minutes = (int)(endTime.Value.ToTimeSpan() - startTime.Value.ToTimeSpan()).TotalMinutes;
        }
        StartTime = startTime; EndTime = endTime;
        if (minutes is < 1 or > 1440) throw new ArgumentException("Duration must be between 1 and 1440 minutes.");
        if (date < new DateOnly(2000,1,1) || date > DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1)) throw new ArgumentException("Work date is outside the allowed range.");
        if (description?.Length > 4000) throw new ArgumentException("Description cannot exceed 4000 characters.");
        WorkDate=date; DurationMinutes=minutes; Description=description?.Trim(); Billable=billable; Touch();
    }
}
