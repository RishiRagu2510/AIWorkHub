import { useState, useRef, useEffect, useMemo } from 'react';
import { useAuth } from '../../../src/features/auth/context';
import { useDeleteEmployeeSetupMutation, useEmployeeSetupQuery, useSaveEmployeeSetupMutation } from '../../../src/services/api/employees';
import { PageHeader, QueryState, ErrorState, Field } from '../../../src/components/common';
import Link from '../../../src/components/PortalLink';
import '../../../src/styles/employees.scss';
import '../setup.scss';
import UserRoles from './UserRoles';
import PermissionsCatalog from './PermissionsCatalog';
import DeleteConfirmation from '../components/DeleteConfirmation';

const labels = {
  skills: 'Skills',
  departments: 'Departments',
  roles: 'Roles',
  permissions: 'Permissions',
  grades: 'Grades',
  titles: 'Job titles',
  users: 'User roles'
};

const singular = {
  skills: 'skill',
  departments: 'department',
  roles: 'role',
  permissions: 'permission',
  grades: 'grade',
  titles: 'job title'
};

const descriptions = {
  skills: 'Manage the shared skill catalog used by employee profiles.',
  departments: 'Manage organization departments, functional divisions, and business units.',
  roles: 'Manage organization roles, custom groups, and permission definitions.',
  permissions: 'Inspect all 74 system capabilities, AI authorizations, and configure role assignments.',
  grades: 'Define career grade levels, seniority bands, and compensation hierarchy.',
  titles: 'Map job titles to department and grade combinations for employee profiling.',
  users: 'Assign organization roles and portal access permissions to employees.'
};

const PERMISSION_CATEGORIES = [
  {
    id: 'ai_automation',
    label: 'AI & Automations',
    prefixes: ['AI_', 'BUG_FIX_', 'RAG_', 'AUTOMATION_', 'POLICY_'],
    description: 'AI model operations, autonomous bug fixing, RAG knowledge, and safety guardrails'
  },
  {
    id: 'projects_issues',
    label: 'Projects & Issues',
    prefixes: ['PROJECT_', 'ISSUE_'],
    description: 'Project governance, task workflows, transitions, assignments, and discussions'
  },
  {
    id: 'timesheets_worklogs',
    label: 'Timesheets & Worklogs',
    prefixes: ['WORKLOG_', 'TIMESHEET_'],
    description: 'Work logging, weekly timesheet submissions, team time reviews, and approvals'
  },
  {
    id: 'employees_org',
    label: 'Employees & Organization',
    prefixes: ['EMPLOYEE_'],
    description: 'Employee profiles, sensitive personnel details, and organizational structures'
  },
  {
    id: 'users_teams',
    label: 'Users & Teams',
    prefixes: ['USER_', 'TEAM_'],
    description: 'User access accounts, team squad configurations, and memberships'
  },
  {
    id: 'code_prs',
    label: 'Code & Pull Requests',
    prefixes: ['GITHUB_', 'REPOSITORY_', 'PR_'],
    description: 'GitHub repositories, code commits, branches, and PR reviews'
  },
  {
    id: 'documentation',
    label: 'Documentation & Knowledge',
    prefixes: ['DOCUMENT_'],
    description: 'Knowledge base, wikis, standard operating procedures, and documentation'
  },
  {
    id: 'service_incident',
    label: 'Service & Incidents',
    prefixes: ['SERVICE_REQUEST_', 'INCIDENT_', 'CHANGE_'],
    description: 'ITSM service requests, incident resolution workflows, and change management'
  },
  {
    id: 'governance_audit',
    label: 'Governance & Security',
    prefixes: ['TENANT_', 'ROLE_', 'PERMISSION_', 'AUDIT_'],
    description: 'Tenant configuration, RBAC roles, permission definitions, and audit logs'
  }
];

function CategoryIcon({ id }) {
  switch (id) {
    case 'ai_automation':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M12 2v4M12 18v4M4.93 4.93l2.83 2.83M16.24 16.24l2.83 2.83M2 12h4M18 12h4M4.93 19.07l2.83-2.83M16.24 7.76l2.83-2.83" />
        </svg>
      );
    case 'projects_issues':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
          <polyline points="9 14 12 17 15 11" />
        </svg>
      );
    case 'timesheets_worklogs':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="10" />
          <polyline points="12 6 12 12 16 14" />
        </svg>
      );
    case 'employees_org':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
          <path d="M16 3.13a4 4 0 0 1 0 7.75" />
        </svg>
      );
    case 'users_teams':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <line x1="19" y1="8" x2="19" y2="14" />
          <line x1="22" y1="11" x2="16" y2="11" />
        </svg>
      );
    case 'code_prs':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="18" cy="18" r="3" />
          <circle cx="6" cy="6" r="3" />
          <path d="M13 6h3a2 2 0 0 1 2 2v7" />
          <line x1="6" y1="9" x2="6" y2="21" />
        </svg>
      );
    case 'documentation':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <polyline points="14 2 14 8 20 8" />
          <line x1="16" y1="13" x2="8" y2="13" />
          <line x1="16" y1="17" x2="8" y2="17" />
        </svg>
      );
    case 'service_incident':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z" />
          <line x1="12" y1="9" x2="12" y2="13" />
          <line x1="12" y1="17" x2="12.01" y2="17" />
        </svg>
      );
    case 'governance_audit':
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          <path d="M9 12l2 2 4-4" />
        </svg>
      );
    default:
      return (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="10" />
          <line x1="12" y1="8" x2="12" y2="12" />
          <line x1="12" y1="16" x2="12.01" y2="16" />
        </svg>
      );
  }
}

function RolePermissionsMatrix({
  allPermissions = [],
  selectedIds = [],
  onChange,
  disabled = false
}) {
  const [searchTerm, setSearchTerm] = useState('');
  const [collapsedCategories, setCollapsedCategories] = useState({});

  const categorized = useMemo(() => {
    const term = (searchTerm || '').trim().toLowerCase();
    const filtered = allPermissions.filter(p => {
      if (!term) return true;
      return (
        (p.code || '').toLowerCase().includes(term) ||
        (p.name || '').toLowerCase().includes(term) ||
        (p.description || '').toLowerCase().includes(term)
      );
    });

    const groups = PERMISSION_CATEGORIES.map(cat => ({
      ...cat,
      permissions: []
    }));
    const uncategorized = [];

    for (const perm of filtered) {
      const code = perm.code || '';
      const match = groups.find(c => c.prefixes.some(p => code.startsWith(p)));
      if (match) {
        match.permissions.push(perm);
      } else {
        uncategorized.push(perm);
      }
    }

    if (uncategorized.length > 0) {
      groups.push({
        id: 'other',
        label: 'Other System Permissions',
        description: 'Additional system permissions',
        permissions: uncategorized
      });
    }

    return groups.filter(g => g.permissions.length > 0);
  }, [allPermissions, searchTerm]);

  const totalFilteredCount = useMemo(
    () => categorized.reduce((acc, g) => acc + g.permissions.length, 0),
    [categorized]
  );

  const toggleCategory = (catPerms) => {
    if (disabled) return;
    const catIds = catPerms.map(p => p.id);
    const allSelected = catIds.length > 0 && catIds.every(id => selectedIds.includes(id));
    if (allSelected) {
      onChange(selectedIds.filter(id => !catIds.includes(id)));
    } else {
      onChange(Array.from(new Set([...selectedIds, ...catIds])));
    }
  };

  const togglePermission = (id) => {
    if (disabled) return;
    onChange(
      selectedIds.includes(id)
        ? selectedIds.filter(x => x !== id)
        : [...selectedIds, id]
    );
  };

  return (
    <div className="role-permissions-section">
      <div className="permissions-section-header">
        <div className="permissions-header-left">
          <div className="section-icon-badge">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
            </svg>
          </div>
          <div>
            <h3 className="permissions-section-title">Role Permissions &amp; Access Control</h3>
            <p className="permissions-section-desc">
              Select granular permissions authorized for this role. Users assigned this role inherit these capabilities.
            </p>
          </div>
        </div>

        <div className="permissions-header-actions">
          <div className="permissions-selected-counter">
            <span className="counter-num">{selectedIds.length}</span>
            <span className="counter-total">/ {allPermissions.length}</span>
            <span className="counter-label">granted</span>
          </div>
          <div className="quick-action-buttons">
            <button
              type="button"
              className="quick-perm-btn"
              onClick={() => onChange(allPermissions.map(p => p.id))}
              disabled={disabled || selectedIds.length === allPermissions.length || allPermissions.length === 0}
            >
              Grant all
            </button>
            <span className="btn-divider">|</span>
            <button
              type="button"
              className="quick-perm-btn"
              onClick={() => onChange([])}
              disabled={disabled || selectedIds.length === 0}
            >
              Revoke all
            </button>
          </div>
        </div>
      </div>

      <div className="permissions-filter-bar">
        <div className="perm-search-box">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
            <circle cx="11" cy="11" r="8" />
            <path d="M21 21l-4.35-4.35" />
          </svg>
          <input
            type="text"
            value={searchTerm}
            placeholder="Search permissions by code, name, or description..."
            onChange={e => setSearchTerm(e.target.value)}
          />
          {searchTerm && (
            <button
              type="button"
              className="clear-perm-search"
              onClick={() => setSearchTerm('')}
              aria-label="Clear filter"
            >
              ✕
            </button>
          )}
        </div>
        {searchTerm && (
          <span className="perm-filter-summary">
            Showing {totalFilteredCount} matching permissions
          </span>
        )}
      </div>

      <div className="permission-categories-container">
        {categorized.map(category => {
          const isCollapsed = !!collapsedCategories[category.id];
          const categoryPerms = category.permissions;
          const selectedInCat = categoryPerms.filter(p => selectedIds.includes(p.id));
          const isAllCatSelected = categoryPerms.length > 0 && selectedInCat.length === categoryPerms.length;
          const isSomeCatSelected = selectedInCat.length > 0 && !isAllCatSelected;

          return (
            <div key={category.id} className={`permission-category-card ${isCollapsed ? 'is-collapsed' : ''}`}>
              <div
                className="category-card-header"
                onClick={() => setCollapsedCategories(prev => ({ ...prev, [category.id]: !prev[category.id] }))}
              >
                <div className="category-title-left">
                  <div className="category-icon-wrap">
                    <CategoryIcon id={category.id} />
                  </div>
                  <div>
                    <h4 className="category-title">{category.label}</h4>
                    <span className="category-desc">{category.description}</span>
                  </div>
                </div>

                <div className="category-header-right" onClick={e => e.stopPropagation()}>
                  <span
                    className={`category-status-pill ${
                      isAllCatSelected ? 'all-selected' : isSomeCatSelected ? 'some-selected' : 'none-selected'
                    }`}
                  >
                    {selectedInCat.length} / {categoryPerms.length} granted
                  </span>
                  <button
                    type="button"
                    className="category-toggle-btn"
                    disabled={disabled}
                    onClick={() => toggleCategory(categoryPerms)}
                  >
                    {isAllCatSelected ? 'Deselect all' : 'Select all'}
                  </button>
                  <button
                    type="button"
                    className={`category-chevron-btn ${isCollapsed ? 'collapsed' : ''}`}
                    onClick={() => setCollapsedCategories(prev => ({ ...prev, [category.id]: !prev[category.id] }))}
                    aria-label={isCollapsed ? `Expand ${category.label}` : `Collapse ${category.label}`}
                  >
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <polyline points="6 9 12 15 18 9" />
                    </svg>
                  </button>
                </div>
              </div>

              {!isCollapsed && (
                <div className="category-permissions-grid">
                  {categoryPerms.map(perm => {
                    const isChecked = selectedIds.includes(perm.id);
                    return (
                      <label
                        key={perm.id}
                        className={`permission-item-card ${isChecked ? 'is-checked' : ''} ${disabled ? 'is-disabled' : ''}`}
                      >
                        <input
                          type="checkbox"
                          checked={isChecked}
                          disabled={disabled}
                          onChange={() => togglePermission(perm.id)}
                        />
                        <div className="permission-item-content">
                          <div className="permission-item-top">
                            <span className="permission-name">{perm.name}</span>
                            <span className="permission-code">{perm.code}</span>
                          </div>
                          {perm.description && (
                            <p className="permission-desc">{perm.description}</p>
                          )}
                        </div>
                      </label>
                    );
                  })}
                </div>
              )}
            </div>
          );
        })}

        {categorized.length === 0 && (
          <div className="permissions-empty-search">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
              <circle cx="11" cy="11" r="8" />
              <path d="M21 21l-4.35-4.35" />
            </svg>
            <p>No permissions match &ldquo;{searchTerm}&rdquo;</p>
            <button
              type="button"
              className="btn-clear-perm-filter"
              onClick={() => setSearchTerm('')}
            >
              Clear filter
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

/* Custom Dropdown Component */
function SetupCustomSelect({
  label,
  name,
  value,
  onChange,
  options = [],
  placeholder = 'Select an option',
  disabled = false,
  required = false,
  icon
}) {
  const [open, setOpen] = useState(false);
  const containerRef = useRef(null);

  const selectedOption = options.find((opt) => String(opt.value) === String(value));
  const displayText = selectedOption ? selectedOption.label : placeholder;

  useEffect(() => {
    function handleClickOutside(e) {
      if (containerRef.current && !containerRef.current.contains(e.target)) {
        setOpen(false);
      }
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  const handleSelect = (val) => {
    if (disabled) return;
    setOpen(false);
    if (onChange) {
      onChange(val);
    }
  };

  return (
    <div className={`setup-select-wrapper ${disabled ? 'is-disabled' : ''}`} ref={containerRef}>
      <label className="field">
        <span>{label}</span>
        {/* Hidden accessible real select that keeps tests and form serialization 100% in sync */}
        <select
          name={name}
          value={value ?? ''}
          required={required}
          disabled={disabled}
          onChange={(e) => {
            if (onChange) onChange(e.target.value);
          }}
          style={{
            position: 'absolute',
            opacity: 0,
            pointerEvents: 'none',
            height: '1px',
            width: '1px',
            margin: '-1px',
            clip: 'rect(0, 0, 0, 0)',
            overflow: 'hidden'
          }}
        >
          <option value="">{placeholder}</option>
          {options.map((opt) => (
            <option key={opt.value} value={opt.value}>
              {opt.label}
            </option>
          ))}
        </select>
      </label>

      {/* Visual Custom Dropdown Trigger */}
      <div className="custom-dropdown-container">
        <button
          type="button"
          className={`custom-select-trigger ${open ? 'is-open' : ''} ${value ? 'has-value' : ''}`}
          disabled={disabled}
          onClick={() => !disabled && setOpen(!open)}
          aria-haspopup="listbox"
          aria-expanded={open}
        >
          <div className="custom-select-left">
            {icon && <span className="custom-select-icon">{icon}</span>}
            <span className="custom-select-value">{displayText}</span>
          </div>
          <svg
            className={`custom-select-chevron ${open ? 'rotate' : ''}`}
            width="16"
            height="16"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
          >
            <polyline points="6 9 12 15 18 9" />
          </svg>
        </button>

        {open && (
          <div className="custom-select-dropdown" role="listbox">
            <div
              className={`custom-select-option ${!value ? 'is-selected' : ''}`}
              role="option"
              aria-selected={!value}
              onClick={() => handleSelect('')}
            >
              <span className="option-title" style={{ color: '#94a3b8' }}>
                {placeholder}
              </span>
              {!value && (
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#d97706" strokeWidth="2.5">
                  <polyline points="20 6 9 17 4 12" />
                </svg>
              )}
            </div>

            {options.map((opt) => {
              const isSelected = String(opt.value) === String(value);
              return (
                <div
                  key={opt.value}
                  className={`custom-select-option ${isSelected ? 'is-selected' : ''}`}
                  role="option"
                  aria-selected={isSelected}
                  onClick={() => handleSelect(opt.value)}
                >
                  <div className="option-content">
                    <span className="option-title">{opt.title || opt.label}</span>
                    {opt.badge && <span className="option-badge">{opt.badge}</span>}
                  </div>
                  {isSelected && (
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#d97706" strokeWidth="2.5">
                      <polyline points="20 6 9 17 4 12" />
                    </svg>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}

export default function EmployeeSetup() {
  const { session } = useAuth();
  const admin = [session?.user?.role, ...(session?.user?.roles || [])].some(role => ['ADMIN', 'HR'].includes(role));
  const query = useEmployeeSetupQuery(undefined, { skip: !admin, refetchOnMountOrArgChange: true });
  const [remove, deletion] = useDeleteEmployeeSetupMutation();
  const [save, result] = useSaveEmployeeSetupMutation();
  const [kind, setKind] = useState('departments');
  const [editing, setEditing] = useState(null);
  const editFormRef = useRef(null);

  useEffect(() => {
    if (!editing || !editFormRef.current) return;
    const form = editFormRef.current;
    const reducedMotion = window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    form.scrollIntoView?.({ behavior: reducedMotion ? 'instant' : 'smooth', block: 'start' });
    form.querySelector('input:not([readonly]):not([disabled]), select:not([disabled]), textarea:not([disabled])')?.focus({ preventScroll: true });
  }, [editing]);
  const [search, setSearch] = useState('');
  const [notice, setNotice] = useState('');
  const [deleteTarget, setDeleteTarget] = useState(null);
  const [clientError, setClientError] = useState(null);
  const [selectedPermissionIds, setSelectedPermissionIds] = useState([]);

  const [formSelects, setFormSelects] = useState({
    name: '',
    departmentId: '',
    gradeId: '',
    family: ''
  });

  const department = id => query.data?.departments.find(x => x.id === id)?.name || '—';
  const grade = id => query.data?.grades.find(x => x.id === id)?.code || '—';

  const startEditing = (item) => {
    setEditing({ ...item });
    setFormSelects({
      name: item?.name || '',
      departmentId: item?.departmentId || '',
      gradeId: item?.gradeId || '',
      family: item?.family || ''
    });
    setSelectedPermissionIds(item?.permissionIds ? [...item.permissionIds] : []);
    setNotice('');
    setClientError(null);
    result.reset();
  };

  const rows = (query.data?.[kind] || []).filter(x =>
    `${x.name} ${x.code || ''} ${kind === 'titles' ? department(x.departmentId) + ' ' + grade(x.gradeId) : ''}`
      .toLowerCase()
      .includes(search.toLowerCase())
  );

  const stats = [
    {
      key: 'departments',
      label: 'Departments',
      count: query.data?.departments?.length ?? 0,
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M3 21h18M5 21V7l8-4v18M13 7h6v14M9 9v.01M9 12v.01M9 15v.01M9 18v.01M17 11v.01M17 14v.01M17 17v.01" />
        </svg>
      )
    },
    {
      key: 'roles',
      label: 'Roles',
      count: query.data?.roles?.length ?? 0,
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
        </svg>
      )
    },
    {
      key: 'permissions',
      label: 'Permissions',
      count: query.data?.permissions?.length ?? 0,
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
          <path d="M9 12l2 2 4-4" />
        </svg>
      )
    },
    {
      key: 'skills',
      label: 'Skills',
      count: query.data?.skills?.length ?? 0,
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M18 20V10M12 20V4M6 20v-6" />
        </svg>
      )
    },
    {
      key: 'grades',
      label: 'Grades',
      count: query.data?.grades?.length ?? 0,
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M18 20V10M12 20V4M6 20v-6" />
        </svg>
      )
    },
    {
      key: 'titles',
      label: 'Job Titles',
      count: query.data?.titles?.length ?? 0,
      icon: (
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <rect x="2" y="7" width="20" height="14" rx="2" ry="2" />
          <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
        </svg>
      )
    }
  ];

  const departmentOptions = (query.data?.departments || [])
    .filter(x => x.isActive || x.id === (editing?.departmentId || formSelects.departmentId))
    .map(x => ({
      value: x.id,
      label: x.name,
      title: x.name,
      badge: x.code
    }));

  const departmentFamilyOptions = [
    { value: '', label: 'General / All Departments' },
    ...(query.data?.departments || []).map(d => ({
      value: d.name,
      label: `${d.name} (${d.code})`,
      title: `${d.name} (${d.code})`,
      badge: d.code
    }))
  ];

  const selectedDeptId = formSelects.departmentId || editing?.departmentId;
  const selectedDept = (query.data?.departments || []).find(x => String(x.id) === String(selectedDeptId));

  const filteredGrades = (query.data?.grades || []).filter(x => {
    if (!x.isActive && x.id !== (editing?.gradeId || formSelects.gradeId)) return false;
    if (x.id === (editing?.gradeId || formSelects.gradeId)) return true;
    if (!selectedDept) return true;

    const deptCode = (selectedDept.code || '').trim().toUpperCase();
    const deptName = (selectedDept.name || '').trim().toLowerCase();
    const gradeCode = (x.code || '').trim().toUpperCase();
    const gradeFamily = (x.family || '').trim().toLowerCase();

    // 1. Explicit Department assignment on Grade (family field stores assigned department name/code)
    if (gradeFamily) {
      if (gradeFamily === 'all departments' || gradeFamily === 'general' || gradeFamily === '') {
        return true;
      }
      return (
        gradeFamily === deptName ||
        deptName.includes(gradeFamily) ||
        gradeFamily.includes(deptName) ||
        gradeFamily === deptCode.toLowerCase()
      );
    }

    // 2. Unassigned Grade (no explicit department set): Use department pattern fallback
    const isHrDept = deptCode.includes('HR') || deptCode.includes('HUMAN') || deptName.includes('hr') || deptName.includes('human');
    const isDevDept = deptCode.includes('DEV') || deptCode.includes('TECH') || deptCode.includes('DATA') || deptCode.includes('ARCH') || deptCode.includes('SDLC') || deptCode.includes('UI') || deptName.includes('developer') || deptName.includes('software');
    const isDesignOrDeliveryDept = deptCode.includes('DELIVERY') || deptCode.includes('DESIGN') || deptName.includes('delivery') || deptName.includes('design');

    if (isHrDept) {
      return gradeCode.startsWith('H');
    }
    if (isDevDept) {
      return gradeCode.startsWith('T');
    }
    if (isDesignOrDeliveryDept) {
      return gradeCode.startsWith('D');
    }
    return true;
  });

  const finalGrades = filteredGrades.length > 0
    ? filteredGrades
    : (query.data?.grades || []).filter(x => x.isActive || x.id === (editing?.gradeId || formSelects.gradeId));

  const gradeOptions = finalGrades.map(x => ({
    value: x.id,
    label: `${x.code} — ${x.name}`,
    title: `${x.code} — ${x.name}`,
    badge: x.code
  }));

  // Identify selected grade & department for dynamic title filtering
  const selectedGradeId = formSelects.gradeId || editing?.gradeId;
  const selectedGrade = (query.data?.grades || []).find(x => String(x.id) === String(selectedGradeId));
  const selectedGradeCode = (selectedGrade?.code || '').toUpperCase();
  const selectedGradeName = (selectedGrade?.name || '').toLowerCase();

  // Grade-tailored job title suggestions based on grade code, name, and department
  const getGradeJobTitleSuggestions = (gCode, gName, dName) => {
    if (!gCode && !gName) return [];
    const code = gCode || '';
    const name = gName || '';
    const dept = dName || '';

    // T-series (Developer / Engineering / Tech)
    if (code === 'T0' || name.includes('intern') || name.includes('trainee')) {
      return [
        'Intern Software Engineer',
        'Intern Full Stack Developer',
        'Intern Frontend Developer',
        'Intern Backend Developer',
        'Intern QA Engineer',
        'Graduate Engineer Trainee'
      ];
    }
    if (code === 'T1' || name.includes('junior') || name.includes('associate')) {
      return [
        'Junior Software Engineer',
        'Associate Software Engineer',
        'Junior Full Stack Developer',
        'Junior Backend Developer',
        'Junior Frontend Developer',
        'Junior QA Engineer'
      ];
    }
    if (code === 'T2' || name.includes('mid') || code === 'L2') {
      return [
        'Software Engineer',
        'Full Stack Developer',
        'Backend Developer',
        'Frontend Developer',
        'DevOps Engineer',
        'QA Engineer',
        'Systems Engineer'
      ];
    }
    if (code === 'T3' || name.includes('senior') || code === 'L3') {
      return [
        'Senior Software Engineer',
        'Senior Full Stack Developer',
        'Senior Backend Developer',
        'Senior Frontend Developer',
        'Senior DevOps Engineer',
        'Senior QA Engineer',
        'Senior Cloud Architect'
      ];
    }
    if (code === 'T4' || name.includes('staff') || name.includes('lead') || name.includes('principal') || code === 'L4') {
      return [
        'Staff Software Engineer',
        'Lead Software Engineer',
        'Principal Software Engineer',
        'Technical Lead',
        'Engineering Manager'
      ];
    }

    // H-series (Human Resources)
    if (code === 'H1' || (code.startsWith('H') && (name.includes('associate') || name.includes('trainee') || name.includes('coordinator')))) {
      return [
        'HR Associate',
        'HR Operations Trainee',
        'HR Coordinator',
        'Talent Acquisition Associate'
      ];
    }
    if (code === 'H2' || (code.startsWith('H') && (name.includes('executive') || name.includes('specialist') || name.includes('generalist')))) {
      return [
        'HR Executive',
        'HR Operations Specialist',
        'HR Generalist',
        'Talent Acquisition Specialist'
      ];
    }
    if (code === 'H3' || (code.startsWith('H') && (name.includes('partner') || name.includes('senior') || name.includes('bp')))) {
      return [
        'HR Business Partner (HRBP)',
        'Senior HR Specialist',
        'Senior HR Business Partner',
        'Senior Talent Acquisition Specialist',
        'Lead HR Specialist'
      ];
    }
    if (code === 'H4' || (code.startsWith('H') && (name.includes('manager') || name.includes('head') || name.includes('director')))) {
      return [
        'HR Manager',
        'Head of Human Resources',
        'Director of People Operations',
        'Senior HR Manager'
      ];
    }

    // D-series (Design & Delivery)
    if (code === 'D1') {
      return [
        'Associate UI/UX Designer',
        'Junior Product Designer',
        'Junior Delivery Associate'
      ];
    }
    if (code === 'D2') {
      return [
        'UI/UX Designer',
        'Product Designer',
        'Visual Designer',
        'Delivery Specialist'
      ];
    }
    if (code === 'D3') {
      return [
        'Senior UI/UX Designer',
        'Senior Product Designer',
        'Senior Design Specialist',
        'Delivery Manager'
      ];
    }
    if (code === 'D4') {
      return [
        'Principal Designer',
        'Design Lead',
        'Head of Product Design',
        'Delivery Director'
      ];
    }

    // Dynamic pattern fallback using department name
    if (dept) {
      if (code.includes('0') || name.includes('intern')) return [`Intern ${dept}`, `Graduate Trainee - ${dept}`];
      if (code.includes('1') || name.includes('junior')) return [`Junior ${dept} Specialist`, `Associate ${dept}`];
      if (code.includes('2')) return [`${dept} Specialist`, `${dept} Officer`, `${dept} Analyst`];
      if (code.includes('3') || name.includes('senior')) return [`Senior ${dept} Specialist`, `Senior ${dept} Officer`];
      if (code.includes('4') || name.includes('lead') || name.includes('manager')) return [`Lead ${dept}`, `${dept} Manager`, `Head of ${dept}`];
    }

    return [];
  };

  const gradeSuggestions = getGradeJobTitleSuggestions(selectedGradeCode, selectedGradeName, selectedDept?.name);

  // Filter existing active titles matching this grade
  const allActiveTitles = (query.data?.titles || []).filter(x => x.isActive);
  const existingTitlesForGrade = selectedGrade
    ? allActiveTitles.filter(x => {
        if (x.gradeId === selectedGrade.id) return true;
        const titleLower = (x.name || '').toLowerCase();
        if (selectedGradeCode === 'T0' || selectedGradeName.includes('intern')) return titleLower.includes('intern') || titleLower.includes('trainee');
        if (selectedGradeCode === 'T1' || selectedGradeName.includes('junior')) return titleLower.includes('junior') || titleLower.includes('associate');
        if (selectedGradeCode === 'T2') return (titleLower.includes('developer') || titleLower.includes('engineer')) && !/(intern|junior|senior|staff|lead|principal)/i.test(titleLower);
        if (selectedGradeCode === 'T3' || selectedGradeName.includes('senior')) return titleLower.includes('senior') || titleLower.includes('sr');
        if (selectedGradeCode === 'T4' || selectedGradeName.includes('lead') || selectedGradeName.includes('staff')) return titleLower.includes('staff') || titleLower.includes('lead') || titleLower.includes('principal');
        if (selectedGradeCode.startsWith('H')) {
          if (selectedGradeCode === 'H1') return titleLower.includes('associate') || titleLower.includes('trainee') || titleLower.includes('coordinator');
          if (selectedGradeCode === 'H2') return titleLower.includes('executive') || titleLower.includes('specialist') || titleLower.includes('generalist');
          if (selectedGradeCode === 'H3') return titleLower.includes('partner') || titleLower.includes('bp') || titleLower.includes('senior');
          if (selectedGradeCode === 'H4') return titleLower.includes('manager') || titleLower.includes('head') || titleLower.includes('director');
          return titleLower.includes('hr') || titleLower.includes('human');
        }
        return false;
      })
    : allActiveTitles;

  const existingTitleOptions = useMemo(() => {
    const seen = new Set();
    const list = [];

    // 1. Direct grade-matching existing titles
    for (const t of existingTitlesForGrade) {
      if (t.name && !seen.has(t.name.trim())) {
        seen.add(t.name.trim());
        list.push({
          value: t.name.trim(),
          label: t.name.trim(),
          title: t.name.trim(),
          badge: selectedGrade?.code ? selectedGrade.code : undefined,
        });
      }
    }

    // 2. Grade suggestions
    for (const s of gradeSuggestions) {
      if (!seen.has(s.trim())) {
        seen.add(s.trim());
        list.push({
          value: s.trim(),
          label: s.trim(),
          title: s.trim(),
          badge: selectedGrade?.code ? `${selectedGrade.code} Suggestion` : 'Suggested',
        });
      }
    }

    // 3. Append remaining organization titles if grade is selected
    if (selectedGrade) {
      for (const t of allActiveTitles) {
        if (t.name && !seen.has(t.name.trim())) {
          seen.add(t.name.trim());
          list.push({
            value: t.name.trim(),
            label: t.name.trim(),
            title: t.name.trim(),
            badge: 'Other'
          });
        }
      }
    }

    return list;
  }, [existingTitlesForGrade, gradeSuggestions, allActiveTitles, selectedGrade]);

  if (!admin) {
    return (
      <section className="panel">
        <h1>Administrator or HR access required</h1>
        <p>Admins and HR can manage organization setup.</p>
      </section>
    );
  }

  return (
    <div className="employee-page employee-setup-page">
      <PageHeader
        title="Organization setup"
        description="Manage departments, roles, grades, and job titles."
      >
        <button
          className="primary"
          onClick={() => {
            setKind('titles');
            startEditing({ linking: true });
            setSearch('');
          }}
        >
          <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ marginRight: 6 }}>
            <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
            <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
          </svg>
          Link department, grade &amp; title
        </button>
        <Link className="button" to="/employees/admin">
          Employee management
        </Link>
      </PageHeader>

      {/* Metric KPI Overview Row */}
      <div className="setup-metrics-grid">
        {stats.map(s => (
          <div
            key={s.key}
            className={`setup-metric-card ${kind === s.key ? 'active' : ''}`}
            onClick={() => {
              setKind(s.key);
              setEditing(null);
              setSearch('');
              setNotice('');
              result.reset();
            }}
            role="button"
            tabIndex={0}
          >
            <div className="metric-icon-wrap">{s.icon}</div>
            <div className="metric-content">
              <span className="metric-label">{s.label}</span>
              <span className="metric-value">{s.count}</span>
            </div>
          </div>
        ))}
      </div>

      {/* Tabs Navigation */}
      <div className="setup-tabs-wrapper">
        <nav className="setup-tabs" aria-label="Setup categories">
          {Object.entries(labels).map(([key, label]) => {
            const count = query.data?.[key]?.length;
            return (
              <button
                key={key}
                aria-pressed={kind === key}
                onClick={() => {
                  setKind(key);
                  setEditing(null);
                  setSearch('');
                  setNotice('');
                  result.reset();
                }}
              >
                {label}
                {count !== undefined && <span className="tab-count-badge" aria-hidden="true">{count}</span>}
              </button>
            );
          })}
        </nav>
      </div>

      {/* Context Description Banner */}
      <div className="setup-context-card">
        <svg className="context-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <circle cx="12" cy="12" r="10" />
          <line x1="12" y1="16" x2="12" y2="12" />
          <line x1="12" y1="8" x2="12.01" y2="8" />
        </svg>
        <p>{descriptions[kind] || 'Departments, grades, and job titles belong to the shared career catalog. Roles belong to your organization.'}</p>
      </div>

      <QueryState query={query}>
        {kind === 'users' ? (
          <UserRoles roles={query.data?.roles || []} />
        ) : kind === 'permissions' ? (
          <PermissionsCatalog
            permissions={query.data?.permissions || []}
            roles={query.data?.roles || []}
            onSaveRole={save}
            isSaving={result.isLoading}
            onDeletePermission={remove}
            deletion={deletion}
            onSavePermission={save}
          />
        ) : (
          <>
            {/* Toolbar */}
            <div className="setup-toolbar">
              <div className="toolbar-search-wrap">
                <Field label={`Search ${labels[kind].toLowerCase()}`}>
                  <div className="search-input-container">
                    <svg className="search-icon" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                      <circle cx="11" cy="11" r="8" />
                      <path d="M21 21l-4.35-4.35" />
                    </svg>
                    <input
                      value={search}
                      placeholder={`Filter ${labels[kind].toLowerCase()} by name, code...`}
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

              <div className="toolbar-buttons">
                <button
                  className="btn-add-primary"
                  onClick={() => startEditing({})}
                >
                  <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                    <line x1="12" y1="5" x2="12" y2="19" />
                    <line x1="5" y1="12" x2="19" y2="12" />
                  </svg>
                  Add {singular[kind]}
                </button>
                {kind !== 'skills' && <button
                  className="btn-delete-all"
                  disabled={!query.data?.[kind]?.length || deletion.isLoading}
                  onClick={() => {
                    deletion.reset();
                    setDeleteTarget({
                      kind,
                      all: true,
                      name: `all ${labels[kind].toLowerCase()}`,
                      count: query.data[kind].length
                    });
                  }}
                >
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                    <polyline points="3 6 5 6 21 6" />
                    <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                  </svg>
                  Delete all {labels[kind].toLowerCase()}
                </button>}
              </div>
            </div>

            {notice && (
              <p role="status" className="setup-notice-banner">
                <svg className="notice-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                  <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
                  <polyline points="22 4 12 14.01 9 11.01" />
                </svg>
                <span>{notice}</span>
              </p>
            )}

            {/* Editing / Creating Form Card */}
            {editing && (
              <form
                key={`${kind}-${editing.id || (editing.linking ? 'link' : 'new')}`}
                ref={editFormRef}
                style={{ scrollMarginTop: '6rem' }}
                className="panel setup-form"
                onSubmit={async e => {
                  e.preventDefault();
                  setClientError(null);
                  const values = Object.fromEntries(new FormData(e.currentTarget));
                  const resolvedDeptId = editing.departmentId || values.departmentId || formSelects.departmentId;
                  const resolvedGradeId = editing.gradeId || values.gradeId || formSelects.gradeId;
                  const resolvedName = editing.linking ? (values.name || formSelects.name) : values.name;
                  const defaultTitle = kind === 'titles' ? (
                    gradeSuggestions[0] ||
                    (selectedGrade?.name && selectedGrade.name !== selectedGrade.code ? selectedGrade.name : `${selectedDept?.name || ''} ${selectedGrade?.code || ''}`.trim()) ||
                    selectedGrade?.code ||
                    'Job Title'
                  ) : '';
                  const finalName = resolvedName?.trim() || (kind === 'grades' ? (editing.code || values.code)?.trim().toUpperCase() : kind === 'titles' ? defaultTitle : resolvedName);
                  const finalCode = (editing.code || values.code)?.trim().toUpperCase();

                  const existingItems = query.data?.[kind] || [];
                  if (!editing.id && finalCode) {
                    if (kind === 'departments') {
                      const exactDuplicate = existingItems.some(
                        x => x.code?.toUpperCase() === finalCode && x.name?.trim().toLowerCase() === finalName?.trim().toLowerCase()
                      );
                      if (exactDuplicate) {
                        setClientError({
                          message: `A department with code "${finalCode}" and name "${finalName}" already exists.`
                        });
                        return;
                      }
                    } else if (['grades', 'roles'].includes(kind)) {
                      const codeDuplicate = existingItems.some(x => x.code?.toUpperCase() === finalCode);
                      if (codeDuplicate) {
                        setClientError({
                          message: `A ${singular[kind]} with code "${finalCode}" already exists. Please enter a unique code.`
                        });
                        return;
                      }
                    }
                  }

                  const body = {
                    ...values,
                    name: finalName,
                    code: kind === 'titles' ? null : finalCode,
                    departmentId: kind === 'titles' ? Number(resolvedDeptId) : null,
                    gradeId: kind === 'titles' ? Number(resolvedGradeId) : null,
                    displayOrder: Number(values.displayOrder || 0),
                    family: kind === 'grades' ? (formSelects.family || values.family || null) : (values.family || null),
                    category: values.category || null,
                    description: values.description || null,
                    isActive: values.isActive !== 'false',
                    permissionIds: kind === 'roles' ? selectedPermissionIds : undefined
                  };
                  try {
                    await save({ kind, id: editing.id, body }).unwrap();
                    setEditing(null);
                    setNotice(
                      editing.linking
                        ? 'Department, grade, and job title linked. The mapping is now available in employee forms.'
                        : `${labels[kind]} saved.`
                    );
                  } catch {
                    /* Keep entered values and show server validation below. */
                  }
                }}
              >
                <div className="setup-form-header">
                  <div className="setup-form-title-wrap">
                    <div className="setup-form-icon">
                      <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        {editing.linking ? (
                          <>
                            <path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71" />
                            <path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71" />
                          </>
                        ) : (
                          <>
                            <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                            <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                          </>
                        )}
                      </svg>
                    </div>
                    <div>
                      <h2>
                        {editing.linking
                          ? 'Link department, grade & title'
                          : `${editing.id ? 'Edit' : 'Add'} ${singular[kind]}`}
                      </h2>
                      <p className="setup-form-subtitle">
                        {editing.linking
                          ? 'Map a department and grade combination to a specific job title.'
                          : `Configure details, identifiers, and properties for this ${singular[kind]}.`}
                      </p>
                    </div>
                  </div>
                  <button
                    type="button"
                    className="setup-form-close-btn"
                    onClick={() => {
                      setEditing(null);
                      result.reset();
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

                <div className="setup-fields">
                  {kind !== 'titles' && (
                    <Field label="Code">
                      <input
                        name="code"
                        required
                        maxLength={kind === 'roles' ? 50 : 100}
                        pattern="[A-Za-z0-9_]+"
                        placeholder={
                          kind === 'grades'
                            ? 'e.g. GRD-01 or L1'
                            : kind === 'departments'
                            ? 'e.g. ENG or HR'
                            : 'e.g. ADMIN or MGR'
                        }
                        defaultValue={editing.code || ''}
                        readOnly={!!editing.id}
                      />
                    </Field>
                  )}

                  {kind === 'titles' ? (
                    <>
                      <SetupCustomSelect
                        label="Department"
                        name="departmentId"
                        value={formSelects.departmentId}
                        onChange={(val) => setFormSelects(prev => ({ ...prev, departmentId: val, gradeId: '' }))}
                        options={departmentOptions}
                        placeholder="Select department"
                        disabled={!!editing.id}
                        required
                        icon={
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <path d="M3 21h18M5 21V7l8-4v18M13 7h6v14" />
                          </svg>
                        }
                      />
                      <SetupCustomSelect
                        label="Grade"
                        name="gradeId"
                        value={formSelects.gradeId}
                        onChange={(val) => setFormSelects(prev => ({ ...prev, gradeId: val }))}
                        options={gradeOptions}
                        placeholder={!formSelects.departmentId ? 'Select department first' : 'Select grade'}
                        disabled={!!editing.id}
                        required
                        icon={
                          <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                            <path d="M18 20V10M12 20V4M6 20v-6" />
                          </svg>
                        }
                      />
                      {editing.linking ? (
                        <SetupCustomSelect
                          label="Job title"
                          name="name"
                          value={formSelects.name}
                          onChange={(val) => setFormSelects(prev => ({ ...prev, name: val }))}
                          options={existingTitleOptions}
                          placeholder={!formSelects.gradeId ? 'Select grade first to filter titles' : `Select job title for grade ${selectedGradeCode || ''}`}
                          required
                          icon={
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                              <rect x="2" y="7" width="20" height="14" rx="2" ry="2" />
                              <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
                            </svg>
                          }
                        />
                      ) : (
                        <Field label="Job title">
                          <div className="setup-title-combobox">
                            <input
                              name="name"
                              maxLength={200}
                              value={formSelects.name}
                              onChange={(e) => setFormSelects(prev => ({ ...prev, name: e.target.value }))}
                              placeholder={
                                selectedGrade
                                  ? `(Optional) Defaults to "${gradeSuggestions[0] || selectedGrade.name || selectedGrade.code}"`
                                  : 'e.g. Full Stack Developer (optional)'
                              }
                            />
                            {gradeSuggestions.length > 0 && (
                              <div className="setup-grade-suggestions">
                                <span className="setup-suggestions-label">Suggestions for {selectedGradeCode}:</span>
                                <div className="setup-suggestions-chips">
                                  {gradeSuggestions.slice(0, 5).map((sugg) => (
                                    <button
                                      key={sugg}
                                      type="button"
                                      className={`setup-chip ${formSelects.name === sugg ? 'is-active' : ''}`}
                                      onClick={() => setFormSelects(prev => ({ ...prev, name: sugg }))}
                                      title={`Use "${sugg}"`}
                                    >
                                      {sugg}
                                    </button>
                                  ))}
                                </div>
                              </div>
                            )}
                          </div>
                        </Field>
                      )}
                    </>
                  ) : (
                    <Field label={kind === 'grades' ? 'Grade Name (optional)' : 'Name'}>
                      <input
                        name="name"
                        required={kind !== 'grades'}
                        maxLength={kind === 'roles' ? 150 : 200}
                        placeholder={
                          kind === 'grades'
                            ? 'e.g. Senior Grade / Level 4 (optional)'
                            : kind === 'departments'
                            ? 'e.g. Engineering & Technology'
                            : kind === 'roles'
                            ? 'e.g. Project Lead'
                            : 'e.g. Full Stack Developer'
                        }
                        defaultValue={editing.name || ''}
                      />
                    </Field>
                  )}

                  {kind === 'grades' && (
                    <SetupCustomSelect
                      label="Department"
                      name="family"
                      value={formSelects.family}
                      onChange={(val) => setFormSelects(prev => ({ ...prev, family: val }))}
                      options={departmentFamilyOptions}
                      placeholder="Select department to link this grade"
                      icon={
                        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                          <path d="M3 21h18M5 21V7l8-4v18M13 7h6v14" />
                        </svg>
                      }
                    />
                  )}
                  {kind === 'skills' && <>
                    <Field label="Category" name="category" maxLength={100} defaultValue={editing.category || ''} />
                    <Field label="Description" name="description" maxLength={1000} defaultValue={editing.description || ''} />
                    <Field label="Status"><select name="isActive" defaultValue={String(editing.isActive !== false)}><option value="true">Active</option><option value="false">Inactive</option></select></Field>
                  </>}
                  {['grades', 'titles'].includes(kind) && (
                    <Field label="Display order">
                      <input
                        name="displayOrder"
                        type="number"
                        min="0"
                        step="1"
                        placeholder="0"
                        defaultValue={editing.displayOrder || 0}
                      />
                    </Field>
                  )}

                  {kind === 'roles' && (
                    <Field label="Description">
                      <textarea
                        name="description"
                        maxLength={1000}
                        placeholder="Provide an overview of responsibilities and permissions for this role..."
                        defaultValue={editing.description || ''}
                        rows={3}
                      />
                    </Field>
                  )}
                </div>

                {kind === 'roles' && (
                  <RolePermissionsMatrix
                    allPermissions={query.data?.permissions || []}
                    selectedIds={selectedPermissionIds}
                    onChange={setSelectedPermissionIds}
                    disabled={result.isLoading}
                  />
                )}

                {kind === 'departments' && (
                  <p className="form-tip">
                    Tip: A department represents the entire organizational unit (e.g. <em>Human Resources</em>). To add multiple job titles (e.g. <em>HR Operations</em>, <em>HR Business Partner</em>) under this department, use the <strong>Job Titles</strong> tab.
                  </p>
                )}
                {kind === 'titles' && (
                  <p className="form-tip">
                    A title links a department to a grade. Each department/grade pair has one title; add a new title to create another mapping.
                  </p>
                )}
                {kind === 'roles' && (
                  <p className="form-tip">
                    Configure granular system permissions above to grant specific authorized capabilities to this role.
                  </p>
                )}
                {editing.id && kind !== 'titles' && (
                  <p className="form-tip">
                    Codes remain fixed to preserve existing assignments and access rules.
                  </p>
                )}

                <div className="actions">
                  <button className="primary" disabled={result.isLoading}>
                    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                      <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z" />
                      <polyline points="17 21 17 13 7 13 7 21" />
                      <polyline points="7 3 7 8 15 8" />
                    </svg>
                    {editing.linking ? 'Save mapping' : `Save ${singular[kind]}`}
                  </button>
                  <button
                    type="button"
                    className="button-cancel"
                    disabled={result.isLoading}
                    onClick={() => {
                      setEditing(null);
                      result.reset();
                    }}
                  >
                    Cancel
                  </button>
                </div>
                {(clientError || result.error) && (
                  <ErrorState
                    error={
                      clientError ||
                      (result.error?.status === 409
                        ? { message: `A ${singular[kind]} with this code or name already exists in your organization records. Please use a unique code and name.` }
                        : result.error)
                    }
                  />
                )}
              </form>
            )}

            {/* Table Container Card */}
            <div className="setup-table-card">
              <div className="table-header-info">
                <div className="table-title">
                  <span>{labels[kind]} Catalog</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <div className="table-count-badge">
                    Showing {rows.length} of {query.data?.[kind]?.length || 0} records
                  </div>
                  {rows.length > 0 && (
                    <button
                      type="button"
                      className="button button-sm button-danger"
                      style={{ padding: '4px 10px', fontSize: '12px', display: 'inline-flex', alignItems: 'center', gap: '5px' }}
                      disabled={deletion.isLoading}
                      onClick={() => {
                        deletion.reset();
                        setDeleteTarget({
                          all: true,
                          count: rows.length,
                          kind,
                          name: `all ${labels[kind].toLowerCase()}`
                        });
                      }}
                    >
                      <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                        <polyline points="3 6 5 6 21 6" />
                        <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                      </svg>
                      Delete all {labels[kind].toLowerCase()}
                    </button>
                  )}
                </div>
              </div>

              <div className="setup-table">
                <table>
                  <thead>
                    <tr>
                      {kind !== 'titles' && <th>Code</th>}
                      <th>{kind === 'titles' ? 'Job title' : 'Name'}</th>
                      {kind === 'titles' && (
                        <>
                          <th>Department</th>
                          <th>Grade</th>
                        </>
                      )}
                      {kind === 'grades' && <th>Department</th>}
                      {kind === 'roles' && <th>Permissions</th>}
                      {kind === 'roles' && <th>Description</th>}
                      <th style={{ textAlign: 'right' }}>Actions</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map(row => (
                      <tr key={row.id}>
                        {kind !== 'titles' && (
                          <td>
                            <span className="setup-code-badge">{row.code}</span>
                          </td>
                        )}
                        <td>
                          <div className="setup-name-cell">
                            <span>{row.name}</span>
                            {row.isActive === false && (
                              <span className="inactive-badge">(inactive)</span>
                            )}
                          </div>
                        </td>
                        {kind === 'titles' && (
                          <>
                            <td>
                              <span className="setup-chip dept-chip">{department(row.departmentId)}</span>
                            </td>
                            <td>
                              <span className="setup-chip grade-chip">{grade(row.gradeId)}</span>
                            </td>
                          </>
                        )}
                        {kind === 'grades' && (
                          <td>
                            <span className="setup-chip dept-chip">{row.family || 'All Departments'}</span>
                          </td>
                        )}
                        {kind === 'roles' && (
                          <td>
                            <span className={`role-perm-chip ${(row.permissionIds?.length || 0) > 0 ? 'has-perms' : 'no-perms'}`}>
                              <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5">
                                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                              </svg>
                              {row.permissionIds?.length || 0} granted
                            </span>
                          </td>
                        )}
                        {kind === 'roles' && (
                          <td style={{ color: '#64748b', fontSize: '13px' }}>
                            {row.description || '—'}
                          </td>
                        )}
                        <td style={{ textAlign: 'right' }}>
                          <div className="setup-row-actions" style={{ justifyContent: 'flex-end' }}>
                            <button
                              className="setup-action-btn edit-btn"
                              aria-label={`Edit ${row.name}`}
                              onClick={() => startEditing(row)}
                            >
                              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7" />
                                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z" />
                              </svg>
                              Edit
                            </button>
                            {['departments', 'grades', 'roles', 'titles', 'skills'].includes(kind) && (
                              <button
                                className="setup-action-btn delete-btn"
                                disabled={deletion.isLoading}
                                aria-label={`Delete ${row.name}`}
                                onClick={() => {
                                  deletion.reset();
                                  setDeleteTarget({ ...row, kind });
                                }}
                              >
                                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                                  <polyline points="3 6 5 6 21 6" />
                                  <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                                </svg>
                                Delete
                              </button>
                            )}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>

                {!rows.length && (
                  <div className="setup-empty-state">
                    <svg className="empty-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
                      <circle cx="11" cy="11" r="8" />
                      <path d="M21 21l-4.35-4.35" />
                    </svg>
                    <h4>No matching {labels[kind].toLowerCase()} found</h4>
                    <p>
                      {search
                        ? `No results matched "${search}". Try a different keyword.`
                        : `No ${labels[kind].toLowerCase()} have been added yet.`}
                    </p>
                    {search && (
                      <button
                        type="button"
                        className="btn-clear-filter"
                        onClick={() => setSearch('')}
                      >
                        Clear search filter
                      </button>
                    )}
                  </div>
                )}
              </div>
            </div>
          </>
        )}
      </QueryState>

      {deleteTarget && (
        <DeleteConfirmation
          name={deleteTarget.name}
          confirmLabel={deleteTarget.all ? 'Delete all' : 'Delete'}
          description={
            deleteTarget.all
              ? `This applies to all ${deleteTarget.count} records in ${labels[deleteTarget.kind]}, including records hidden by search. Linked or protected records will be skipped. Other tabs are unchanged. This cannot be undone.`
              : undefined
          }
          busy={deletion.isLoading}
          error={deletion.error}
          onCancel={() => {
            setDeleteTarget(null);
            deletion.reset();
          }}
          onConfirm={async () => {
            try {
              const summary = await remove({ kind: deleteTarget.kind, id: deleteTarget.id }).unwrap();
              setEditing(null);
              setNotice(
                deleteTarget.all
                  ? `${summary.deleted} deleted; ${summary.skipped} linked or protected records kept.`
                  : `${deleteTarget.name} deleted.`
              );
              setDeleteTarget(null);
            } catch {
              /* Keep the popup open with its validation message. */
            }
          }}
        />
      )}
    </div>
  );
}
