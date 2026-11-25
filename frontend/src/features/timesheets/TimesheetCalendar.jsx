import { useState } from 'react';
import { durationLabel, dateText } from './time';
import WorkLogForm from './WorkLogForm';
import { Icon } from '../../components/Icon';
import { Badge } from '../../components/common';

export default function TimesheetCalendar({
  items = [],
  anchor,
  onAnchorChange,
  canCreate = false,
}) {
  const [editingLog, setEditingLog] = useState(null);
  const [loggingDate, setLoggingDate] = useState(null);

  // Compute 7 days of the week starting from Monday
  const anchorDate = new Date(anchor + 'T12:00:00');
  const dayOfWeek = (anchorDate.getDay() + 6) % 7; // Monday = 0
  const monday = new Date(anchorDate);
  monday.setDate(monday.getDate() - dayOfWeek);

  const days = Array.from({ length: 7 }, (_, i) => {
    const d = new Date(monday);
    d.setDate(d.getDate() + i);
    const dateStr = dateText(d);
    const dayLogs = items.filter((log) => log.workDate === dateStr);
    const totalMinutes = dayLogs.reduce((acc, log) => acc + (log.durationMinutes || 0), 0);
    return {
      date: dateStr,
      dateObj: d,
      logs: dayLogs,
      totalMinutes,
      isToday: dateText(new Date()) === dateStr,
    };
  });

  const weekTotalMinutes = days.reduce((acc, d) => acc + d.totalMinutes, 0);

  function shiftWeek(direction) {
    const next = new Date(anchorDate);
    next.setDate(next.getDate() + direction * 7);
    onAnchorChange(dateText(next));
  }

  return (
    <div className="timesheet-calendar">
      <div className="calendar-nav-bar">
        <div className="calendar-nav-actions">
          <button onClick={() => shiftWeek(-1)} aria-label="Previous week">
            ← Previous week
          </button>
          <button
            onClick={() => onAnchorChange(dateText(new Date()))}
            aria-label="Current week"
          >
            Today
          </button>
          <button onClick={() => shiftWeek(1)} aria-label="Next week">
            Next week →
          </button>
        </div>
        <strong className="calendar-week-range">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
            <rect x="3" y="4" width="18" height="18" rx="2" />
            <path d="M16 2v4M8 2v4M3 10h18" />
          </svg>
          <span>{days[0].date} — {days[6].date}</span>
        </strong>
        <div className="calendar-week-summary">
          Week total: <strong>{durationLabel(weekTotalMinutes)} hrs</strong>
        </div>
      </div>

      <div className="calendar-week-grid">
        {days.map((day) => {
          const dayNum = day.dateObj.getDay();
          const isWeekend = dayNum === 0 || dayNum === 6;
          return (
            <div
              key={day.date}
              className={`calendar-day-column${day.isToday ? ' is-today' : ''}${isWeekend ? ' is-weekend' : ''}`}
            >
              <div className="calendar-day-header">
                <span className="calendar-day-name">
                  {day.dateObj.toLocaleDateString(undefined, { weekday: 'short' })}
                </span>
                <span className="calendar-day-date">
                  {day.dateObj.toLocaleDateString(undefined, { month: 'numeric', day: 'numeric' })}
                </span>
                <span className="calendar-day-badge">
                  {durationLabel(day.totalMinutes)}h
                </span>
              </div>

            <div className="calendar-day-logs">
              {day.logs.map((log) => (
                <div
                  key={log.id}
                  className="calendar-log-card"
                  onClick={() => setEditingLog(log)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => { if (e.key === 'Enter') setEditingLog(log); }}
                  aria-label={`Log for ${log.issueKey}: ${durationLabel(log.durationMinutes)} hours`}
                >
                  <div className="calendar-log-top">
                    <span className="calendar-log-key">{log.issueKey}</span>
                    <strong className="calendar-log-hours">{durationLabel(log.durationMinutes)}h</strong>
                  </div>
                  <div className="calendar-log-summary">{log.issueSummary || log.description || '—'}</div>
                  <div className="calendar-log-footer">
                    {log.billable && <span className="calendar-billable-tag">Billable</span>}
                    {log.periodStatus && <Badge value={log.periodStatus} />}
                  </div>
                </div>
              ))}

              {canCreate && (
                <button
                  className="calendar-add-log-btn"
                  onClick={() => setLoggingDate(day.date)}
                  aria-label={`Log work for ${day.date}`}
                >
                  <Icon name="plus" /> Add time
                </button>
              )}
            </div>

            <div className="calendar-day-footer">
              Total: {durationLabel(day.totalMinutes)}h
            </div>
          </div>
        );
      })}
      </div>

      {editingLog && (
        <div className="panel" style={{ marginTop: '16px', borderLeft: '4px solid #f59e0b' }}>
          <WorkLogForm log={editingLog} onClose={() => setEditingLog(null)} />
        </div>
      )}

      {loggingDate && (
        <div className="panel" style={{ marginTop: '16px', borderLeft: '4px solid #10b981' }}>
          <WorkLogForm
            initialDate={loggingDate}
            onClose={() => setLoggingDate(null)}
          />
        </div>
      )}
    </div>
  );
}
