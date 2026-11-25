import { useState } from 'react';
import { liveQueryOptions } from '../../services/api/live';
import { useAuth } from '../auth/context';
import { useWorkLogsQuery } from '../../services/api/timesheets';
import { QueryState, Pagination } from '../../components/common';
import WorkLogForm from './WorkLogForm';
import WorkLogTable from './WorkLogTable';
import { dateRange, dateText } from './time';
import '../../styles/timesheets.scss';
export default function IssueWorkLogs({ issue }) {
  const { can } = useAuth();
  const [add, setAdd] = useState(false);
  const [page, setPage] = useState(1);
  const [month, setMonth] = useState(dateText(new Date()).slice(0, 7));
  const range = dateRange(month + '-01', 'month');
  const query = useWorkLogsQuery(
    { issueId: issue.id, ...range, page, pageSize: 20 },
    { ...liveQueryOptions, skip: !can('WORKLOG_VIEW') },
  );
  if (!can('WORKLOG_VIEW')) return null;
  return (
    <section className="panel timesheet-workspace">
      <div className="section-header">
        <h2>Work log</h2>
        {can('WORKLOG_CREATE') && (
          <button className="primary" onClick={() => setAdd(true)}>
            Log work
          </button>
        )}
      </div>
      <div className="panel-body">
        <label>
          Work log month
          <input
            type="month"
            value={month}
            onChange={(e) => {
              if (e.target.value) {
                setMonth(e.target.value);
                setPage(1);
              }
            }}
          />
        </label>
        {add && <WorkLogForm issue={issue} onClose={() => setAdd(false)} />}
        <QueryState query={query}>
          <WorkLogTable items={query.data?.items} />
          {query.data?.totalPages > 1 && (
            <Pagination data={query.data} onPage={setPage} busy={query.isFetching} />
          )}
        </QueryState>
      </div>
    </section>
  );
}
