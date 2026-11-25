import { useState } from 'react';
import { useSearchIssuesQuery } from '../../services/api/api';
import { ErrorState } from '../../components/common';
const parentTypes = { STORY: ['EPIC'], TASK: ['STORY'], BUG: ['STORY'], SUBTASK: ['TASK', 'BUG'] };
export default function ParentIssuePicker({ projectId, issueType, issueId, initialId, disabled }) {
  const [text, setText] = useState('');
  const [value, setValue] = useState(initialId || '');
  const [page, setPage] = useState(1);
  const types = parentTypes[issueType] || [];
  const [parentType, setParentType] = useState(types[0]);
  const query = useSearchIssuesQuery(
    { ProjectId: projectId, IssueType: parentType, Text: text, Page: page, PageSize: 20 },
    { skip: !projectId || !types.length || disabled },
  );
  return (
    <div style={{ display: 'none' }}>
      <label>
        Find parent issue
        <input
          value={text}
          disabled={disabled || !types.length}
          onChange={(e) => {
            setText(e.target.value);
            setPage(1);
          }}
          placeholder="Search parent issues"
        />
      </label>
      {types.length > 1 && (
        <label>
          Parent type
          <select
            value={parentType}
            onChange={(e) => {
              setParentType(e.target.value);
              setPage(1);
            }}
          >
            <option>TASK</option>
            <option>BUG</option>
          </select>
        </label>
      )}
      <label>
        Parent issue
        <select
          name="parentIssueId"
          value={value}
          disabled={disabled || !types.length}
          required={issueType === 'SUBTASK' && !disabled}
          onChange={(e) => setValue(e.target.value)}
        >
          <option value="">
            {issueType === 'SUBTASK' ? 'Select a parent task or bug' : 'No parent'}
          </option>
          {value && !query.data?.items?.some((p) => String(p.id) === String(value)) && (
            <option value={value}>Current parent #{value}</option>
          )}
          {query.data?.items
            ?.filter((p) => p.id !== issueId)
            .map((p) => (
              <option key={p.id} value={p.id}>
                {p.issueKey} — {p.summary}
              </option>
            ))}
        </select>
      </label>
      <button
        type="button"
        disabled={page <= 1 || query.isFetching}
        onClick={() => setPage(page - 1)}
      >
        Previous parents
      </button>
      <button
        type="button"
        disabled={page >= (query.data?.totalPages || 1) || query.isFetching}
        onClick={() => setPage(page + 1)}
      >
        More parents
      </button>
      <ErrorState error={query.error} />
      <p>Epic → Story → Task/Bug → Subtask. Parents must belong to this project.</p>
    </div>
  );
}
