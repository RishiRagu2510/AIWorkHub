import { useState, useRef, useEffect, useMemo } from 'react';
import {
  useDeleteEmployeeSetupMutation,
  useEmployeeSetupUsersQuery,
  useAssignEmployeeUserRolesMutation,
  useBulkAssignEmployeeUserRolesMutation
} from '../../../src/services/api/employees';
import { Field, QueryState, ErrorState, Pagination } from '../../../src/components/common';
import DeleteConfirmation from '../components/DeleteConfirmation';
import { getRoleBadgeStyle } from './PermissionsCatalog';

const avatarPalette = [
  { bg: '#fef3c7', text: '#b45309' }, // amber
  { bg: '#e0e7ff', text: '#3730a3' }, // indigo
  { bg: '#d1fae5', text: '#065f46' }, // emerald
  { bg: '#e0f2fe', text: '#0369a1' }, // sky
  { bg: '#fce7f3', text: '#9d174d' }, // pink
  { bg: '#f3e8ff', text: '#6b21a8' }, // purple
  { bg: '#ffedd5', text: '#c2410c' }, // orange
  { bg: '#ccfbf1', text: '#0f766e' }  // teal
];

function getAvatarStyle(str) {
  let hash = 0;
  const name = str || 'User';
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  const index = Math.abs(hash) % avatarPalette.length;
  return avatarPalette[index];
}

function getRoleBadgeClass(name) {
  const lower = (name || '').toLowerCase();
  if (lower.includes('admin')) return 'role-chip-admin';
  if (lower.includes('resource') || lower.includes('hr')) return 'role-chip-hr';
  if (lower.includes('dev') || lower.includes('engineer')) return 'role-chip-dev';
  if (lower.includes('manager') || lower.includes('lead')) return 'role-chip-lead';
  return 'role-chip-default';
}

export function UserRoleFilterDropdown({
  roles = [],
  totalCount = 0,
  selectedRoleFilter = 'ALL',
  onSelectRoleFilter
}) {
  const [isOpen, setIsOpen] = useState(false);
  const [searchQuery, setSearchQuery] = useState('');
  const dropdownRef = useRef(null);
  const searchInputRef = useRef(null);

  useEffect(() => {
    function handleClickOutside(event) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsOpen(false);
      }
    }
    function handleKeyDown(event) {
      if (event.key === 'Escape') {
        setIsOpen(false);
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
      document.addEventListener('keydown', handleKeyDown);
      return () => {
        document.removeEventListener('mousedown', handleClickOutside);
        document.removeEventListener('keydown', handleKeyDown);
      };
    }
  }, [isOpen]);

  useEffect(() => {
    if (isOpen) {
      setSearchQuery('');
      setTimeout(() => searchInputRef.current?.focus(), 50);
    }
  }, [isOpen]);

  const selectedRole = useMemo(() => {
    if (selectedRoleFilter === 'ALL' || selectedRoleFilter === 'UNASSIGNED') return null;
    return roles.find(r => String(r.id) === String(selectedRoleFilter));
  }, [roles, selectedRoleFilter]);

  const activeLabel = useMemo(() => {
    if (selectedRoleFilter === 'UNASSIGNED') return 'Unassigned (No Roles)';
    if (selectedRole) return selectedRole.name;
    return 'All Role Assignments';
  }, [selectedRoleFilter, selectedRole]);

  const filteredRoles = useMemo(() => {
    if (!searchQuery.trim()) return roles;
    const q = searchQuery.toLowerCase();
    return roles.filter(
      r => r.name.toLowerCase().includes(q) || r.code.toLowerCase().includes(q)
    );
  }, [roles, searchQuery]);

  return (
    <div className="custom-role-dropdown" ref={dropdownRef}>
      <button
        type="button"
        className={`dropdown-trigger-btn ${isOpen ? 'is-open' : ''} ${selectedRoleFilter !== 'ALL' ? 'is-filtered' : ''}`}
        onClick={() => setIsOpen(prev => !prev)}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-label="Filter users by assigned role"
      >
        <span className="trigger-icon" aria-hidden="true">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3" />
          </svg>
        </span>
        <span className="trigger-label">
          {selectedRoleFilter !== 'ALL' ? `Role: ${activeLabel}` : 'All Role Assignments'}
        </span>
        {selectedRole && (
          <span
            className="trigger-role-chip"
            style={{
              background: getRoleBadgeStyle(selectedRole.code).bg,
              color: getRoleBadgeStyle(selectedRole.code).color,
              borderColor: getRoleBadgeStyle(selectedRole.code).border
            }}
          >
            {selectedRole.code}
          </span>
        )}
        <svg
          className={`chevron-icon ${isOpen ? 'open' : ''}`}
          width="13"
          height="13"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2.25"
          aria-hidden="true"
        >
          <polyline points="6 9 12 15 18 9" />
        </svg>
      </button>

      {isOpen && (
        <div className="dropdown-menu-card" role="listbox">
          {/* Quick Search */}
          <div className="dropdown-search-wrap">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
              <circle cx="11" cy="11" r="8" />
              <path d="M21 21l-4.35-4.35" />
            </svg>
            <input
              ref={searchInputRef}
              type="text"
              placeholder="Filter roles..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              className="dropdown-search-input"
            />
            {searchQuery && (
              <button
                type="button"
                className="dropdown-clear-btn"
                onClick={() => setSearchQuery('')}
                aria-label="Clear role search"
              >
                ✕
              </button>
            )}
          </div>

          <div className="dropdown-menu-scrollable">
            {/* Standard Options */}
            {!searchQuery && (
              <>
                <button
                  type="button"
                  className={`dropdown-item ${selectedRoleFilter === 'ALL' ? 'selected' : ''}`}
                  role="option"
                  aria-selected={selectedRoleFilter === 'ALL'}
                  onClick={() => {
                    onSelectRoleFilter('ALL');
                    setIsOpen(false);
                  }}
                >
                  <div className="item-left">
                    <span className="item-dot all-dot" />
                    <span className="item-text">All Role Assignments</span>
                  </div>
                  <div className="item-right">
                    {totalCount > 0 && <span className="item-count-badge">{totalCount}</span>}
                    {selectedRoleFilter === 'ALL' && (
                      <svg className="check-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                    )}
                  </div>
                </button>

                <button
                  type="button"
                  className={`dropdown-item ${selectedRoleFilter === 'UNASSIGNED' ? 'selected' : ''}`}
                  role="option"
                  aria-selected={selectedRoleFilter === 'UNASSIGNED'}
                  onClick={() => {
                    onSelectRoleFilter('UNASSIGNED');
                    setIsOpen(false);
                  }}
                >
                  <div className="item-left">
                    <span className="item-dot unassigned-dot" />
                    <span className="item-text">Unassigned (No Roles)</span>
                  </div>
                  <div className="item-right">
                    {selectedRoleFilter === 'UNASSIGNED' && (
                      <svg className="check-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                    )}
                  </div>
                </button>

                <div className="dropdown-divider" />
                <div className="dropdown-section-header">Organization Roles</div>
              </>
            )}

            {/* Filtered Roles List */}
            {filteredRoles.map(r => {
              const isSelected = String(selectedRoleFilter) === String(r.id);
              const bStyle = getRoleBadgeStyle(r.code);

              return (
                <button
                  key={r.id}
                  type="button"
                  className={`dropdown-item role-item ${isSelected ? 'selected' : ''}`}
                  role="option"
                  aria-selected={isSelected}
                  onClick={() => {
                    onSelectRoleFilter(String(r.id));
                    setIsOpen(false);
                  }}
                >
                  <div className="item-left">
                    <span className="item-dot" style={{ background: bStyle.color }} />
                    <span className="role-name">{r.name}</span>
                  </div>
                  <div className="item-right">
                    <span
                      className="role-code-badge"
                      style={{
                        background: bStyle.bg,
                        color: bStyle.color,
                        borderColor: bStyle.border
                      }}
                    >
                      {r.code}
                    </span>
                    {isSelected && (
                      <svg className="check-icon" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" aria-hidden="true">
                        <polyline points="20 6 9 17 4 12" />
                      </svg>
                    )}
                  </div>
                </button>
              );
            })}

            {filteredRoles.length === 0 && (
              <div className="dropdown-empty-roles">
                No roles matching "{searchQuery}"
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default function UserRoles({ roles }) {
  const [filters, setFilters] = useState({ search: '', page: 1 });
  const [selectedRoleFilter, setSelectedRoleFilter] = useState('ALL');

  const queryParams = useMemo(() => {
    const p = { search: filters.search.trim() || undefined, page: filters.page };
    if (selectedRoleFilter === 'UNASSIGNED') {
      p.roleId = 0;
    } else if (selectedRoleFilter !== 'ALL') {
      p.roleId = Number(selectedRoleFilter);
    }
    return p;
  }, [filters.search, filters.page, selectedRoleFilter]);

  const query = useEmployeeSetupUsersQuery(queryParams);

  const handleSelectRoleFilter = (filterKey) => {
    setSelectedRoleFilter(filterKey);
    setFilters(prev => ({ ...prev, page: 1 }));
  };
  const [save, result] = useAssignEmployeeUserRolesMutation();
  const [, bulkResult] = useBulkAssignEmployeeUserRolesMutation();
  const [editingUsers, setEditingUsers] = useState([]);
  const [roleIds, setRoleIds] = useState([]);
  const [notice, setNotice] = useState('');
  const [confirmAll, setConfirmAll] = useState(false);
  const [remove, deletion] = useDeleteEmployeeSetupMutation();
  const editFormRef = useRef(null);
  const bulkFormRef = useRef(null);
  const headerCheckboxRef = useRef(null);

  // Bulk selection state
  const [selectedUserIds, setSelectedUserIds] = useState([]);
  const [bulkMode, setBulkMode] = useState(null); // 'ADD' | 'REPLACE' | 'REMOVE'
  const [bulkRoleIds, setBulkRoleIds] = useState([]);
  const [bulkBusy, setBulkBusy] = useState(false);
  const [bulkError, setBulkError] = useState(null);
  const [confirmModal, setConfirmModal] = useState(null);

  // Keep a map of known users across fetches so selection data is never lost
  const allKnownUsersMap = useRef(new Map());
  useEffect(() => {
    if (query.data?.items) {
      query.data.items.forEach(u => allKnownUsersMap.current.set(u.id, u));
    }
  }, [query.data?.items]);

  const currentPageUsers = query.data?.items || [];
  const currentPageUserIds = currentPageUsers.map(u => u.id);
  const isAllPageSelected = currentPageUserIds.length > 0 && currentPageUserIds.every(id => selectedUserIds.includes(id));
  const isPartiallySelected = currentPageUserIds.some(id => selectedUserIds.includes(id)) && !isAllPageSelected;

  useEffect(() => {
    if (headerCheckboxRef.current) {
      headerCheckboxRef.current.indeterminate = isPartiallySelected;
    }
  }, [isPartiallySelected]);

  useEffect(() => {
    if (editingUsers.length > 0 && editFormRef.current) {
      editFormRef.current?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
    }
  }, [editingUsers]);

  useEffect(() => {
    if (bulkMode && bulkFormRef.current) {
      bulkFormRef.current?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
    }
  }, [bulkMode]);

  const toggleSelectAll = () => {
    if (isAllPageSelected) {
      const remaining = selectedUserIds.filter(id => !currentPageUserIds.includes(id));
      setSelectedUserIds(remaining);
      if (editingUsers.length > 0) {
        const remainingUsers = remaining.map(id => allKnownUsersMap.current.get(id)).filter(Boolean);
        if (remainingUsers.length > 0) {
          startEditingUsers(remainingUsers);
        } else {
          setEditingUsers([]);
        }
      }
    } else {
      const next = Array.from(new Set([...selectedUserIds, ...currentPageUserIds]));
      setSelectedUserIds(next);
      if (editingUsers.length > 0) {
        const nextUsers = next.map(id => allKnownUsersMap.current.get(id)).filter(Boolean);
        startEditingUsers(nextUsers);
      }
    }
  };

  const toggleSelectUser = (id) => {
    setSelectedUserIds(prev => {
      const next = prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id];
      // If the editor is already open, keep editingUsers in sync
      if (editingUsers.length > 0) {
        const nextUsers = next.map(uid => allKnownUsersMap.current.get(uid)).filter(Boolean);
        if (nextUsers.length > 0) {
          startEditingUsers(nextUsers);
        } else {
          setEditingUsers([]);
        }
      }
      return next;
    });
  };

  const startEditingUsers = (users) => {
    if (!users || !users.length) return;
    setEditingUsers(users);
    setBulkMode(null);
    // Union all current roles of the selected users so any held role is represented
    const unionRoleIds = Array.from(new Set(users.flatMap(u => u.roleIds || [])));
    setRoleIds(unionRoleIds);
    setNotice('');
    result.reset();
    setTimeout(() => {
      editFormRef.current?.scrollIntoView?.({ behavior: 'smooth', block: 'start' });
    }, 50);
  };

  const openBulkAction = (mode) => {
    setEditingUsers([]);
    setBulkMode(mode);
    setBulkRoleIds([]);
    setBulkError(null);
  };

  const selectedUsersList = useMemo(() => {
    return selectedUserIds.map(id => allKnownUsersMap.current.get(id)).filter(Boolean);
  }, [selectedUserIds, query.data?.items]);

  return (
    <section>
      {/* Toolbar */}
      <div className="setup-toolbar">
        <div className="toolbar-search-wrap">
          <Field label="Search users by name or email">
            <div className="search-input-container">
              <svg className="search-icon" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="11" cy="11" r="8" />
                <path d="M21 21l-4.35-4.35" />
              </svg>
              <input
                value={filters.search}
                placeholder="Search by user name or email address..."
                onChange={e => setFilters({ search: e.target.value, page: 1 })}
              />
              {filters.search && (
                <button
                  type="button"
                  className="clear-search-btn"
                  onClick={() => setFilters({ search: '', page: 1 })}
                  aria-label="Clear search"
                >
                  ✕
                </button>
              )}
            </div>
          </Field>
        </div>

        <div className="toolbar-buttons">
          <button
            className="btn-delete-all"
            disabled={!query.data?.totalRoleAssignments || deletion.isLoading}
            onClick={() => {
              deletion.reset();
              setConfirmAll(true);
            }}
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <polyline points="3 6 5 6 21 6" />
              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
            </svg>
            Delete all user roles
          </button>
        </div>
      </div>

      {confirmAll && (
        <DeleteConfirmation
          name="all user-role assignments"
          confirmLabel="Delete all"
          busy={deletion.isLoading}
          error={deletion.error}
          description={`This removes non-admin role assignments across all users in this organization (${
            query.data?.totalRoleAssignments || 0
          } total assignments), including other pages and search results. Administrator assignments and user accounts are kept. Other setup tabs are unchanged. Affected users lose portal access after their sessions refresh.`}
          onCancel={() => setConfirmAll(false)}
          onConfirm={async () => {
            try {
              const summary = await remove({ kind: 'users' }).unwrap();
              setNotice(`${summary.deleted} role assignments removed; ${summary.skipped} admin assignments kept.`);
              setSelected(null);
              setSelectedUserIds([]);
              setConfirmAll(false);
            } catch {
              /* Keep errors in the popup. */
            }
          }}
        />
      )}

      {notice && (
        <p role="status" className="setup-notice-banner">
          <svg className="notice-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
            <polyline points="22 4 12 14.01 9 11.01" />
          </svg>
          <span>{notice}</span>
        </p>
      )}

      {/* Bulk Action Bar (when rows are selected) */}
      {selectedUserIds.length > 0 && (
        <div className="setup-bulk-bar">
          <div className="bulk-bar-left">
            <span className="bulk-count-badge">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <polyline points="20 6 9 17 4 12" />
              </svg>
              {selectedUserIds.length}
            </span>
            <span className="bulk-bar-label">
              {selectedUserIds.length === 1 ? '1 user selected' : `${selectedUserIds.length} users selected`}
            </span>
          </div>
          <div className="bulk-bar-actions">
            <button
              type="button"
              className="bulk-btn bulk-btn-primary"
              onClick={() => startEditingUsers(selectedUsersList)}
              disabled={result.isLoading || bulkBusy}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
              </svg>
              Edit roles for {selectedUserIds.length} {selectedUserIds.length === 1 ? 'member' : 'members'}
            </button>
            <button
              type="button"
              className="bulk-btn bulk-btn-secondary"
              onClick={() => openBulkAction('ADD')}
              disabled={bulkResult.isLoading || bulkBusy}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <line x1="12" y1="5" x2="12" y2="19" />
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              Add roles
            </button>
            <button
              type="button"
              className="bulk-btn bulk-btn-secondary"
              onClick={() => openBulkAction('REMOVE')}
              disabled={bulkResult.isLoading || bulkBusy}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="5" y1="12" x2="19" y2="12" />
              </svg>
              Remove roles
            </button>
            <button
              type="button"
              className="bulk-btn bulk-btn-danger"
              disabled={bulkBusy}
              onClick={() => {
                setConfirmModal({
                  title: `Remove all roles from ${selectedUserIds.length} users?`,
                  description: `Remove all role assignments from ${selectedUserIds.length} selected users? Administrator roles are safely retained.`,
                  confirmLabel: 'Clear all roles',
                  variant: 'warning',
                  onConfirm: async () => {
                    setBulkBusy(true);
                    try {
                      const usersMap = new Map((query.data?.items || []).map(u => [u.id, u]));
                      let updated = 0;
                      for (const userId of selectedUserIds) {
                        const user = usersMap.get(userId);
                        if (user) {
                          await save({
                            userId,
                            roleIds: [],
                            expectedRoleIds: user.roleIds
                          }).unwrap();
                          updated++;
                        }
                      }
                      setNotice(`Successfully cleared role assignments for ${updated} users.`);
                      setSelectedUserIds([]);
                      setBulkMode(null);
                      setConfirmModal(null);
                    } catch (err) {
                      setNotice(err?.message || 'Failed to clear roles for one or more users.');
                    } finally {
                      setBulkBusy(false);
                    }
                  }
                });
              }}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <polyline points="3 6 5 6 21 6" />
                <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
              </svg>
              Clear all roles
            </button>
            <button
              type="button"
              className="bulk-btn bulk-btn-ghost"
              onClick={() => {
                setSelectedUserIds([]);
                setBulkMode(null);
              }}
            >
              Deselect all
            </button>
          </div>
        </div>
      )}

      {/* Bulk Action Form Drawer/Card */}
      {bulkMode && selectedUserIds.length > 0 && (
        <form
          ref={bulkFormRef}
          style={{ scrollMarginTop: '6rem' }}
          className="panel setup-form"
          onSubmit={async e => {
            e.preventDefault();
            if (bulkRoleIds.length === 0 && bulkMode !== 'REPLACE') {
              alert('Please select at least one role to proceed.');
              return;
            }
            setBulkBusy(true);
            setBulkError(null);
            try {
              const usersMap = new Map((query.data?.items || []).map(u => [u.id, u]));
              let count = 0;
              for (const userId of selectedUserIds) {
                const user = usersMap.get(userId);
                const existing = user ? user.roleIds : [];
                let nextRoles = [];
                if (bulkMode === 'ADD') {
                  nextRoles = Array.from(new Set([...existing, ...bulkRoleIds]));
                } else if (bulkMode === 'REMOVE') {
                  nextRoles = existing.filter(id => !bulkRoleIds.includes(id));
                } else {
                  nextRoles = [...bulkRoleIds];
                }
                await save({
                  userId,
                  roleIds: nextRoles,
                  expectedRoleIds: user ? user.roleIds : null
                }).unwrap();
                count++;
              }
              const actionLabel =
                bulkMode === 'ADD'
                  ? 'added to'
                  : bulkMode === 'REMOVE'
                  ? 'removed from'
                  : 'updated for';
              setNotice(`Roles successfully ${actionLabel} ${count} selected users.`);
              setSelectedUserIds([]);
              setBulkMode(null);
            } catch (err) {
              setBulkError(err);
            } finally {
              setBulkBusy(false);
            }
          }}
        >
          <div className="setup-form-header">
            <div className="setup-form-title-wrap">
              <div className="setup-form-icon" style={{ background: '#fef3c7', color: '#b45309' }}>
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                  <circle cx="9" cy="7" r="4" />
                  <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                  <path d="M16 3.13a4 4 0 0 1 0 7.75" />
                </svg>
              </div>
              <div>
                <h2>
                  {bulkMode === 'ADD' && `Add roles to ${selectedUserIds.length} users`}
                  {bulkMode === 'REPLACE' && `Set / Replace roles for ${selectedUserIds.length} users`}
                  {bulkMode === 'REMOVE' && `Remove roles from ${selectedUserIds.length} users`}
                </h2>
                <p className="setup-form-subtitle">
                  {bulkMode === 'ADD' && 'Selected roles will be added to the users’ existing roles.'}
                  {bulkMode === 'REPLACE' && 'The exact selected roles will replace all current roles for these users.'}
                  {bulkMode === 'REMOVE' && 'Selected roles will be removed from these users.'}
                </p>
              </div>
            </div>
            <button
              type="button"
              className="setup-form-close-btn"
              onClick={() => setBulkMode(null)}
              aria-label="Close form"
              title="Close"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>

          <fieldset style={{ border: 'none', padding: 0, margin: '16px 0 0 0' }}>
            <div className="setup-role-fieldset-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px' }}>
              <legend style={{ fontWeight: 600, fontSize: '13.5px', color: '#334155' }}>
                {bulkMode === 'REMOVE' ? 'Select roles to remove' : 'Select roles to assign'}
              </legend>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <button
                  type="button"
                  className="link-btn-xs"
                  onClick={() => setBulkRoleIds(roles.map(r => r.id))}
                  disabled={bulkBusy || bulkRoleIds.length === roles.length}
                >
                  Select All
                </button>
                <span style={{ color: '#cbd5e1' }}>|</span>
                <button
                  type="button"
                  className="link-btn-xs"
                  onClick={() => setBulkRoleIds([])}
                  disabled={bulkBusy || bulkRoleIds.length === 0}
                >
                  Clear All
                </button>
              </div>
            </div>

            <div className="setup-role-choices">
              {roles.map(role => {
                const isChecked = bulkRoleIds.includes(role.id);
                return (
                  <label key={role.id} className={`setup-role-card ${isChecked ? 'is-checked' : ''}`}>
                    <input
                      type="checkbox"
                      checked={isChecked}
                      disabled={bulkBusy}
                      onChange={e =>
                        setBulkRoleIds(ids =>
                          e.target.checked ? [...ids, role.id] : ids.filter(id => id !== role.id)
                        )
                      }
                    />
                    <div className="role-card-info">
                      <span className="role-card-name">{role.name} ({role.code})</span>
                    </div>
                  </label>
                );
              })}
            </div>
          </fieldset>

          <div className="actions" style={{ marginTop: '20px' }}>
            <button className="primary" disabled={bulkBusy}>
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <polyline points="20 6 9 17 4 12" />
              </svg>
              {bulkBusy
                ? 'Applying roles…'
                : bulkMode === 'ADD'
                ? `Add selected roles to ${selectedUserIds.length} users`
                : bulkMode === 'REPLACE'
                ? `Apply roles to ${selectedUserIds.length} users`
                : `Remove selected roles from ${selectedUserIds.length} users`}
            </button>
            <button
              type="button"
              className="button-cancel"
              disabled={bulkBusy}
              onClick={() => setBulkMode(null)}
            >
              Cancel
            </button>
          </div>
          {bulkError && <ErrorState error={bulkError} />}
        </form>
      )}

      {/* Edit User Roles Form (Single or Multi-user) */}
      {editingUsers.length > 0 && (
        <form
          ref={editFormRef}
          style={{ scrollMarginTop: '6rem' }}
          className="panel setup-form"
          onSubmit={async e => {
            e.preventDefault();
            const isSingle = editingUsers.length === 1;
            const singleUser = isSingle ? editingUsers[0] : null;

            if (!roleIds.length) {
              const confirmMsg = isSingle
                ? `Remove all roles from ${singleUser.displayName}? They will lose portal access after their session refreshes.`
                : `Remove all roles from ${editingUsers.length} selected members? They will lose portal access after their sessions refresh.`;
              setConfirmModal({
                title: isSingle
                  ? `Remove all roles from ${singleUser.displayName}?`
                  : `Remove all roles from ${editingUsers.length} members?`,
                description: confirmMsg,
                confirmLabel: 'Remove all roles',
                variant: 'warning',
                onConfirm: async () => {
                  try {
                    setBulkBusy(true);
                    for (const user of editingUsers) {
                      await save({ userId: user.id, roleIds: [], expectedRoleIds: user.roleIds }).unwrap();
                    }
                    setNotice(
                      isSingle
                        ? `Roles updated for ${singleUser.displayName}. Sign in again to load the new permissions.`
                        : `Roles cleared for ${editingUsers.length} members.`
                    );
                    setEditingUsers([]);
                    setSelectedUserIds([]);
                    setConfirmModal(null);
                  } catch (err) {
                    setNotice(err?.message || 'Failed to update roles.');
                  } finally {
                    setBulkBusy(false);
                  }
                }
              });
              return;
            }

            try {
              setBulkBusy(true);
              for (const user of editingUsers) {
                await save({ userId: user.id, roleIds: [...roleIds], expectedRoleIds: user.roleIds }).unwrap();
              }
              setNotice(
                isSingle
                  ? `Roles updated for ${singleUser.displayName}. Sign in again to load the new permissions.`
                  : `Roles updated for ${editingUsers.length} members: ${editingUsers.map(u => u.displayName).join(', ')}.`
              );
              setEditingUsers([]);
              setSelectedUserIds([]);
            } catch (err) {
              setNotice(err?.message || 'Failed to update roles for one or more users.');
            } finally {
              setBulkBusy(false);
            }
          }}
        >
          <div className="setup-form-header">
            <div className="setup-form-title-wrap">
              <div className="setup-form-icon">
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                  <circle cx="12" cy="7" r="4" />
                </svg>
              </div>
              <div>
                <h2>
                  {editingUsers.length === 1
                    ? `Edit roles for ${editingUsers[0].displayName}`
                    : `Edit roles for ${editingUsers.length} selected members`}
                </h2>
                {editingUsers.length === 1 ? (
                  <p className="setup-form-subtitle">
                    {editingUsers[0].email} &bull; {roleIds.length} role{roleIds.length === 1 ? '' : 's'} assigned
                  </p>
                ) : (
                  <p className="setup-form-subtitle">
                    Updating roles for {editingUsers.length} members &bull; Roles chosen below will be saved across all selected members.
                  </p>
                )}
              </div>
            </div>
            <button
              type="button"
              className="setup-form-close-btn"
              onClick={() => {
                setEditingUsers([]);
                setBulkBusy(false);
              }}
              aria-label="Close form"
              title="Close"
            >
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <line x1="18" y1="6" x2="6" y2="18" />
                <line x1="6" y1="6" x2="18" y2="18" />
              </svg>
            </button>
          </div>

          {editingUsers.length > 1 && (
            <div className="setup-form-selected-members">
              {editingUsers.map(u => {
                const avatarStyle = getAvatarStyle(u.displayName || u.email);
                const assignedRoles = u.roleIds
                  .map(id => roles.find(role => role.id === id)?.name)
                  .filter(Boolean);
                return (
                  <div key={u.id} className="selected-member-chip">
                    <span
                      className="member-avatar"
                      style={{ backgroundColor: avatarStyle.bg, color: avatarStyle.text }}
                    >
                      {(u.displayName || u.email || 'U')[0].toUpperCase()}
                    </span>
                    <strong className="member-name">{u.displayName}</strong>
                    <span className="member-email">{u.email}</span>
                    <div className="member-roles">
                      {assignedRoles.length > 0 ? (
                        assignedRoles.map(r => (
                          <span key={r} className={`role-chip ${getRoleBadgeClass(r)}`}>
                            {r}
                          </span>
                        ))
                      ) : (
                        <span className="role-chip role-chip-empty">No roles</span>
                      )}
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          <fieldset style={{ border: 'none', padding: 0, margin: '16px 0 0 0' }}>
            <div className="setup-role-fieldset-header" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '12px' }}>
              <legend style={{ fontWeight: 600, fontSize: '13.5px', color: '#334155' }}>
                Select user roles
              </legend>
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                <button
                  type="button"
                  className="link-btn-xs"
                  onClick={() => setRoleIds(roles.map(r => r.id))}
                  disabled={result.isLoading || bulkBusy || roleIds.length === roles.length}
                >
                  Select All
                </button>
                <span style={{ color: '#cbd5e1' }}>|</span>
                <button
                  type="button"
                  className="link-btn-xs"
                  onClick={() => setRoleIds([])}
                  disabled={result.isLoading || bulkBusy || roleIds.length === 0}
                >
                  Clear All
                </button>
              </div>
            </div>

            <div className="setup-role-choices">
              {roles.map(role => {
                const isChecked = roleIds.includes(role.id);
                const assignedCount = editingUsers.length > 1
                  ? editingUsers.filter(u => u.roleIds.includes(role.id)).length
                  : 0;

                return (
                  <label key={role.id} className={`setup-role-card ${isChecked ? 'is-checked' : ''}`}>
                    <input
                      type="checkbox"
                      aria-label={`${role.name} (${role.code})`}
                      checked={isChecked}
                      disabled={result.isLoading || bulkBusy}
                      onChange={e =>
                        setRoleIds(ids =>
                          e.target.checked ? [...ids, role.id] : ids.filter(id => id !== role.id)
                        )
                      }
                    />
                    <div className="role-card-info">
                      <span className="role-card-name">{role.name} ({role.code})</span>
                      {editingUsers.length > 1 && assignedCount > 0 && (
                        <span className="role-card-assigned-badge">
                          {assignedCount === editingUsers.length
                            ? `Assigned to all (${assignedCount}/${editingUsers.length})`
                            : `Currently assigned to ${assignedCount} of ${editingUsers.length}`}
                        </span>
                      )}
                    </div>
                  </label>
                );
              })}
            </div>
          </fieldset>

          <p className="form-tip">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" strokeWidth="2" style={{ flexShrink: 0 }}>
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="16" x2="12" y2="12" />
              <line x1="12" y1="8" x2="12.01" y2="8" />
            </svg>
            <span>
              Select roles to assign and uncheck roles to remove. Uncheck all roles to remove all assignments. Users without roles cannot access the portals.
            </span>
          </p>

          <div className="actions">
            <button className="primary" disabled={result.isLoading || bulkBusy}>
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                <polyline points="17 21 17 13 7 13 7 21" />
                <polyline points="7 3 7 8 15 8" />
              </svg>
              {bulkBusy ? 'Saving roles…' : 'Save roles'}
            </button>
            <button
              type="button"
              className="button-cancel"
              disabled={result.isLoading || bulkBusy}
              onClick={() => {
                setEditingUsers([]);
                setBulkBusy(false);
              }}
            >
              Cancel
            </button>
          </div>
          {result.error && <ErrorState error={result.error} />}
        </form>
      )}

      {/* User Roles Table Card */}
      <QueryState query={query}>
        <div className="setup-table-card">
          <div className="table-header-info">
            <div className="table-title">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" strokeWidth="2">
                <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                <circle cx="9" cy="7" r="4" />
                <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                <path d="M16 3.13a4 4 0 0 1 0 7.75" />
              </svg>
              <span>Organization Users &amp; Role Assignments</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
              <UserRoleFilterDropdown
                roles={roles}
                totalCount={query.data?.totalCount}
                selectedRoleFilter={selectedRoleFilter}
                onSelectRoleFilter={handleSelectRoleFilter}
              />
              {selectedRoleFilter !== 'ALL' && (
                <button
                  type="button"
                  className="link-btn-xs"
                  onClick={() => handleSelectRoleFilter('ALL')}
                  title="Reset role filter to all users"
                  style={{ color: '#b45309', fontWeight: 600, fontSize: '12.5px', textDecoration: 'underline' }}
                >
                  Clear role filter
                </button>
              )}
              <div className="table-count-badge">
                Total: {query.data?.totalCount ?? query.data?.items?.length ?? 0} users
              </div>
            </div>
          </div>

          <div className="setup-table">
            <table>
              <thead>
                <tr>
                  <th className="table-checkbox-col">
                    <input
                      type="checkbox"
                      ref={headerCheckboxRef}
                      checked={isAllPageSelected}
                      onChange={toggleSelectAll}
                      aria-label="Select all users on current page"
                      title="Select all on this page"
                    />
                  </th>
                  <th>User</th>
                  <th>Email</th>
                  <th>Current roles</th>
                  <th style={{ textAlign: 'right' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {query.data?.items.map(user => {
                  const assignedRoles = user.roleIds
                    .map(id => roles.find(role => role.id === id)?.name)
                    .filter(Boolean);

                  const avatarStyle = getAvatarStyle(user.displayName || user.email);

                  const isEditingThisUser = editingUsers.some(u => u.id === user.id);
                  const isChecked = selectedUserIds.includes(user.id);

                  return (
                    <tr
                      key={user.id}
                      className={`${isEditingThisUser ? 'is-selected is-editing' : ''} ${isChecked ? 'is-row-checked' : ''}`}
                    >
                      <td className="table-checkbox-col">
                        <input
                          type="checkbox"
                          checked={isChecked}
                          onChange={() => toggleSelectUser(user.id)}
                          aria-label={`Select ${user.displayName}`}
                        />
                      </td>
                      <td>
                        <div className="setup-user-cell">
                          <div
                            className="user-avatar"
                            style={{
                              backgroundColor: avatarStyle.bg,
                              color: avatarStyle.text
                            }}
                          >
                            {(user.displayName || user.email || 'U')[0].toUpperCase()}
                          </div>
                          <span className="user-name">{user.displayName}</span>
                        </div>
                      </td>
                      <td style={{ color: '#475569', fontSize: '13.5px' }}>{user.email}</td>
                      <td>
                        {assignedRoles.length > 0 ? (
                          <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px' }}>
                            {assignedRoles.map(r => (
                              <span key={r} className={`role-chip ${getRoleBadgeClass(r)}`}>
                                {r}
                              </span>
                            ))}
                          </div>
                        ) : (
                          <span className="role-chip role-chip-empty">
                            No roles assigned
                          </span>
                        )}
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <div className="setup-row-actions" style={{ justifyContent: 'flex-end' }}>
                          <button
                            className={`setup-action-btn edit-btn ${isEditingThisUser ? 'active' : ''}`}
                            aria-label={`Edit roles for ${user.displayName}`}
                            onClick={() => {
                              if (selectedUserIds.length > 1 && selectedUserIds.includes(user.id)) {
                                // If multiple members are selected and this is one of them, edit all selected members
                                startEditingUsers(selectedUsersList);
                              } else {
                                // Edit this specific user
                                setSelectedUserIds([user.id]);
                                startEditingUsers([user]);
                              }
                            }}
                          >
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                              <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                              <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                            </svg>
                            Edit roles
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {query.data?.items?.length === 0 && (
              <div className="setup-empty-state">
                <svg className="empty-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                  <circle cx="11" cy="11" r="8" />
                  <path d="M21 21l-4.35-4.35" />
                </svg>
                <h4>No users found</h4>
                <p>
                  {filters.search
                    ? `No users matched "${filters.search}".`
                    : selectedRoleFilter !== 'ALL'
                    ? 'No users match the selected role filter.'
                    : 'No active users found in this organization.'}
                </p>
                {(filters.search || selectedRoleFilter !== 'ALL') && (
                  <button
                    type="button"
                    className="btn-clear-filter"
                    onClick={() => {
                      setFilters({ search: '', page: 1 });
                      setSelectedRoleFilter('ALL');
                    }}
                  >
                    Clear filters
                  </button>
                )}
              </div>
            )}
            <Pagination data={query.data} onPage={page => setFilters(f => ({ ...f, page }))} busy={query.isFetching} />
          </div>
        </div>
      </QueryState>

      {confirmModal && (
        <DeleteConfirmation
          title={confirmModal.title}
          description={confirmModal.description}
          confirmLabel={confirmModal.confirmLabel}
          variant={confirmModal.variant || 'warning'}
          busy={bulkBusy || result.isLoading}
          error={confirmModal.error || result.error}
          onCancel={() => {
            setConfirmModal(null);
            result.reset();
          }}
          onConfirm={confirmModal.onConfirm}
        />
      )}
    </section>
  );
}
