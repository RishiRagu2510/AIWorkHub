import { Icon } from '../../components/Icon';
import { Field } from '../../components/common';
import { useProjectAssigneesQuery } from '../../services/api/api';
import { useAuth } from '../auth/context';
import DeveloperPicker from '../projects/DeveloperPicker';
import { assignmentError } from './assignment';

export default function IssueAssignee({ value, onChange, disabled, projectIds = [], multiple = true }) {
  const { session, can } = useAuth();
  const lookup = '';
  const directory = useProjectAssigneesQuery(
    {
      projectIds,
      selfId: session.user.id,
      extraId: lookup,
      includeProfiles: session.user.role === 'ADMIN' && can('USER_VIEW'),
    },
    { skip: !projectIds.length, refetchOnMountOrArgChange: true },
  );
  const people = (directory.currentData || []).map((person) =>
    String(person.id) === String(session.user.id)
      ? {
          ...person,
          displayName: session.user.name || person.displayName,
          email: session.user.email || person.email,
        }
      : person,
  );

  const selectedList = Array.isArray(value) ? value : value ? [value] : [];
  const primaryAssignee = selectedList[0] || null;

  const handlePickerChange = (result) => {
    if (multiple) {
      const selectedIds = Array.isArray(result) ? result : result ? [result] : [];
      const updatedList = selectedIds
        .map((id) => people.find((p) => String(p.id) === String(id)) || selectedList.find((p) => String(p.id) === String(id)))
        .filter(Boolean);
      onChange(updatedList);
    } else {
      onChange(people.find((person) => String(person.id) === String(result)) || null);
    }
  };

  const removeAssignee = (idToRemove) => {
    if (multiple) {
      onChange(selectedList.filter((p) => String(p.id) !== String(idToRemove)));
    } else {
      onChange(null);
    }
  };

  const available = selectedList.every((p) => people.some((person) => String(person.id) === String(p.id)));
  const options = people;
  const error = assignmentError(directory.error);

  return (
    <div className="issue-assignee">
      <div className="form-grid">
        <div className="field">
          <label htmlFor="issue-assignee">Assignee</label>
          <DeveloperPicker
            id="issue-assignee"
            multiple={multiple}
            noOptionsText="No eligible users match this search."
            developers={options}
            value={multiple ? selectedList.map((p) => String(p.id)) : (primaryAssignee?.id || '')}
            onChange={handlePickerChange}
            disabled={disabled || !projectIds.length || directory.isFetching || Boolean(error)}
          />
        </div>
        <Field
          label={multiple && selectedList.length > 1 ? 'Primary assignee email' : 'Assignee email'}
          type="email"
          value={primaryAssignee?.email || ''}
          readOnly
          placeholder="Available directory email"
        />
      </div>

      {selectedList.length > 0 && (
        <div className="assignee-selections-list" style={{ display: 'grid', gap: '8px' }}>
          {selectedList.map((person) => (
            <div key={person.id} className="assignee-selection">
              <span className="assignee-avatar" aria-hidden="true">
                <Icon name="user" />
              </span>
              <div className="assignee-selection-info">
                <span className="assignee-selection-label">
                  {selectedList.length > 1 && String(person.id) === String(primaryAssignee?.id)
                    ? 'Primary assignee'
                    : 'Selected assignee'}
                </span>
                <strong>{person.displayName || `User #${person.id}`}</strong>
                <span className="hint">Assignee ID: {person.id}</span>
              </div>
              <button
                className="assignee-clear"
                type="button"
                disabled={disabled}
                onClick={() => removeAssignee(person.id)}
              >
                Clear{selectedList.length > 1 ? '' : ' assignee'}
              </button>
            </div>
          ))}
        </div>
      )}
      {selectedList.length > 0 && !available && !directory.isFetching && !error && (
        <p role="alert">
          Some selections are no longer in the eligible list. Clear them or choose eligible assignees.
        </p>
      )}
      {directory.isFetching && <p role="status">Checking project access and active accounts…</p>}
      {error && (
        <div role="alert">
          <p>{error.message}</p>
          <button type="button" disabled={disabled} onClick={directory.refetch}>
            Retry assignees
          </button>
        </div>
      )}
      {projectIds.length > 0 && !directory.isFetching && !error && !people.length && (
        <p>No eligible members found for these projects.</p>
      )}
      {/* {projectIds.length > 0 && (
        <details className="assignee-lookup">
          <summary>Find another user by ID</summary>
          <p className="hint">
            Use this for administrators with implicit access or a known project member. Profile
            details may be unavailable.
          </p>
          <div className="assignee-lookup-controls">
            <Field
              label="User ID to check"
              type="number"
              min="1"
              step="1"
              value={extraId}
              onChange={(event) => setExtraId(event.target.value)}
            />
            <button
              type="button"
              disabled={
                disabled ||
                directory.isFetching ||
                !/^[1-9]\d*$/.test(extraId) ||
                !Number.isSafeInteger(Number(extraId))
              }
              onClick={() => setLookup(extraId)}
            >
              Check user access
            </button>
          </div>
          {lookup &&
            !directory.isFetching &&
            !error &&
            !people.some((person) => String(person.id) === lookup) && (
              <p role="status">That user is not eligible for all selected projects.</p>
            )}
        </details>
      )} */}
    </div>
  );
}
