import { useState, useMemo } from 'react';
import { Icon } from '../../components/Icon';
import DeveloperPicker from './DeveloperPicker';
import Link from '../../components/PortalLink';
import { useAuth } from '../auth/context';
import {
  useDevelopersQuery,
  useProjectMembersQuery,
  useAddProjectMemberMutation,
  useRemoveProjectMemberMutation,
} from '../../services/api/api';
import { QueryState } from '../../components/common';

function initials(name) {
  if (!name) return '?';
  return name
    .trim()
    .split(/\s+/)
    .slice(0, 2)
    .map((p) => p[0])
    .join('')
    .toUpperCase();
}

function avatarColor(name) {
  const colors = [
    'linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%)',
    'linear-gradient(135deg, #10b981 0%, #047857 100%)',
    'linear-gradient(135deg, #f59e0b 0%, #b45309 100%)',
    'linear-gradient(135deg, #8b5cf6 0%, #6d28d9 100%)',
    'linear-gradient(135deg, #ec4899 0%, #be185d 100%)',
    'linear-gradient(135deg, #06b6d4 0%, #0e7490 100%)',
  ];
  let hash = 0;
  for (let i = 0; i < (name || '').length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  return colors[Math.abs(hash) % colors.length];
}

export default function ProjectMembers({ project, onClose }) {
  const auth = useAuth() || {};
  const session = auth.session;
  const can = auth.can || (() => true);
  const isAdmin = !session || session?.user?.role === 'ADMIN';
  const canManage = !session || (isAdmin && can('PROJECT_MANAGE'));

  const [page] = useState(1);
  const [userId, setUserId] = useState('');
  const [searchQuery, setSearchQuery] = useState('');
  const [error, setError] = useState('');
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [viewMode, setViewMode] = useState('grid'); // 'grid' | 'table'
  const [confirmDeleteId, setConfirmDeleteId] = useState(null);

  const developers = useDevelopersQuery(page, { skip: !canManage });
  const members = useProjectMembersQuery(project.id, { skip: !isAdmin });
  const [add, adding] = useAddProjectMemberMutation();
  const [remove, removing] = useRemoveProjectMemberMutation();

  const busy = adding.isLoading || removing.isLoading;
  const assigned = new Set((members.data || []).map((m) => m.userId));
  const available = (developers.data?.items || []).filter((u) => !assigned.has(u.id));

  // Filter members by search query
  const filteredMembers = useMemo(() => {
    const list = members.data || [];
    if (!searchQuery.trim()) return list;
    const q = searchQuery.toLowerCase().trim();
    return list.filter((m) => {
      const dev = developers.data?.items?.find((u) => u.id === m.userId);
      const name = dev?.displayName || '';
      const email = dev?.email || '';
      return (
        name.toLowerCase().includes(q) ||
        email.toLowerCase().includes(q) ||
        String(m.userId).includes(q)
      );
    });
  }, [members.data, developers.data, searchQuery]);

  async function addMember(event) {
    event.preventDefault();
    if (!userId || busy) return;
    setError('');
    try {
      await add({ projectId: project.id, userId: Number(userId) }).unwrap();
      setUserId('');
      setIsAddOpen(false);
    } catch (e) {
      setError(e.message || 'Could not add this developer to the project.');
    }
  }

  async function removeMember(id) {
    setError('');
    try {
      await remove({ projectId: project.id, userId: id }).unwrap();
      setConfirmDeleteId(null);
    } catch (e) {
      setError(e.message || 'Could not remove this member.');
    }
  }

  const totalMembers = members.data?.length ?? 0;

  return (
    <section className="panel project-team-hub" aria-label={`Team members of ${project.projectName || project.name}`}>
      {/* Header */}
      <div className="section-header">
        <div className="team-hub-title-group">
          <div className="team-hub-icon">
            <Icon name="user" />
          </div>
          <div>
            <div className="team-hub-heading-row">
              <h2>Direct memberships</h2>
              <span className="badge team-count-badge">
                {totalMembers} {totalMembers === 1 ? 'member' : 'members'}
              </span>
            </div>
            <p className="hint">
              Collaborators with direct access to view, report, and work on issues in this project
            </p>
          </div>
        </div>

        <div className="actions team-hub-actions">
          {canManage && (
            <button
              type="button"
              className={isAddOpen ? 'button' : 'primary button'}
              onClick={() => {
                setIsAddOpen(!isAddOpen);
                setError('');
              }}
            >
              <Icon name={isAddOpen ? 'close' : 'plus'} />
              {isAddOpen ? 'Cancel' : 'Add developer'}
            </button>
          )}

          {totalMembers > 0 && (
            <div className="view-mode-toggle" role="group" aria-label="View mode">
              <button
                type="button"
                className={`view-btn ${viewMode === 'grid' ? 'is-active' : ''}`}
                onClick={() => setViewMode('grid')}
                title="Grid view"
                aria-label="Grid view"
              >
                <Icon name="dashboard" />
              </button>
              <button
                type="button"
                className={`view-btn ${viewMode === 'table' ? 'is-active' : ''}`}
                onClick={() => setViewMode('table')}
                title="Table view"
                aria-label="Table view"
              >
                <Icon name="menu" />
              </button>
            </div>
          )}

          {onClose && (
            <button type="button" className="button" onClick={onClose}>
              Close
            </button>
          )}
        </div>
      </div>

      {error && (
        <div role="alert" className="notice is-error" style={{ margin: '14px 20px 0' }}>
          {error}
        </div>
      )}

      {/* Expandable Add Member Drawer */}
      {canManage && isAddOpen && (
        <div className="add-member-drawer">
          <div className="add-member-drawer-inner">
            <div className="drawer-heading">
              <div>
                <h3>Add developer to project</h3>
                <p className="drawer-subtitle">
                  Select a registered developer to grant them direct collaborator permissions on this project.
                </p>
              </div>
              <button
                type="button"
                className="close-drawer-btn"
                onClick={() => setIsAddOpen(false)}
                aria-label="Close add developer form"
              >
                <Icon name="close" />
              </button>
            </div>

            <form className="add-member-form" onSubmit={addMember}>
              <div className="form-field-group">
                <label htmlFor="project-developer" className="field-label">
                  Developer
                </label>
                <DeveloperPicker
                  developers={available}
                  value={userId}
                  onChange={setUserId}
                  disabled={busy || members.isFetching}
                  placeholder="Search available developers by name or email…"
                />
              </div>

              <div className="form-action-group">
                <button
                  className="primary button add-submit-btn"
                  type="submit"
                  disabled={!userId || busy || members.isFetching}
                >
                  <Icon name="plus" />
                  {busy ? 'Adding developer…' : 'Add developer'}
                </button>
                <button
                  type="button"
                  className="button"
                  onClick={() => {
                    setIsAddOpen(false);
                    setUserId('');
                  }}
                >
                  Cancel
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Filter & Search Toolbar */}
      {totalMembers > 0 && (
        <div className="members-filter-bar">
          <div className="members-search-wrapper">
            <span className="search-icon" aria-hidden="true">
              <Icon name="search" />
            </span>
            <input
              type="search"
              placeholder="Search members by name or email…"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="members-search-input"
            />
            {searchQuery && (
              <button
                type="button"
                className="search-clear-btn"
                onClick={() => setSearchQuery('')}
                title="Clear search"
              >
                <Icon name="close" />
              </button>
            )}
          </div>
          <div className="members-stats-indicator">
            Showing <strong>{filteredMembers.length}</strong> of <strong>{totalMembers}</strong> members
          </div>
        </div>
      )}

      {/* Members List / Grid */}
      <QueryState query={members}>
        {totalMembers === 0 ? (
          <div className="members-empty-state">
            <div className="empty-state-icon">
              <Icon name="user" />
            </div>
            <h3>No direct members yet</h3>
            <p>Direct memberships allow specific developers to collaborate and manage issues in this project.</p>
            {canManage && (
              <button
                type="button"
                className="primary button"
                onClick={() => setIsAddOpen(true)}
              >
                <Icon name="plus" />
                Add first developer
              </button>
            )}
          </div>
        ) : filteredMembers.length === 0 ? (
          <div className="members-empty-state">
            <div className="empty-state-icon">
              <Icon name="search" />
            </div>
            <h3>No members match "{searchQuery}"</h3>
            <p>Try refining your search term or clear the search filter.</p>
            <button
              type="button"
              className="button"
              onClick={() => setSearchQuery('')}
            >
              Clear search
            </button>
          </div>
        ) : viewMode === 'table' ? (
          /* Table View */
          <div className="members-table-container">
            <table className="members-table">
              <thead>
                <tr>
                  <th>Collaborator</th>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Assigned Work</th>
                  {canManage && <th style={{ textAlign: 'right' }}>Actions</th>}
                </tr>
              </thead>
              <tbody>
                {filteredMembers.map((m) => {
                  const dev = developers.data?.items?.find((u) => u.id === m.userId);
                  const displayName = dev?.displayName || `User #${m.userId}`;
                  const email = dev?.email || '';
                  const bg = avatarColor(displayName);
                  const isConfirming = confirmDeleteId === m.userId;

                  return (
                    <tr key={m.userId}>
                      <td>
                        <div className="member-table-identity">
                          <div
                            className="member-avatar"
                            style={{ background: bg }}
                            aria-hidden="true"
                          >
                            {initials(displayName)}
                          </div>
                          <span className="member-name">{displayName}</span>
                        </div>
                      </td>
                      <td>
                        <span className="member-email-text">{email || '—'}</span>
                      </td>
                      <td>
                        <span className="member-role-badge">
                          {m.role || 'DEVELOPER'}
                        </span>
                      </td>
                      <td>
                        <Link
                          to={`/issues?ProjectId=${project.id}&AssigneeUserId=${m.userId}`}
                          className="member-issues-pill"
                          title={`View issues assigned to ${displayName}`}
                        >
                          <Icon name="issues" />
                          View issues →
                        </Link>
                      </td>
                      {canManage && (
                        <td style={{ textAlign: 'right' }}>
                          {isConfirming ? (
                            <div className="confirm-remove-group">
                              <span className="confirm-text">Remove?</span>
                              <button
                                type="button"
                                className="danger-button button-xs"
                                disabled={busy}
                                onClick={() => removeMember(m.userId)}
                              >
                                Yes
                              </button>
                              <button
                                type="button"
                                className="button button-xs"
                                onClick={() => setConfirmDeleteId(null)}
                              >
                                No
                              </button>
                            </div>
                          ) : (
                            <button
                              type="button"
                              className="remove-member-btn"
                              disabled={busy}
                              onClick={() => setConfirmDeleteId(m.userId)}
                              title={`Remove ${displayName} from project`}
                            >
                              <Icon name="close" />
                              Remove
                            </button>
                          )}
                        </td>
                      )}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        ) : (
          /* Modern Card Grid View */
          <div className="project-members-card-grid">
            {filteredMembers.map((m) => {
              const dev = developers.data?.items?.find((u) => u.id === m.userId);
              const displayName = dev?.displayName || `User #${m.userId}`;
              const email = dev?.email || '';
              const bg = avatarColor(displayName);
              const isConfirming = confirmDeleteId === m.userId;

              return (
                <div key={m.userId} className="member-card">
                  <div className="member-card-body">
                    <div
                      className="member-card-avatar"
                      style={{ background: bg }}
                      aria-hidden="true"
                    >
                      {initials(displayName)}
                    </div>

                    <div className="member-card-info">
                      <div className="member-card-header-line">
                        <span className="member-card-name" title={displayName}>
                          {displayName}
                        </span>
                        <span className="member-role-badge">
                          {m.role || 'DEVELOPER'}
                        </span>
                      </div>
                      <div className="member-card-email" title={email}>
                        {email || 'No email specified'}
                      </div>
                    </div>
                  </div>

                  <div className="member-card-footer">
                    <Link
                      to={`/issues?ProjectId=${project.id}&AssigneeUserId=${m.userId}`}
                      className="member-issues-pill"
                      title={`View issues assigned to ${displayName}`}
                    >
                      <Icon name="issues" />
                      View issues →
                    </Link>

                    {canManage && (
                      <div className="member-card-actions">
                        {isConfirming ? (
                          <div className="confirm-remove-group">
                            <span className="confirm-text">Remove?</span>
                            <button
                              type="button"
                              className="danger-button button-xs"
                              disabled={busy}
                              onClick={() => removeMember(m.userId)}
                            >
                              Yes
                            </button>
                            <button
                              type="button"
                              className="button button-xs"
                              onClick={() => setConfirmDeleteId(null)}
                            >
                              No
                            </button>
                          </div>
                        ) : (
                          <button
                            type="button"
                            className="remove-member-btn"
                            disabled={busy}
                            onClick={() => setConfirmDeleteId(m.userId)}
                            title={`Remove ${displayName} from project`}
                          >
                            <Icon name="close" />
                            Remove
                          </button>
                        )}
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </QueryState>
    </section>
  );
}
