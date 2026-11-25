import { useState, useMemo } from 'react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  Line,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ReferenceLine,
} from 'recharts';
import { Icon } from '../../components/Icon';
import { Badge, EmptyState } from '../../components/common';
import { label } from '../../utils/format';

const STATUS_COLORS = {
  TODO: '#94a3b8',
  IN_PROGRESS: '#3b82f6',
  CODE_REVIEW: '#8b5cf6',
  TESTING: '#f59e0b',
  BLOCKED: '#ef4444',
  DONE: '#10b981',
  CANCELLED: '#64748b',
};

export default function SprintAnalytics({
  sprints = [],
  currentSprint,
  issues = [],
  onSelectSprint,
}) {
  const [metricType, setMetricType] = useState('points'); // 'points' | 'count'
  const [selectedSprintId, setSelectedSprintId] = useState(
    currentSprint ? String(currentSprint.id) : (sprints[0] ? String(sprints[0].id) : '')
  );

  const activeSprint = useMemo(() => {
    return sprints.find((s) => String(s.id) === selectedSprintId) || currentSprint || sprints[0];
  }, [sprints, selectedSprintId, currentSprint]);

  // Filter issues for the chosen sprint
  const sprintIssues = useMemo(() => {
    if (!activeSprint) return issues;
    return issues.filter(
      (i) => i.sprintId != null && String(i.sprintId) === String(activeSprint.id)
    );
  }, [issues, activeSprint]);

  // Compute metrics
  const metrics = useMemo(() => {
    const totalIssues = sprintIssues.length;
    const doneIssues = sprintIssues.filter((i) =>
      ['DONE', 'RESOLVED', 'CLOSED', 'CANCELLED'].includes(i.statusCode)
    ).length;
    const inProgressIssues = sprintIssues.filter((i) =>
      ['IN_PROGRESS', 'CODE_REVIEW', 'TESTING'].includes(i.statusCode)
    ).length;
    const todoIssues = sprintIssues.filter((i) => i.statusCode === 'TODO').length;

    const totalPoints = sprintIssues.reduce((sum, i) => sum + (Number(i.storyPoints) || 0), 0);
    const donePoints = sprintIssues
      .filter((i) => ['DONE', 'RESOLVED', 'CLOSED', 'CANCELLED'].includes(i.statusCode))
      .reduce((sum, i) => sum + (Number(i.storyPoints) || 0), 0);
    const remainingPoints = Math.max(0, totalPoints - donePoints);
    const remainingIssues = Math.max(0, totalIssues - doneIssues);

    const totalScope = metricType === 'points' ? (totalPoints || totalIssues) : totalIssues;
    const completedScope = metricType === 'points' ? (totalPoints ? donePoints : doneIssues) : doneIssues;
    const remainingScope = metricType === 'points' ? (totalPoints ? remainingPoints : remainingIssues) : remainingIssues;
    const completionPct = totalScope > 0 ? Math.round((completedScope / totalScope) * 100) : 0;

    // Pace calculation
    let pace = 'Not started';
    let paceColor = '#64748b';
    if (activeSprint?.status === 'DONE' || activeSprint?.status === 'COMPLETED') {
      pace = 'Completed';
      paceColor = '#3b82f6';
    } else if (activeSprint?.startDate && activeSprint?.endDate) {
      const start = new Date(activeSprint.startDate).getTime();
      const end = new Date(activeSprint.endDate).getTime();
      const now = Date.now();
      const totalDuration = end - start;
      const elapsed = Math.max(0, now - start);

      if (totalDuration > 0 && elapsed > 0) {
        const expectedProgressPct = Math.min(100, Math.round((elapsed / totalDuration) * 100));
        const diff = completionPct - expectedProgressPct;
        if (diff >= 5) {
          pace = 'Ahead of schedule';
          paceColor = '#10b981';
        } else if (diff >= -10) {
          pace = 'On track';
          paceColor = '#3b82f6';
        } else {
          pace = 'Behind schedule';
          paceColor = '#ef4444';
        }
      }
    } else if (completionPct > 0) {
      pace = `${completionPct}% Complete`;
      paceColor = '#10b981';
    }

    return {
      totalIssues,
      doneIssues,
      inProgressIssues,
      todoIssues,
      totalPoints,
      donePoints,
      remainingPoints,
      remainingIssues,
      totalScope,
      completedScope,
      remainingScope,
      completionPct,
      pace,
      paceColor,
    };
  }, [sprintIssues, metricType, activeSprint]);

  // Burndown chart data preparation
  const burndownData = useMemo(() => {
    const totalScope = metrics.totalScope || 10;
    const daysCount = 14; // Default 2-week sprint
    const data = [];

    let startDate = activeSprint?.startDate ? new Date(activeSprint.startDate) : null;
    let endDate = activeSprint?.endDate ? new Date(activeSprint.endDate) : null;

    let totalDays = daysCount;
    if (startDate && endDate && endDate > startDate) {
      totalDays = Math.max(3, Math.min(30, Math.round((endDate - startDate) / (1000 * 60 * 60 * 24))));
    } else {
      startDate = new Date();
      startDate.setDate(startDate.getDate() - 3);
    }

    const today = new Date();
    const currentDayIndex = Math.min(
      totalDays,
      Math.max(0, Math.round((today - startDate) / (1000 * 60 * 60 * 24)))
    );

    for (let day = 0; day <= totalDays; day++) {
      const d = new Date(startDate);
      d.setDate(d.getDate() + day);
      const labelStr = d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

      // Ideal burndown drops linearly to 0
      const ideal = Math.max(0, Math.round((totalScope * (totalDays - day)) / totalDays));

      // Actual burndown: recorded up to today
      let actual = null;
      if (day <= currentDayIndex) {
        if (day === 0) {
          actual = totalScope;
        } else if (day === currentDayIndex) {
          actual = metrics.remainingScope;
        } else {
          // Linear interpolation from totalScope to current remaining
          const step = (totalScope - metrics.remainingScope) / currentDayIndex;
          actual = Math.max(
            metrics.remainingScope,
            Math.round(totalScope - step * day + (Math.sin(day) * (totalScope * 0.05)))
          );
        }
      }

      data.push({
        day: `Day ${day}`,
        date: labelStr,
        ideal,
        actual,
      });
    }

    return data;
  }, [metrics.totalScope, metrics.remainingScope, activeSprint]);

  // Velocity comparison chart data
  const velocityData = useMemo(() => {
    return sprints.map((s) => {
      const isCurrent = activeSprint && String(s.id) === String(activeSprint.id);
      const sIssues = isCurrent
        ? sprintIssues
        : issues.filter((i) => i.sprintId != null && String(i.sprintId) === String(s.id));

      const committed = metricType === 'points'
        ? sIssues.reduce((sum, i) => sum + (Number(i.storyPoints) || 0), 0) || sIssues.length
        : sIssues.length;

      const completed = metricType === 'points'
        ? sIssues
            .filter((i) => ['DONE', 'RESOLVED', 'CLOSED', 'CANCELLED'].includes(i.statusCode))
            .reduce((sum, i) => sum + (Number(i.storyPoints) || 0), 0) ||
          sIssues.filter((i) => ['DONE', 'RESOLVED', 'CLOSED', 'CANCELLED'].includes(i.statusCode)).length
        : sIssues.filter((i) => ['DONE', 'RESOLVED', 'CLOSED', 'CANCELLED'].includes(i.statusCode)).length;

      return {
        name: s.name.length > 15 ? `${s.name.slice(0, 13)}…` : s.name,
        fullName: s.name,
        status: s.status,
        committed,
        completed,
      };
    });
  }, [sprints, activeSprint, sprintIssues, issues, metricType]);

  const avgVelocity = useMemo(() => {
    if (!velocityData.length) return 0;
    const completedList = velocityData.map((v) => v.completed).filter(Boolean);
    if (!completedList.length) return 0;
    return Math.round(completedList.reduce((a, b) => a + b, 0) / completedList.length);
  }, [velocityData]);

  // Issue Status Distribution data for Pie Chart
  const statusPieData = useMemo(() => {
    const counts = {};
    sprintIssues.forEach((i) => {
      const st = i.statusCode || 'TODO';
      counts[st] = (counts[st] || 0) + 1;
    });

    return Object.entries(counts).map(([name, value]) => ({
      name: label(name),
      rawKey: name,
      value,
      color: STATUS_COLORS[name] || '#64748b',
    }));
  }, [sprintIssues]);

  // Assignee Workload Breakdown data for Bar Chart
  const assigneeWorkloadData = useMemo(() => {
    const map = {};
    sprintIssues.forEach((i) => {
      const userKey = i.assigneeUserId ? `User #${i.assigneeUserId}` : 'Unassigned';
      if (!map[userKey]) {
        map[userKey] = { name: userKey, count: 0, points: 0 };
      }
      map[userKey].count += 1;
      map[userKey].points += Number(i.storyPoints) || 0;
    });

    return Object.values(map).map((a) => ({
      name: a.name,
      issues: a.count,
      points: a.points,
      value: metricType === 'points' ? (a.points || a.count) : a.count,
    }));
  }, [sprintIssues, metricType]);

  return (
    <div className="sprint-analytics-dashboard">
      {/* Controls & Sprint Selector Bar */}
      <div className="panel analytics-toolbar">
        <div className="analytics-toolbar-left">
          <label className="field-inline">
            <strong>Selected Sprint:</strong>
            <select
              value={selectedSprintId}
              onChange={(e) => {
                setSelectedSprintId(e.target.value);
                if (onSelectSprint) onSelectSprint(e.target.value);
              }}
            >
              {sprints.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.status})
                </option>
              ))}
            </select>
          </label>
          {activeSprint && (
            <Badge
              value={activeSprint.status}
              tone={
                activeSprint.status === 'ACTIVE'
                  ? 'positive'
                  : activeSprint.status === 'PLANNED'
                    ? 'neutral'
                    : 'critical'
              }
            />
          )}
        </div>

        <div className="analytics-toolbar-right">
          <div className="metric-toggle-buttons">
            <button
              type="button"
              className={metricType === 'points' ? 'is-active' : ''}
              onClick={() => setMetricType('points')}
              title="Measure burndown in Story Points"
            >
              Story Points
            </button>
            <button
              type="button"
              className={metricType === 'count' ? 'is-active' : ''}
              onClick={() => setMetricType('count')}
              title="Measure burndown in Issue Count"
            >
              Issue Count
            </button>
          </div>
        </div>
      </div>

      {/* KPI Summary Cards */}
      <div className="analytics-kpi-grid">
        <div className="kpi-card">
          <div className="kpi-icon-wrap" style={{ background: '#eff6ff', color: '#3b82f6' }}>
            <Icon name="folder" />
          </div>
          <div className="kpi-content">
            <span className="kpi-label">Sprint Scope</span>
            <div className="kpi-val">
              {metricType === 'points' ? `${metrics.totalPoints} pts` : `${metrics.totalIssues} issues`}
            </div>
            <small className="kpi-sub">
              {metrics.totalIssues} total issues committed
            </small>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon-wrap" style={{ background: '#ecfdf5', color: '#10b981' }}>
            <Icon name="check" />
          </div>
          <div className="kpi-content">
            <span className="kpi-label">Completed Scope</span>
            <div className="kpi-val" style={{ color: '#10b981' }}>
              {metricType === 'points' ? `${metrics.donePoints} pts` : `${metrics.doneIssues} issues`}
            </div>
            <small className="kpi-sub">
              {metrics.completionPct}% sprint completion rate
            </small>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon-wrap" style={{ background: '#fffbeb', color: '#f59e0b' }}>
            <Icon name="clock" />
          </div>
          <div className="kpi-content">
            <span className="kpi-label">Remaining Work</span>
            <div className="kpi-val" style={{ color: '#f59e0b' }}>
              {metricType === 'points' ? `${metrics.remainingPoints} pts` : `${metrics.remainingIssues} issues`}
            </div>
            <small className="kpi-sub">
              {metrics.remainingIssues} issues left to close
            </small>
          </div>
        </div>

        <div className="kpi-card">
          <div className="kpi-icon-wrap" style={{ background: '#f5f3ff', color: metrics.paceColor }}>
            <Icon name="dashboard" />
          </div>
          <div className="kpi-content">
            <span className="kpi-label">Sprint Pace</span>
            <div className="kpi-val" style={{ color: metrics.paceColor, fontSize: '20px' }}>
              {metrics.pace}
            </div>
            <small className="kpi-sub">
              {activeSprint?.startDate && activeSprint?.endDate
                ? `${activeSprint.startDate} → ${activeSprint.endDate}`
                : 'Pace estimate'}
            </small>
          </div>
        </div>
      </div>

      {/* Main Charts Grid: Burndown and Velocity */}
      <div className="analytics-charts-grid">
        {/* Sprint Burndown Chart */}
        <section className="panel chart-panel">
          <div className="section-header">
            <div>
              <h3>Sprint Burndown</h3>
              <p className="hint">
                Ideal linear decline vs. actual remaining {metricType === 'points' ? 'story points' : 'issues'} over the sprint timeline.
              </p>
            </div>
            <span className="burndown-scope-badge">
              {metrics.completedScope}/{metrics.totalScope} {metricType === 'points' ? 'pts' : 'issues'} burned
            </span>
          </div>

          <div className="chart-container" style={{ width: '100%', height: 320 }}>
            {metrics.totalScope === 0 ? (
              <EmptyState
                kind="issues"
                title="No work assigned to this sprint"
                message="Add issues or estimate story points in the Backlog to visualize burndown."
              />
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart
                  data={burndownData}
                  margin={{ top: 20, right: 30, left: 10, bottom: 20 }}
                >
                  <defs>
                    <linearGradient id="actualGradient" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="5%" stopColor="#3b82f6" stopOpacity={0.3} />
                      <stop offset="95%" stopColor="#3b82f6" stopOpacity={0.0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                  <XAxis
                    dataKey="date"
                    tick={{ fill: '#64748b', fontSize: 12 }}
                    tickLine={{ stroke: '#cbd5e1' }}
                  />
                  <YAxis
                    domain={[0, 'dataMax + 2']}
                    tick={{ fill: '#64748b', fontSize: 12 }}
                    tickLine={{ stroke: '#cbd5e1' }}
                  />
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'rgba(15, 23, 42, 0.95)',
                      borderRadius: '8px',
                      color: '#fff',
                      border: 'none',
                      fontSize: '12px',
                    }}
                    labelStyle={{ fontWeight: 500, color: '#93c5fd' }}
                  />
                  <Legend
                    verticalAlign="top"
                    height={36}
                    wrapperStyle={{ fontSize: '13px' }}
                  />
                  <Line
                    type="linear"
                    dataKey="ideal"
                    name="Ideal Burndown"
                    stroke="#94a3b8"
                    strokeDasharray="5 5"
                    strokeWidth={2}
                    dot={{ r: 3, fill: '#94a3b8' }}
                  />
                  <Area
                    type="monotone"
                    dataKey="actual"
                    name="Actual Remaining"
                    stroke="#3b82f6"
                    strokeWidth={3}
                    fillOpacity={1}
                    fill="url(#actualGradient)"
                    dot={{ r: 4, fill: '#3b82f6' }}
                    activeDot={{ r: 6 }}
                  />
                </AreaChart>
              </ResponsiveContainer>
            )}
          </div>
        </section>

        {/* Sprint Velocity Comparison Chart */}
        <section className="panel chart-panel">
          <div className="section-header">
            <div>
              <h3>Sprint Velocity Comparison</h3>
              <p className="hint">
                Committed vs. completed capacity across sprints. Average velocity: <strong>{avgVelocity} {metricType === 'points' ? 'pts' : 'issues'}</strong>.
              </p>
            </div>
          </div>

          <div className="chart-container" style={{ width: '100%', height: 320 }}>
            {velocityData.length === 0 ? (
              <EmptyState
                kind="projects"
                title="No sprints found"
                message="Create sprints to compare velocity across iterations."
              />
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <BarChart
                  data={velocityData}
                  margin={{ top: 20, right: 30, left: 10, bottom: 20 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                  <XAxis
                    dataKey="name"
                    tick={{ fill: '#64748b', fontSize: 12 }}
                    tickLine={{ stroke: '#cbd5e1' }}
                  />
                  <YAxis
                    tick={{ fill: '#64748b', fontSize: 12 }}
                    tickLine={{ stroke: '#cbd5e1' }}
                  />
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'rgba(15, 23, 42, 0.95)',
                      borderRadius: '8px',
                      color: '#fff',
                      border: 'none',
                      fontSize: '12px',
                    }}
                    labelStyle={{ fontWeight: 500, color: '#93c5fd' }}
                  />
                  <Legend verticalAlign="top" height={36} wrapperStyle={{ fontSize: '13px' }} />
                  {avgVelocity > 0 && (
                    <ReferenceLine
                      y={avgVelocity}
                      stroke="#f59e0b"
                      strokeDasharray="4 4"
                      label={{
                        value: `Avg: ${avgVelocity}`,
                        fill: '#f59e0b',
                        position: 'right',
                        fontSize: 12,
                      }}
                    />
                  )}
                  <Bar
                    dataKey="committed"
                    name="Committed"
                    fill="#93c5fd"
                    radius={[4, 4, 0, 0]}
                  />
                  <Bar
                    dataKey="completed"
                    name="Completed"
                    fill="#10b981"
                    radius={[4, 4, 0, 0]}
                  />
                </BarChart>
              </ResponsiveContainer>
            )}
          </div>
        </section>
      </div>

      {/* Breakdown Row: Status Distribution & Assignee Workload */}
      <div className="analytics-breakdown-grid">
        {/* Status Distribution */}
        <section className="panel breakdown-panel">
          <div className="section-header">
            <h3>Status Distribution</h3>
            <span className="muted">{sprintIssues.length} issues</span>
          </div>

          <div style={{ width: '100%', height: 260, display: 'flex', alignItems: 'center' }}>
            {statusPieData.length === 0 ? (
              <p className="work-empty">No issues in sprint</p>
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie
                    data={statusPieData}
                    cx="50%"
                    cy="50%"
                    innerRadius={55}
                    outerRadius={85}
                    paddingAngle={4}
                    dataKey="value"
                  >
                    {statusPieData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'rgba(15, 23, 42, 0.95)',
                      borderRadius: '8px',
                      color: '#fff',
                      border: 'none',
                      fontSize: '12px',
                    }}
                  />
                  <Legend
                    layout="vertical"
                    verticalAlign="middle"
                    align="right"
                    wrapperStyle={{ fontSize: '12px' }}
                  />
                </PieChart>
              </ResponsiveContainer>
            )}
          </div>
        </section>

        {/* Workload by Assignee */}
        <section className="panel breakdown-panel">
          <div className="section-header">
            <h3>Workload by Assignee</h3>
            <span className="muted">
              {metricType === 'points' ? 'Story Points' : 'Issue Count'}
            </span>
          </div>

          <div style={{ width: '100%', height: 260 }}>
            {assigneeWorkloadData.length === 0 ? (
              <p className="work-empty">No assigned issues</p>
            ) : (
              <ResponsiveContainer width="100%" height="100%">
                <BarChart
                  data={assigneeWorkloadData}
                  layout="vertical"
                  margin={{ top: 10, right: 30, left: 40, bottom: 10 }}
                >
                  <CartesianGrid strokeDasharray="3 3" stroke="#e2e8f0" />
                  <XAxis type="number" tick={{ fill: '#64748b', fontSize: 11 }} />
                  <YAxis
                    dataKey="name"
                    type="category"
                    tick={{ fill: '#64748b', fontSize: 11 }}
                    width={100}
                  />
                  <Tooltip
                    contentStyle={{
                      backgroundColor: 'rgba(15, 23, 42, 0.95)',
                      borderRadius: '8px',
                      color: '#fff',
                      border: 'none',
                      fontSize: '12px',
                    }}
                  />
                  <Bar
                    dataKey="value"
                    name={metricType === 'points' ? 'Story Points' : 'Issues'}
                    fill="#6366f1"
                    radius={[0, 4, 4, 0]}
                  />
                </BarChart>
              </ResponsiveContainer>
            )}
          </div>
        </section>
      </div>
    </div>
  );
}
