import { useState, useMemo, useRef, useEffect } from 'react';
import { Field } from '../../../src/components/common';
import DeleteConfirmation from '../components/DeleteConfirmation';
import { useDeleteEmployeeSetupMutation, useSaveEmployeeSetupMutation } from '../../../src/services/api/employees';

export const PERMISSION_CATEGORIES = [
  {
    id: 'ai_automation',
    label: 'AI & Automations',
    prefixes: ['AI_', 'BUG_FIX_', 'RAG_', 'AUTOMATION_', 'POLICY_'],
    description: 'AI model operations, autonomous bug fixing, RAG knowledge, and safety guardrails',
    color: '#d97706',
    bgColor: '#fef3c7'
  },
  {
    id: 'projects_issues',
    label: 'Projects & Issues',
    prefixes: ['PROJECT_', 'ISSUE_'],
    description: 'Project governance, task workflows, transitions, assignments, and discussions',
    color: '#2563eb',
    bgColor: '#dbeafe'
  },
  {
    id: 'timesheets_worklogs',
    label: 'Timesheets & Worklogs',
    prefixes: ['WORKLOG_', 'TIMESHEET_'],
    description: 'Work logging, weekly timesheet submissions, team time reviews, and approvals',
    color: '#059669',
    bgColor: '#d1fae5'
  },
  {
    id: 'employees_org',
    label: 'Employees & Organization',
    prefixes: ['EMPLOYEE_'],
    description: 'Employee profiles, sensitive personnel details, and organizational structures',
    color: '#7c3aed',
    bgColor: '#ede9fe'
  },
  {
    id: 'users_teams',
    label: 'Users & Teams',
    prefixes: ['USER_', 'TEAM_'],
    description: 'User access accounts, team squad configurations, and memberships',
    color: '#0891b2',
    bgColor: '#cffafe'
  },
  {
    id: 'code_prs',
    label: 'Code & Pull Requests',
    prefixes: ['GITHUB_', 'REPOSITORY_', 'PR_'],
    description: 'GitHub repositories, code commits, branches, and PR reviews',
    color: '#475569',
    bgColor: '#f1f5f9'
  },
  {
    id: 'documentation',
    label: 'Documentation & Knowledge',
    prefixes: ['DOCUMENT_'],
    description: 'Knowledge base, wikis, standard operating procedures, and documentation',
    color: '#d97706',
    bgColor: '#fef3c7'
  },
  {
    id: 'service_incident',
    label: 'Service & Incidents',
    prefixes: ['SERVICE_REQUEST_', 'INCIDENT_', 'CHANGE_'],
    description: 'ITSM service requests, incident resolution workflows, and change management',
    color: '#dc2626',
    bgColor: '#fee2e2'
  },
  {
    id: 'governance_audit',
    label: 'Governance & Security',
    prefixes: ['TENANT_', 'ROLE_', 'PERMISSION_', 'AUDIT_'],
    description: 'Tenant configuration, RBAC roles, permission definitions, and audit logs',
    color: '#4f46e5',
    bgColor: '#e0e7ff'
  }
];

export function getCategoryForCode(code = '') {
  return PERMISSION_CATEGORIES.find(c => c.prefixes.some(p => code.startsWith(p))) || {
    id: 'other',
    label: 'Other',
    description: 'System permissions',
    color: '#64748b',
    bgColor: '#f1f5f9'
  };
}

export function getRoleBadgeStyle(code = '') {
  const c = (code || '').toUpperCase();
  if (c.includes('ADMIN')) return { bg: '#f5f3ff', color: '#6d28d9', border: '#ddd6fe' };
  if (c.includes('DEV') || c.includes('TECH')) return { bg: '#eff6ff', color: '#1d4ed8', border: '#bfdbfe' };
  if (c.includes('HR') || c.includes('HUMAN')) return { bg: '#ecfdf5', color: '#047857', border: '#a7f3d0' };
  if (c.includes('ENG') || c.includes('LEAD')) return { bg: '#f0fdf4', color: '#15803d', border: '#bbf7d0' };
  if (c.includes('MAN') || c.includes('MGR')) return { bg: '#fffbeb', color: '#b45309', border: '#fde68a' };
  return { bg: '#f1f5f9', color: '#334155', border: '#cbd5e1' };
}

export default function PermissionsCatalog({
  permissions = [],
  roles = [],
  onSaveRole,
  isSaving = false,
  onDeletePermission,
  deletion,
  onSavePermission
}) {
  const [internalRemove, internalDeletion] = useDeleteEmployeeSetupMutation();
  const [internalSave, internalSaveState] = useSaveEmployeeSetupMutation();
  const removeFn = onDeletePermission || internalRemove;
  const savePermFn = onSavePermission || internalSave;
  const isDeleting = deletion?.isLoading || internalDeletion.isLoading;
  const deleteErr = deletion?.error || internalDeletion.error;
  const isSavingPerm = isSaving || internalSaveState.isLoading;

  const [search, setSearch] = useState('');
  const [selectedCategory, setSelectedCategory] = useState('ALL');
  const [selectedRoleFilter, setSelectedRoleFilter] = useState('ALL');
  const [viewMode, setViewMode] = useState('table'); // 'table' | 'matrix'
  const [assigningPerm, setAssigningPerm] = useState(null);
  const [editingPerm, setEditingPerm] = useState(null);
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [deleteError, setDeleteError] = useState(null);
  const [notice, setNotice] = useState('');
  const [errorMsg, setErrorMsg] = useState('');

  // Matrix pending edits: roleId -> Set of permissionIds
  const [matrixEdits, setMatrixEdits] = useState(null);

  // Initialize role permission mapping
  const rolePermMap = useMemo(() => {
    const map = {};
    for (const r of roles) {
      map[r.id] = new Set(r.permissionIds || []);
    }
    return map;
  }, [roles]);

  const activeMatrix = matrixEdits || rolePermMap;

  const isMatrixDirty = useMemo(() => {
    if (!matrixEdits) return false;
    for (const r of roles) {
      const orig = rolePermMap[r.id] || new Set();
      const curr = matrixEdits[r.id] || new Set();
      if (orig.size !== curr.size) return true;
      for (const id of orig) {
        if (!curr.has(id)) return true;
      }
    }
    return false;
  }, [matrixEdits, rolePermMap, roles]);

  // Map each permission to the roles that currently hold it
  const rolesByPermId = useMemo(() => {
    const map = {};
    for (const p of permissions) {
      map[p.id] = [];
    }
    for (const r of roles) {
      for (const pId of r.permissionIds || []) {
        if (map[pId]) {
          map[pId].push(r);
        }
      }
    }
    return map;
  }, [permissions, roles]);

  const permCountByRoleId = useMemo(() => {
    const map = {};
    for (const r of roles) {
      map[r.id] = (r.permissionIds || []).length;
    }
    return map;
  }, [roles]);

  const unassignedCount = useMemo(() => {
    return permissions.filter(p => !rolesByPermId[p.id] || rolesByPermId[p.id].length === 0).length;
  }, [permissions, rolesByPermId]);

  // Filtered permissions
  const filteredPermissions = useMemo(() => {
    const term = search.trim().toLowerCase();
    return permissions.filter(p => {
      // Category filter
      if (selectedCategory !== 'ALL') {
        const cat = getCategoryForCode(p.code);
        if (cat.id !== selectedCategory) return false;
      }

      // Role filter
      if (selectedRoleFilter !== 'ALL') {
        const assignedRoles = rolesByPermId[p.id] || [];
        if (selectedRoleFilter === 'UNASSIGNED') {
          if (assignedRoles.length > 0) return false;
        } else {
          const roleId = Number(selectedRoleFilter);
          if (!assignedRoles.some(r => r.id === roleId)) return false;
        }
      }

      // Text search
      if (term) {
        const code = (p.code || '').toLowerCase();
        const name = (p.name || '').toLowerCase();
        const desc = (p.description || '').toLowerCase();
        const cat = getCategoryForCode(p.code).label.toLowerCase();
        const assignedRoleNames = (rolesByPermId[p.id] || []).map(r => r.name.toLowerCase()).join(' ');
        if (!code.includes(term) && !name.includes(term) && !desc.includes(term) && !cat.includes(term) && !assignedRoleNames.includes(term)) {
          return false;
        }
      }

      return true;
    });
  }, [permissions, search, selectedCategory, selectedRoleFilter, rolesByPermId]);

  // Category counts
  const categoryCounts = useMemo(() => {
    const counts = { ALL: permissions.length };
    for (const c of PERMISSION_CATEGORIES) {
      counts[c.id] = 0;
    }
    for (const p of permissions) {
      const cat = getCategoryForCode(p.code);
      if (counts[cat.id] !== undefined) {
        counts[cat.id]++;
      }
    }
    return counts;
  }, [permissions]);

  // Handle single permission role assignment modal save
  const handleSavePermRoles = async (permId, targetRoleIds) => {
    setErrorMsg('');
    setNotice('');
    try {
      const targetSet = new Set(targetRoleIds);
      const updates = [];

      for (const r of roles) {
        const currentIds = r.permissionIds || [];
        const hasIt = currentIds.includes(permId);
        const shouldHave = targetSet.has(r.id);

        if (hasIt !== shouldHave) {
          const nextPermIds = shouldHave
            ? Array.from(new Set([...currentIds, permId]))
            : currentIds.filter(id => id !== permId);

          updates.push(
            onSaveRole({
              kind: 'roles',
              id: r.id,
              body: {
                name: r.name,
                code: r.code,
                description: r.description,
                permissionIds: nextPermIds
              }
            }).unwrap()
          );
        }
      }

      if (updates.length > 0) {
        await Promise.all(updates);
      }

      const pObj = permissions.find(x => x.id === permId);
      setNotice(`Role access updated for ${pObj ? pObj.name : 'permission'}.`);
      setAssigningPerm(null);
    } catch (err) {
      setErrorMsg(err?.data?.message || err?.message || 'Failed to update role assignments.');
    }
  };

  // Toggle cell in matrix mode
  const toggleMatrixCell = (roleId, permId) => {
    const base = matrixEdits || Object.fromEntries(
      Object.entries(rolePermMap).map(([k, set]) => [k, new Set(set)])
    );
    const roleSet = new Set(base[roleId] || []);
    if (roleSet.has(permId)) {
      roleSet.delete(permId);
    } else {
      roleSet.add(permId);
    }
    setMatrixEdits({ ...base, [roleId]: roleSet });
  };

  // Save full matrix edits
  const handleSaveMatrix = async () => {
    if (!matrixEdits) return;
    setErrorMsg('');
    setNotice('');
    try {
      const updates = [];
      for (const r of roles) {
        const orig = rolePermMap[r.id] || new Set();
        const curr = matrixEdits[r.id] || new Set();
        let changed = orig.size !== curr.size;
        if (!changed) {
          for (const id of orig) {
            if (!curr.has(id)) {
              changed = true;
              break;
            }
          }
        }
        if (changed) {
          updates.push(
            onSaveRole({
              kind: 'roles',
              id: r.id,
              body: {
                name: r.name,
                code: r.code,
                description: r.description,
                permissionIds: Array.from(curr)
              }
            }).unwrap()
          );
        }
      }

      if (updates.length > 0) {
        await Promise.all(updates);
        setNotice(`Permission matrix saved successfully for ${updates.length} roles.`);
      }
      setMatrixEdits(null);
    } catch (err) {
      setErrorMsg(err?.data?.message || err?.message || 'Failed to save matrix changes.');
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteTarget) return;
    try {
      setDeleteError(null);
      await removeFn({ kind: 'permissions', id: deleteTarget.id }).unwrap();
      setNotice(`Permission "${deleteTarget.name}" (${deleteTarget.code}) deleted successfully.`);
      setDeleteTarget(null);
    } catch (err) {
      setDeleteError(err);
    }
  };

  const handleSavePermission = async (payload) => {
    try {
      setErrorMsg('');
      const isEdit = Boolean(editingPerm?.id);
      await savePermFn({
        kind: 'permissions',
        id: editingPerm?.id,
        body: payload
      }).unwrap();

      setNotice(
        isEdit
          ? `Permission "${payload.name}" updated successfully.`
          : `Permission "${payload.name}" (${payload.code}) created successfully.`
      );
      setEditingPerm(null);
    } catch (err) {
      throw err;
    }
  };

  return (
    <div className="permissions-catalog-page">
      {/* Notice & Error */}
      {notice && (
        <p role="status" className="setup-notice-banner">
          <svg className="notice-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
            <polyline points="22 4 12 14.01 9 11.01" />
          </svg>
          <span>{notice}</span>
        </p>
      )}

      {errorMsg && (
        <p role="alert" className="setup-error-banner" style={{ background: '#fef2f2', border: '1px solid #fecaca', color: '#991b1b', padding: '10px 14px', borderRadius: '8px', marginBottom: '16px', fontSize: '13.5px' }}>
          {errorMsg}
        </p>
      )}

      {/* Filter Toolbar */}
      <div className="setup-toolbar perm-catalog-toolbar">
        <div className="toolbar-search-wrap" style={{ flex: 1, maxWidth: '440px' }}>
          <Field label="Search permissions">
            <div className="search-input-container">
              <svg className="search-icon" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="11" cy="11" r="8" />
                <path d="M21 21l-4.35-4.35" />
              </svg>
              <input
                value={search}
                placeholder="Filter permissions by code, name, description..."
                onChange={e => setSearch(e.target.value)}
              />
              {search && (
                <button
                  type="button"
                  className="clear-search-btn"
                  onClick={() => setSearch('')}
                  aria-label="Clear search"
                >
                  ✕
                </button>
              )}
            </div>
          </Field>
        </div>

        {/* View Mode Switcher */}
        <div className="perm-view-switcher">
          <button
            type="button"
            className={`view-switch-btn ${viewMode === 'table' ? 'active' : ''}`}
            onClick={() => setViewMode('table')}
            title="Catalog table view"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <line x1="8" y1="6" x2="21" y2="6" />
              <line x1="8" y1="12" x2="21" y2="12" />
              <line x1="8" y1="18" x2="21" y2="18" />
              <line x1="3" y1="6" x2="3.01" y2="6" />
              <line x1="3" y1="12" x2="3.01" y2="12" />
              <line x1="3" y1="18" x2="3.01" y2="18" />
            </svg>
            Catalog view
          </button>
          <button
            type="button"
            className={`view-switch-btn ${viewMode === 'matrix' ? 'active' : ''}`}
            onClick={() => setViewMode('matrix')}
            title="Interactive cross-role RBAC matrix"
          >
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <rect x="3" y="3" width="18" height="18" rx="2" ry="2" />
              <line x1="3" y1="9" x2="21" y2="9" />
              <line x1="3" y1="15" x2="21" y2="15" />
              <line x1="9" y1="3" x2="9" y2="21" />
              <line x1="15" y1="3" x2="15" y2="21" />
            </svg>
            RBAC Matrix view
          </button>
        </div>

        {/* Add Permission Button */}
        <div className="toolbar-buttons" style={{ margin: 0 }}>
          <button
            type="button"
            className="btn-add-primary"
            onClick={() => {
              setEditingPerm({});
              setErrorMsg('');
            }}
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            Add permission
          </button>
        </div>
      </div>

      {/* Category Filter Pills Strip */}
      <div className="perm-category-pills">
        <button
          type="button"
          className={`cat-pill ${selectedCategory === 'ALL' ? 'active' : ''}`}
          onClick={() => setSelectedCategory('ALL')}
        >
          All categories ({categoryCounts.ALL})
        </button>
        {PERMISSION_CATEGORIES.map(cat => (
          <button
            key={cat.id}
            type="button"
            className={`cat-pill ${selectedCategory === cat.id ? 'active' : ''}`}
            onClick={() => setSelectedCategory(cat.id)}
          >
            {cat.label} ({categoryCounts[cat.id] || 0})
          </button>
        ))}
      </div>

      {/* Main Content Card */}
      <div className="setup-table-card">
        <div className="table-header-info">
          <div className="table-title">
            <span>
              {viewMode === 'table' ? 'System Permissions Catalog' : 'Cross-Role RBAC Permission Matrix'}
            </span>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            {viewMode === 'table' && (
              <RoleFilterDropdown
                roles={roles}
                permissions={permissions}
                permCountByRoleId={permCountByRoleId}
                unassignedCount={unassignedCount}
                selectedRoleFilter={selectedRoleFilter}
                onSelectRoleFilter={setSelectedRoleFilter}
              />
            )}
            <div className="table-count-badge">
              Showing {filteredPermissions.length} of {permissions.length} permissions
            </div>
          </div>
        </div>

        {/* 1. TABLE VIEW */}
        {viewMode === 'table' && (
          <div className="setup-table perm-catalog-table">
            <table>
              <thead>
                <tr>
                  <th style={{ width: '48px', textAlign: 'center' }}>#</th>
                  <th style={{ width: '180px' }}>Permission Code</th>
                  <th style={{ width: '190px' }}>Name</th>
                  <th style={{ width: '160px' }}>Category</th>
                  <th>Description</th>
                  <th style={{ minWidth: '220px' }}>Assigned Roles</th>
                  <th style={{ textAlign: 'right', width: '150px' }}>Actions</th>
                </tr>
              </thead>
              <tbody>
                {filteredPermissions.map(perm => {
                  const cat = getCategoryForCode(perm.code);
                  const assigned = rolesByPermId[perm.id] || [];

                  return (
                    <tr key={perm.id}>
                      <td style={{ textAlign: 'center', color: '#94a3b8', fontSize: '12px', fontWeight: 600 }}>
                        {perm.id}
                      </td>
                      <td>
                        <span className="setup-code-badge perm-code-badge">
                          {perm.code}
                        </span>
                      </td>
                      <td>
                        <span style={{ fontWeight: 600, color: '#0f172a', fontSize: '13.5px' }}>
                          {perm.name}
                        </span>
                      </td>
                      <td>
                        <span
                          className="perm-category-tag"
                          style={{
                            background: cat.bgColor,
                            color: cat.color
                          }}
                        >
                          {cat.label}
                        </span>
                      </td>
                      <td style={{ color: '#475569', fontSize: '13px', lineHeight: 1.45 }}>
                        {perm.description || '—'}
                      </td>
                      <td>
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '5px', alignItems: 'center' }}>
                          {assigned.length > 0 ? (
                            assigned.map(r => {
                              const bStyle = getRoleBadgeStyle(r.code);
                              return (
                                <span
                                  key={r.id}
                                  className="perm-assigned-role-chip"
                                  style={{
                                    background: bStyle.bg,
                                    color: bStyle.color,
                                    borderColor: bStyle.border
                                  }}
                                  title={r.name}
                                >
                                  {r.code}
                                </span>
                              );
                            })
                          ) : (
                            <span style={{ color: '#94a3b8', fontSize: '12px', fontStyle: 'italic' }}>
                              Unassigned
                            </span>
                          )}
                        </div>
                      </td>
                      <td style={{ textAlign: 'right' }}>
                        <div className="setup-row-actions" style={{ justifyContent: 'flex-end' }}>
                          <button
                            type="button"
                            className="setup-action-btn edit-btn"
                            aria-label={`Edit ${perm.name}`}
                            onClick={() => {
                              setEditingPerm(perm);
                              setErrorMsg('');
                            }}
                            title={`Edit ${perm.name}`}
                          >
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                              <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                              <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                            </svg>
                            Edit
                          </button>
                          <button
                            type="button"
                            className="setup-action-btn delete-btn"
                            disabled={isDeleting}
                            aria-label={`Delete ${perm.name}`}
                            onClick={() => {
                              setDeleteError(null);
                              deletion?.reset?.();
                              internalDeletion?.reset?.();
                              setDeleteTarget(perm);
                            }}
                            title={`Delete ${perm.name} (${perm.code})`}
                          >
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                              <polyline points="3 6 5 6 21 6" />
                              <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                            </svg>
                            Delete
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>

            {!filteredPermissions.length && (
              <div className="setup-empty-state">
                <h4>No matching permissions found</h4>
                <p>Try adjusting your search query or category filter.</p>
                {(search || selectedCategory !== 'ALL' || selectedRoleFilter !== 'ALL') && (
                  <button
                    type="button"
                    className="btn-clear-filter"
                    onClick={() => {
                      setSearch('');
                      setSelectedCategory('ALL');
                      setSelectedRoleFilter('ALL');
                    }}
                  >
                    Reset all filters
                  </button>
                )}
              </div>
            )}
          </div>
        )}

        {/* 2. MATRIX VIEW */}
        {viewMode === 'matrix' && (
          <div className="setup-table perm-matrix-table-wrap">
            <table>
              <thead>
                <tr>
                  <th style={{ width: '45px', textAlign: 'center' }}>#</th>
                  <th style={{ width: '190px' }}>Permission</th>
                  <th style={{ width: '150px' }}>Category</th>
                  {roles.map(r => {
                    const bStyle = getRoleBadgeStyle(r.code);
                    return (
                      <th key={r.id} style={{ textAlign: 'center', minWidth: '110px' }}>
                        <div style={{ fontWeight: 700, fontSize: '13px', color: '#0f172a' }}>{r.name}</div>
                        <span
                          style={{
                            display: 'inline-block',
                            marginTop: '2px',
                            padding: '1px 6px',
                            fontSize: '10.5px',
                            fontWeight: 700,
                            borderRadius: '4px',
                            background: bStyle.bg,
                            color: bStyle.color,
                            border: `1px solid ${bStyle.border}`
                          }}
                        >
                          {r.code}
                        </span>
                      </th>
                    );
                  })}
                </tr>
              </thead>
              <tbody>
                {filteredPermissions.map(perm => {
                  const cat = getCategoryForCode(perm.code);

                  return (
                    <tr key={perm.id}>
                      <td style={{ textAlign: 'center', color: '#94a3b8', fontSize: '11.5px', fontWeight: 600 }}>
                        {perm.id}
                      </td>
                      <td>
                        <div style={{ fontWeight: 600, fontSize: '13px', color: '#0f172a' }}>
                          {perm.name}
                        </div>
                        <div style={{ fontFamily: 'monospace', fontSize: '10.5px', color: '#64748b' }}>
                          {perm.code}
                        </div>
                      </td>
                      <td>
                        <span
                          className="perm-category-tag"
                          style={{
                            background: cat.bgColor,
                            color: cat.color
                          }}
                        >
                          {cat.label}
                        </span>
                      </td>
                      {roles.map(r => {
                        const checked = (activeMatrix[r.id] || new Set()).has(perm.id);

                        return (
                          <td key={r.id} style={{ textAlign: 'center', verticalAlign: 'middle' }}>
                            <input
                              type="checkbox"
                              className="matrix-cell-checkbox"
                              checked={checked}
                              disabled={isSaving}
                              onChange={() => toggleMatrixCell(r.id, perm.id)}
                              aria-label={`${r.name} has ${perm.name}`}
                            />
                          </td>
                        );
                      })}
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Floating Save Bar for Matrix Mode */}
      {viewMode === 'matrix' && isMatrixDirty && (
        <div className="perm-matrix-save-bar">
          <div className="save-bar-left">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="8" x2="12" y2="12" />
              <line x1="12" y1="16" x2="12.01" y2="16" />
            </svg>
            <span>You have unsaved changes in the Role Permission Matrix.</span>
          </div>
          <div className="save-bar-actions">
            <button
              type="button"
              className="button button-sm button-cancel"
              disabled={isSaving}
              onClick={() => setMatrixEdits(null)}
            >
              Discard changes
            </button>
            <button
              type="button"
              className="button button-sm primary"
              disabled={isSaving}
              onClick={handleSaveMatrix}
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                <polyline points="20 6 9 17 4 12" />
              </svg>
              {isSaving ? 'Saving matrix...' : 'Save matrix changes'}
            </button>
          </div>
        </div>
      )}

      {/* Single Permission Role Assign Modal */}
      {assigningPerm && (
        <RoleAssignModal
          permission={assigningPerm}
          roles={roles}
          busy={isSaving}
          onClose={() => setAssigningPerm(null)}
          onSave={selectedRoleIds => handleSavePermRoles(assigningPerm.id, selectedRoleIds)}
        />
      )}

      {/* Create / Edit Permission Modal */}
      {editingPerm && (
        <PermissionEditModal
          permission={editingPerm}
          roles={roles}
          existingPermissions={permissions}
          busy={isSavingPerm}
          onClose={() => setEditingPerm(null)}
          onSave={handleSavePermission}
        />
      )}

      {/* Delete Permission Confirmation Modal */}
      {deleteTarget && (
        <DeleteConfirmation
          name={`${deleteTarget.name} (${deleteTarget.code})`}
          title={`Delete ${deleteTarget.name}?`}
          confirmLabel="Delete permission"
          description={`This will permanently remove "${deleteTarget.name}" (${deleteTarget.code}) from the system catalog and revoke it from all assigned roles. This action cannot be undone.`}
          busy={isDeleting}
          error={deleteErr || deleteError}
          onCancel={() => {
            setDeleteTarget(null);
            setDeleteError(null);
            deletion?.reset?.();
            internalDeletion?.reset?.();
          }}
          onConfirm={handleDeleteConfirm}
        />
      )}
    </div>
  );
}

function RoleAssignModal({
  permission,
  roles,
  busy,
  onClose,
  onSave
}) {
  const [selectedRoleIds, setSelectedRoleIds] = useState(() => {
    return roles
      .filter(r => (r.permissionIds || []).includes(permission.id))
      .map(r => r.id);
  });

  const cat = getCategoryForCode(permission.code);

  return (
    <div className="perm-modal-overlay" onClick={onClose}>
      <div className="perm-modal-card" onClick={e => e.stopPropagation()}>
        {/* Header */}
        <div className="modal-header">
          <div className="modal-header-text">
            <div className="modal-badge-row">
              <span className="setup-code-badge perm-code-badge">{permission.code}</span>
              <span
                className="perm-category-tag"
                style={{
                  background: cat.bgColor,
                  color: cat.color
                }}
              >
                {cat.label}
              </span>
            </div>
            <h3 className="modal-title">{permission.name}</h3>
            {permission.description && (
              <p className="modal-desc">{permission.description}</p>
            )}
          </div>
          <button
            type="button"
            className="setup-form-close-btn"
            onClick={onClose}
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        {/* Body */}
        <div className="modal-body">
          <div className="modal-subheader">
            <span className="modal-subtitle">
              Select authorized roles ({selectedRoleIds.length} of {roles.length} granted)
            </span>
            <div className="modal-quick-links">
              <button
                type="button"
                className="link-btn-xs"
                onClick={() => setSelectedRoleIds(roles.map(r => r.id))}
                disabled={busy || selectedRoleIds.length === roles.length}
              >
                Select all
              </button>
              <span className="divider">|</span>
              <button
                type="button"
                className="link-btn-xs"
                onClick={() => setSelectedRoleIds([])}
                disabled={busy || selectedRoleIds.length === 0}
              >
                Clear all
              </button>
            </div>
          </div>

          <div className="modal-roles-grid">
            {roles.map(role => {
              const isChecked = selectedRoleIds.includes(role.id);
              const bStyle = getRoleBadgeStyle(role.code);

              return (
                <label
                  key={role.id}
                  className={`modal-role-card ${isChecked ? 'is-checked' : ''}`}
                >
                  <input
                    type="checkbox"
                    className="role-modal-checkbox"
                    checked={isChecked}
                    disabled={busy}
                    onChange={e => {
                      if (e.target.checked) {
                        setSelectedRoleIds(prev => [...prev, role.id]);
                      } else {
                        setSelectedRoleIds(prev => prev.filter(id => id !== role.id));
                      }
                    }}
                  />
                  <div className="role-card-inner">
                    <div className="role-card-row">
                      <span className="role-title">{role.name}</span>
                      <span
                        className="role-code-pill"
                        style={{
                          background: bStyle.bg,
                          color: bStyle.color,
                          borderColor: bStyle.border
                        }}
                      >
                        {role.code}
                      </span>
                    </div>
                    {role.description && (
                      <span className="role-caption">{role.description}</span>
                    )}
                  </div>
                </label>
              );
            })}
          </div>
        </div>

        {/* Actions */}
        <div className="modal-actions">
          <button
            type="button"
            className="button button-cancel"
            disabled={busy}
            onClick={onClose}
          >
            Cancel
          </button>
          <button
            type="button"
            className="primary"
            disabled={busy}
            onClick={() => onSave(selectedRoleIds)}
          >
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" style={{ marginRight: 6 }}>
              <polyline points="20 6 9 17 4 12" />
            </svg>
            {busy ? 'Saving...' : 'Save role assignments'}
          </button>
        </div>
      </div>
    </div>
  );
}

function PermissionEditModal({
  permission,
  roles,
  existingPermissions = [],
  busy,
  onClose,
  onSave
}) {
  const isEdit = Boolean(permission?.id);
  const [code, setCode] = useState(permission?.code || '');
  const [name, setName] = useState(permission?.name || '');
  const [description, setDescription] = useState(permission?.description || '');
  const [selectedRoleIds, setSelectedRoleIds] = useState(() => {
    if (isEdit) {
      return roles
        .filter(r => (r.permissionIds || []).includes(permission.id))
        .map(r => r.id);
    }
    return [];
  });
  const [error, setError] = useState('');

  const detectedCategory = useMemo(() => getCategoryForCode(code.trim().toUpperCase()), [code]);

  const handleSubmit = async e => {
    e.preventDefault();
    setError('');

    const normCode = code.trim().toUpperCase();
    const normName = name.trim();

    if (!normCode) {
      setError('Permission code is required.');
      return;
    }

    if (!/^[A-Z0-9_]+$/.test(normCode)) {
      setError('Permission code must contain only uppercase letters, numbers, and underscores (e.g. INVENTORY_VIEW).');
      return;
    }

    if (!normName) {
      setError('Permission name is required.');
      return;
    }

    if (!isEdit) {
      const duplicate = existingPermissions.some(
        p => p.code?.toUpperCase() === normCode
      );
      if (duplicate) {
        setError(`A permission with code "${normCode}" already exists.`);
        return;
      }
    }

    try {
      await onSave({
        code: normCode,
        name: normName,
        description: description.trim() || null,
        roleIds: selectedRoleIds
      });
    } catch (err) {
      setError(err?.data?.message || err?.message || 'Failed to save permission.');
    }
  };

  return (
    <div className="perm-modal-overlay" onClick={onClose}>
      <div
        className="perm-modal-card"
        style={{ maxWidth: '640px' }}
        onClick={e => e.stopPropagation()}
        role="dialog"
        aria-modal="true"
        aria-labelledby="perm-modal-title"
      >
        {/* Header */}
        <div className="modal-header">
          <div className="modal-header-text">
            <div className="modal-badge-row">
              <span
                className="perm-category-tag"
                style={{
                  background: detectedCategory.bgColor,
                  color: detectedCategory.color
                }}
              >
                {detectedCategory.label}
              </span>
              {isEdit && (
                <span className="setup-code-badge perm-code-badge">
                  {permission.code}
                </span>
              )}
            </div>
            <h3 id="perm-modal-title" className="modal-title">
              {isEdit ? `Edit Permission: ${permission.name}` : 'Add New System Permission'}
            </h3>
            <p className="modal-desc">
              {isEdit
                ? 'Update permission name, documentation description, and role authorizations.'
                : 'Define a new system permission capability and assign it to authorized roles.'}
            </p>
          </div>
          <button
            type="button"
            className="setup-form-close-btn"
            onClick={onClose}
            aria-label="Close dialog"
          >
            ✕
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', flex: 1, minHeight: 0 }}>
          <div className="modal-body" style={{ overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '16px' }}>
            {error && (
              <div role="alert" style={{ background: '#fef2f2', border: '1px solid #fecaca', color: '#991b1b', padding: '10px 14px', borderRadius: '8px', fontSize: '13px' }}>
                {error}
              </div>
            )}

            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '14px' }}>
              <div>
                <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: 'var(--theme-text, #334155)', marginBottom: '6px' }}>
                  Permission Code <span style={{ color: '#dc2626' }}>*</span>
                </label>
                <input
                  type="text"
                  value={code}
                  readOnly={isEdit}
                  placeholder="e.g. INVENTORY_VIEW"
                  aria-label="Permission Code"
                  onChange={e => setCode(e.target.value.toUpperCase())}
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    borderRadius: '7px',
                    border: '1px solid var(--theme-border, #cbd5e1)',
                    fontFamily: 'monospace',
                    fontSize: '13px',
                    fontWeight: 600,
                    background: isEdit ? 'var(--theme-surface-subtle, #f1f5f9)' : 'var(--theme-surface, #ffffff)',
                    color: 'var(--theme-text, #0f172a)',
                    cursor: isEdit ? 'not-allowed' : 'text'
                  }}
                />
                <span style={{ display: 'block', fontSize: '11px', color: '#64748b', marginTop: '4px' }}>
                  {isEdit ? 'Codes cannot be modified once created.' : 'Uppercase alphanumeric with underscores.'}
                </span>
              </div>

              <div>
                <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: 'var(--theme-text, #334155)', marginBottom: '6px' }}>
                  Permission Name <span style={{ color: '#dc2626' }}>*</span>
                </label>
                <input
                  type="text"
                  value={name}
                  placeholder="e.g. View Inventory"
                  aria-label="Permission Name"
                  onChange={e => setName(e.target.value)}
                  style={{
                    width: '100%',
                    padding: '8px 12px',
                    borderRadius: '7px',
                    border: '1px solid var(--theme-border, #cbd5e1)',
                    fontSize: '13px',
                    background: 'var(--theme-surface, #ffffff)',
                    color: 'var(--theme-text, #0f172a)'
                  }}
                />
                <span style={{ display: 'block', fontSize: '11px', color: '#64748b', marginTop: '4px' }}>
                  User-friendly label shown in roles & audit logs.
                </span>
              </div>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '12.5px', fontWeight: 600, color: 'var(--theme-text, #334155)', marginBottom: '6px' }}>
                Description
              </label>
              <textarea
                value={description}
                rows={2}
                placeholder="Explain what access or actions this permission enables..."
                aria-label="Description"
                onChange={e => setDescription(e.target.value)}
                style={{
                  width: '100%',
                  padding: '8px 12px',
                  borderRadius: '7px',
                  border: '1px solid var(--theme-border, #cbd5e1)',
                  fontSize: '13px',
                  resize: 'vertical',
                  background: 'var(--theme-surface, #ffffff)',
                  color: 'var(--theme-text, #0f172a)'
                }}
              />
            </div>

            {/* Role Assignments Section */}
            <div>
              <div className="modal-subheader" style={{ marginBottom: '10px' }}>
                <span className="modal-subtitle">
                  Assign to Roles ({selectedRoleIds.length} of {roles.length} selected)
                </span>
                <div className="modal-quick-links">
                  <button
                    type="button"
                    className="link-btn-xs"
                    onClick={() => setSelectedRoleIds(roles.map(r => r.id))}
                    disabled={busy || selectedRoleIds.length === roles.length}
                  >
                    Select all
                  </button>
                  <span className="divider">|</span>
                  <button
                    type="button"
                    className="link-btn-xs"
                    onClick={() => setSelectedRoleIds([])}
                    disabled={busy || selectedRoleIds.length === 0}
                  >
                    Clear all
                  </button>
                </div>
              </div>

              <div className="modal-roles-grid">
                {roles.map(role => {
                  const isChecked = selectedRoleIds.includes(role.id);
                  const bStyle = getRoleBadgeStyle(role.code);

                  return (
                    <label
                      key={role.id}
                      className={`modal-role-card ${isChecked ? 'is-checked' : ''}`}
                    >
                      <input
                        type="checkbox"
                        className="role-modal-checkbox"
                        checked={isChecked}
                        disabled={busy}
                        onChange={e => {
                          if (e.target.checked) {
                            setSelectedRoleIds(prev => [...prev, role.id]);
                          } else {
                            setSelectedRoleIds(prev => prev.filter(id => id !== role.id));
                          }
                        }}
                      />
                      <div className="role-card-inner">
                        <div className="role-card-row">
                          <span className="role-title">{role.name}</span>
                          <span
                            className="role-code-pill"
                            style={{
                              background: bStyle.bg,
                              color: bStyle.color,
                              borderColor: bStyle.border
                            }}
                          >
                            {role.code}
                          </span>
                        </div>
                        {role.description && (
                          <span className="role-caption">{role.description}</span>
                        )}
                      </div>
                    </label>
                  );
                })}
              </div>
            </div>
          </div>

          {/* Footer Actions */}
          <div className="modal-actions">
            <button
              type="button"
              className="button button-cancel"
              disabled={busy}
              onClick={onClose}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="primary"
              disabled={busy}
            >
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" style={{ marginRight: 6 }}>
                <polyline points="20 6 9 17 4 12" />
              </svg>
              {busy ? 'Saving...' : isEdit ? 'Save permission' : 'Create permission'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}

export function RoleFilterDropdown({
  roles = [],
  permissions = [],
  permCountByRoleId = {},
  unassignedCount = 0,
  selectedRoleFilter,
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
    if (selectedRoleFilter === 'UNASSIGNED') return 'Unassigned Only';
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
        aria-label="Filter permissions by assigned role"
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
                    <span className="item-count-badge">{permissions.length}</span>
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
                    <span className="item-text">Unassigned Permissions</span>
                  </div>
                  <div className="item-right">
                    <span className="item-count-badge">{unassignedCount}</span>
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
              const count = permCountByRoleId[r.id] || 0;

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
                    <span className="item-count-badge" title={`${count} permissions`}>
                      {count}
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
