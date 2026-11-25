import { useState, useMemo } from 'react';
import { useSearchParams } from 'react-router';
import Link from '../../../src/components/PortalLink';
import { useAuth } from '../../../src/features/auth/context';
import {
  useEmployeesQuery,
  useEmployeeReferencesQuery,
  useSaveEmployeeMutation,
  useEmployeeCareerCatalogQuery,
  useEmployeeManagersQuery,
} from '../../../src/services/api/employees';
import { PageHeader, QueryState, ErrorState, Pagination, Badge, Field, SelectField, CustomSelect, EmptyState } from '../../../src/components/common';
import EmployeeEditor from '../../../src/features/employees/EmployeeEditor';
import BulkEmployeeEditor from '../../../src/features/employees/BulkEmployeeEditor';
import OnboardingReview from '../../../src/features/employees/OnboardingReview';
import EmployeePhoto from '../../../src/features/employees/EmployeePhoto';
import { exportToExcelSpreadsheet } from '../exportSpreadsheet';
import '../../../src/styles/employees.scss';

export default function EmployeeDirectory({ management = false }) {
  const { can, session } = useAuth();
  const isAdminOrHr = [session?.user?.role, ...(session?.user?.roles || [])].some((r) => ['ADMIN', 'HR'].includes(r));
  const [searchParams, setSearchParams] = useSearchParams();
  const onboardingId = management && can('EMPLOYEE_CREATE') ? searchParams.get('onboarding') : null;
  const [filters, setFilters] = useState({
    page: 1,
    pageSize: 50,
  });
  const [viewMode, setViewMode] = useState('table');
  const [selectedIds, setSelectedIds] = useState([]);
  const [bulkMode, setBulkMode] = useState(false);
  const [bulkUpdating, setBulkUpdating] = useState(false);
  const [creating, setCreating] = useState(
    management && searchParams.get('create') === '1' && can('EMPLOYEE_CREATE'),
  );

  const query = useEmployeesQuery(filters);
  const refs = useEmployeeReferencesQuery();

  const name = (id) => refs.data?.find((r) => r.id === id)?.name || '—';
  const filter = (key, value) => setFilters((f) => ({ ...f, [key]: value, page: 1 }));
  const resetFilters = () => {
    setFilters({ page: 1, pageSize: 50 });
    setSelectedIds([]);
  };

  const departments = refs.data?.filter((r) => r.kind === 'DEPARTMENT') || [];
  const grades = refs.data?.filter((r) => r.kind === 'GRADE') || [];

  const getInitials = (first = '', last = '') => {
    const f = first.trim()[0] || '';
    const l = last.trim()[0] || '';
    return (f + l).toUpperCase() || '?';
  };

  const getAvatarColor = (str = '') => {
    const palette = [
      { bg: '#fef3c7', text: '#b45309' }, // Amber
      { bg: '#e0f2fe', text: '#0369a1' }, // Sky
      { bg: '#f3e8ff', text: '#7e22ce' }, // Purple
      { bg: '#dcfce7', text: '#15803d' }, // Green
      { bg: '#ffe4e6', text: '#e11d48' }, // Pink
      { bg: '#ccfbf1', text: '#0f766e' }, // Teal
      { bg: '#fef9c3', text: '#854d0e' }, // Yellow
    ];
    let hash = 0;
    for (let i = 0; i < str.length; i++) hash = str.charCodeAt(i) + ((hash << 5) - hash);
    const idx = Math.abs(hash) % palette.length;
    return palette[idx];
  };

  const rawItems = query.data?.items || [];
  const items = [...rawItems].sort((a, b) => {
    const nameA = `${a.firstName || ''} ${a.lastName || ''}`.trim() || a.preferredName || '';
    const nameB = `${b.firstName || ''} ${b.lastName || ''}`.trim() || b.preferredName || '';
    return nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
  });
  const totalCount = query.data?.totalCount ?? items.length;

  const hasSearch = Boolean(filters.search?.trim() || filters.gradeId || filters.departmentId || filters.employmentStatus);

  const allCurrentIds = items.map((e) => e.id);
  const isAllSelected = allCurrentIds.length > 0 && allCurrentIds.every((id) => selectedIds.includes(id));

  const toggleSelectAll = () => {
    if (isAllSelected) {
      setSelectedIds([]);
    } else {
      setSelectedIds(allCurrentIds);
    }
  };

  const toggleSelect = (id) => {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    );
  };

  function getExportData(itemsToExport) {
    const headers = ['Employee Code', 'Full Name', 'First Name', 'Last Name', 'Grade', 'Department', 'Employment Status', 'Joining Date'];
    const rows = itemsToExport.map((e) => [
      e.employeeCode || '—',
      `${e.preferredName || e.firstName || ''} ${e.lastName || ''}`.trim(),
      e.firstName || '',
      e.lastName || '',
      name(e.gradeId) || '—',
      name(e.departmentId) || '—',
      e.employmentStatus || '—',
      e.joiningDate ? String(e.joiningDate).slice(0, 10) : '—',
    ]);
    return { headers, rows };
  }

  function handleDownloadCSV() {
    const targetItems = selectedIds.length > 0
      ? items.filter((e) => selectedIds.includes(e.id))
      : items;

    if (!targetItems.length) return;
    const { headers, rows } = getExportData(targetItems);

    function escapeCell(val) {
      const str = String(val ?? '');
      if (str.includes(',') || str.includes('"') || str.includes('\n') || str.includes('\r')) {
        return `"${str.replace(/"/g, '""')}"`;
      }
      return str;
    }

    const csvContent = '\uFEFF' + [
      headers.map(escapeCell).join(','),
      ...rows.map((row) => row.map(escapeCell).join(',')),
    ].join('\r\n');

    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `Employees_Work_History_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  function handleExportExcel() {
    const targetItems = selectedIds.length > 0
      ? items.filter((e) => selectedIds.includes(e.id))
      : items;

    if (!targetItems.length) return;
    const { headers, rows } = getExportData(targetItems);

    const metadata = [
      { label: 'Directory', value: management ? 'Employee Management' : 'Employee Directory' },
      { label: 'Scope', value: selectedIds.length > 0 ? (selectedIds.length + ' Selected Employees') : 'All Listed Employees' },
      { label: 'Export Date', value: new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) },
      { label: 'Total Employees', value: String(rows.length) },
    ];

    exportToExcelSpreadsheet({
      filename: 'Employees_Work_History_' + new Date().toISOString().slice(0, 10) + '.xlsx',
      sheetName: 'Employees',
      title: 'Employee Work History & Directory',
      metadata,
      headers,
      rows,
    });
  }

  return (
    <div className="employee-page employee-management-page">
      <PageHeader
        title={management ? 'Employee management' : 'Employee directory'}
        description="People, skills and reporting relationships in your organization."
      >
        {management && can('EMPLOYEE_CREATE') && (
          <div className="employee-create-actions">
            <button
              type="button"
              className="button bulk-add-btn"
              onClick={() => {
                setBulkMode(true);
                setCreating(true);
              }}
            >
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                <path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2" />
                <circle cx="9" cy="7" r="4" />
                <path d="M23 21v-2a4 4 0 0 0-3-3.87" />
                <path d="M16 3.13a4 4 0 0 1 0 7.75" />
              </svg>
              Bulk add
            </button>
            <button
              type="button"
              className="primary"
              onClick={() => {
                setBulkMode(false);
                setCreating(!creating || bulkMode);
              }}
            >
              Add employee
            </button>
          </div>
        )}
      </PageHeader>

      {onboardingId && (
        <section aria-label="Add employee from onboarding request">
          <h2>Add employee</h2>
          <OnboardingReview id={onboardingId} standalone completing onCompleteClose={() => {
            setSearchParams({});
            setCreating(false);
            query.refetch();
          }} />
        </section>
      )}
      {creating && !onboardingId && (
        bulkMode ? (
          <BulkEmployeeEditor
            references={refs.data || []}
            onClose={() => setCreating(false)}
            onSaved={() => query.refetch()}
          />
        ) : (
          <EmployeeEditor
            references={refs.data || []}
            onClose={() => setCreating(false)}
            onSaved={() => query.refetch()}
            onSwitchToBulk={() => setBulkMode(true)}
          />
        )
      )}

      <section className="panel employee-panel">
        <div className="employee-panel-header-row">
          <div className="employee-panel-header-text">
            <h2>Find employees</h2>
            <p>
              {totalCount > 0
                ? `Showing ${items.length} of ${totalCount} employee${totalCount === 1 ? '' : 's'}`
                : 'Search, filter and manage directory of all staff members.'}
            </p>
          </div>

          <div className="career-history-actions">
            <button
              type="button"
              className="button-download-csv"
              onClick={handleDownloadCSV}
              title="Download CSV"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><polyline points="7 10 12 15 17 10"/><line x1="12" y1="15" x2="12" y2="3"/></svg>
              Download CSV
            </button>
            <button
              type="button"
              className="button-export-excel"
              onClick={handleExportExcel}
              title="Open Spreadsheet Preview"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><polygon points="12 2 2 7 12 12 22 7 12 2"/><polyline points="2 17 12 22 22 17"/><polyline points="2 12 12 17 22 12"/></svg>
              Spreadsheet View
            </button>
          </div>
        </div>

        <div className="employee-directory-toolbar">
          <form className="employee-filters" onSubmit={(e) => e.preventDefault()}>
            <Field
              label="Search"
              placeholder="Search by name or code..."
              type="search"
              value={filters.search || ''}
              onChange={(e) => filter('search', e.target.value)}
            />

            <SelectField
              label="department"
              value={filters.departmentId || ''}
              onChange={(e) => filter('departmentId', e.target.value)}
              empty="All departments"
              values={departments.map((d) => ({ value: String(d.id), label: d.name }))}
            />

            <SelectField
              label="grade"
              value={filters.gradeId || ''}
              onChange={(e) => filter('gradeId', e.target.value)}
              empty="All grades"
              values={grades.map((g) => ({ value: String(g.id), label: g.name }))}
            />

            <CustomSelect
              label="Status"
              placeholder="All Statuses"
              value={filters.employmentStatus || ''}
              onChange={(e) => filter('employmentStatus', e.target.value)}
              options={[
                { value: 'ACTIVE', label: 'Active' },
                { value: 'PROBATION', label: 'Probation' },
                { value: 'NOTICE', label: 'Notice Period' },
                { value: 'SUSPENDED', label: 'Suspended' },
                { value: 'TERMINATED', label: 'Terminated' },
              ]}
            />

            {hasSearch && (
              <button type="button" className="button-clear" onClick={resetFilters}>
                Clear filters
              </button>
            )}
          </form>

          <div className="toolbar-actions-wrap flex items-center gap-3">
            <div className="view-mode-toggle" role="group" aria-label="View Mode">
              <button
                type="button"
                className={`toggle-btn ${viewMode === 'table' ? 'active' : ''}`}
                onClick={() => setViewMode('table')}
                title="Table View"
              >
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M4 6h16M4 10h16M4 14h16M4 18h16"/></svg>
                Table
              </button>
              <button
                type="button"
                className={`toggle-btn ${viewMode === 'grid' ? 'active' : ''}`}
                onClick={() => setViewMode('grid')}
                title="Cards Grid View"
              >
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"><path d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z"/></svg>
                Cards
              </button>
            </div>
          </div>
        </div>

        {selectedIds.length > 0 && (
          <div
            className="directory-selection-banner"
            style={{
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'space-between',
              padding: '10px 16px',
              marginBottom: '16px',
              backgroundColor: '#eff6ff',
              border: '1px solid #bfdbfe',
              borderRadius: '8px',
              color: '#1e40af',
              fontSize: '13px',
              fontWeight: 500,
            }}
          >
            <span>
              <strong>{selectedIds.length}</strong> employee{selectedIds.length === 1 ? '' : 's'} selected
            </span>
            <div style={{ display: 'flex', gap: '8px' }}>
              {isAdminOrHr && (can('EMPLOYEE_UPDATE') || management) && (
                <button
                  type="button"
                  className="primary"
                  style={{ padding: '4px 12px', fontSize: '12px', height: '28px', cursor: 'pointer' }}
                  onClick={() => setBulkUpdating(true)}
                >
                  Bulk Update ({selectedIds.length})
                </button>
              )}
              <button
                type="button"
                className="btn-secondary"
                style={{ padding: '4px 10px', fontSize: '12px', height: '28px', cursor: 'pointer' }}
                onClick={handleDownloadCSV}
              >
                Export Selected ({selectedIds.length})
              </button>
              <button
                type="button"
                className="button-clear"
                style={{ padding: '4px 10px', fontSize: '12px', height: '28px', cursor: 'pointer' }}
                onClick={() => setSelectedIds([])}
              >
                Deselect All
              </button>
            </div>
          </div>
        )}

        <ErrorState error={refs.error} />

        <QueryState query={query}>
          {items.length === 0 ? (
            <EmptyState title="No employees found" variant="search">
              {hasSearch ? (
                <>
                  Try adjusting your search criteria or{' '}
                  <button type="button" className="link-button" onClick={resetFilters}>
                    clear active filters
                  </button>
                  .
                </>
              ) : (
                'No employees have been added to the organization yet.'
              )}
            </EmptyState>
          ) : viewMode === 'table' ? (
            <div
              className="employee-table-scroll"
              role="region"
              aria-label="Employee search results"
              tabIndex={0}
            >
              <table className="employee-history-table">
                <thead>
                  <tr>
                    <th scope="col" style={{ width: '36px' }}>
                      <input
                        type="checkbox"
                        aria-label="Select all employees"
                        checked={isAllSelected}
                        onChange={toggleSelectAll}
                      />
                    </th>
                    <th scope="col">Employee</th>
                    <th scope="col">Code</th>
                    <th scope="col">Grade</th>
                    <th scope="col">Department</th>
                    <th scope="col">Status</th>
                    <th scope="col" className="text-right">Action</th>
                  </tr>
                </thead>
                <tbody>
                  {items.map((e) => {
                    const avatarColor = getAvatarColor(`${e.firstName || ''} ${e.lastName || ''}`);
                    return (
                      <tr key={e.id} className={selectedIds.includes(e.id) ? 'selected-row' : ''}>
                        <td>
                          <input
                            type="checkbox"
                            aria-label={`Select ${e.firstName} ${e.lastName}`}
                            checked={selectedIds.includes(e.id)}
                            onChange={() => toggleSelect(e.id)}
                          />
                        </td>
                        <td>
                          <div className="employee-cell-wrap">
                            <EmployeePhoto
                              src={e.profilePhotoUrl}
                              alt={`${e.firstName} ${e.lastName}`}
                              initials={getInitials(e.firstName, e.lastName)}
                              className="employee-avatar-img"
                              style={{ backgroundColor: avatarColor.bg, color: avatarColor.text }}
                            />
                            <div className="employee-name-block">
                              {management ? (
                                <Link
                                  to={`/employees/admin/${e.id}`}
                                  className="employee-name-link"
                                >
                                  {e.preferredName || e.firstName} {e.lastName}
                                </Link>
                              ) : (
                                <span className="employee-name-text">
                                  {e.preferredName || e.firstName} {e.lastName}
                                </span>
                              )}
                              {e.preferredName && (
                                <span className="employee-sub-name">({e.firstName})</span>
                              )}
                            </div>
                          </div>
                        </td>
                        <td>
                          <span className="code-tag">{e.employeeCode || '—'}</span>
                        </td>
                        <td>{name(e.gradeId)}</td>
                        <td>{name(e.departmentId)}</td>
                        <td>
                          <Badge value={e.employmentStatus} />
                        </td>
                        <td className="text-right">
                          <Link
                            to={management ? `/employees/admin/${e.id}` : `/employees/${e.id}`}
                            className="view-profile-btn"
                          >
                            View Profile &rarr;
                          </Link>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : (
            /* Cards Grid View */
            <div className="employee-directory-cards">
              {items.map((e) => {
                const avatarColor = getAvatarColor(`${e.firstName || ''} ${e.lastName || ''}`);
                return (
                  <div key={e.id} className={`employee-grid-card ${selectedIds.includes(e.id) ? 'selected-card' : ''}`}>
                    <div className="card-top">
                      <div className="card-avatar-wrap">
                        <input
                          type="checkbox"
                          aria-label={`Select ${e.firstName} ${e.lastName}`}
                          checked={selectedIds.includes(e.id)}
                          onChange={() => toggleSelect(e.id)}
                          style={{ marginRight: '6px' }}
                        />
                        <EmployeePhoto
                          src={e.profilePhotoUrl}
                          alt={`${e.firstName} ${e.lastName}`}
                          initials={getInitials(e.firstName, e.lastName)}
                          className="employee-avatar-img lg"
                          style={{ backgroundColor: avatarColor.bg, color: avatarColor.text }}
                        />
                        <div>
                          <Link
                            to={management ? `/employees/admin/${e.id}` : `/employees/${e.id}`}
                            className="card-name"
                          >
                            {e.preferredName || e.firstName} {e.lastName}
                          </Link>
                          <div className="card-code">{e.employeeCode || '—'}</div>
                        </div>
                      </div>
                      <Badge value={e.employmentStatus} />
                    </div>

                    <div className="card-details">
                      <div className="card-detail-item">
                        <span className="label">Grade:</span>
                        <span className="val">{name(e.gradeId)}</span>
                      </div>
                      <div className="card-detail-item">
                        <span className="label">Department:</span>
                        <span className="val">{name(e.departmentId)}</span>
                      </div>
                    </div>

                    <div className="card-footer">
                      <Link
                        to={management ? `/employees/admin/${e.id}` : `/employees/${e.id}`}
                        className="card-action"
                      >
                        View Profile &rarr;
                      </Link>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {query.data && (
            <Pagination
              data={query.data}
              busy={query.isFetching}
              onPage={(page) => setFilters((f) => ({ ...f, page }))}
            />
          )}
        </QueryState>
      </section>

      {isAdminOrHr && bulkUpdating && (
        <BulkUpdateModal
          selectedIds={selectedIds}
          items={items}
          onClose={() => setBulkUpdating(false)}
          onSaved={() => {
            setSelectedIds([]);
            query.refetch();
          }}
        />
      )}
    </div>
  );
}

function BulkUpdateModal({ selectedIds = [], items = [], onClose, onSaved }) {
  const [saveEmployee] = useSaveEmployeeMutation();
  const catalogQuery = useEmployeeCareerCatalogQuery();
  const refsQuery = useEmployeeReferencesQuery();
  const managersQuery = useEmployeeManagersQuery();

  const [updateDept, setUpdateDept] = useState(false);
  const [departmentVal, setDepartmentVal] = useState('');

  const [updateGrade, setUpdateGrade] = useState(false);
  const [gradeVal, setGradeVal] = useState('');

  const [updateJobTitle, setUpdateJobTitle] = useState(false);
  const [jobTitleVal, setJobTitleVal] = useState('');

  const [updateManager, setUpdateManager] = useState(false);
  const [managerVal, setManagerVal] = useState('');

  const [updateEmpType, setUpdateEmpType] = useState(false);
  const [empTypeVal, setEmpTypeVal] = useState('');

  const [updateEmpStatus, setUpdateEmpStatus] = useState(false);
  const [empStatusVal, setEmpStatusVal] = useState('');

  const [updateLocation, setUpdateLocation] = useState(false);
  const [locationVal, setLocationVal] = useState('');

  const [updatePromotionDate, setUpdatePromotionDate] = useState(false);
  const [promotionDateVal, setPromotionDateVal] = useState(() => new Date().toISOString().slice(0, 10));
  const [promotionNoteVal, setPromotionNoteVal] = useState('');

  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState(null);
  const [errorMsg, setErrorMsg] = useState(null);

  const departmentOptions = useMemo(() => {
    const list = [];
    const seen = new Set();
    for (const p of catalogQuery.data?.profiles || []) {
      if (!seen.has(p.name)) {
        seen.add(p.name);
        list.push({ value: `profile:${p.id}`, label: p.name, rawId: p.id, type: 'profile' });
      }
    }
    for (const r of (refsQuery.data || []).filter((x) => x.kind === 'DEPARTMENT')) {
      if (!seen.has(r.name)) {
        seen.add(r.name);
        list.push({ value: `reference:${r.id}`, label: r.name, rawId: r.id, type: 'reference' });
      }
    }
    return list;
  }, [catalogQuery.data?.profiles, refsQuery.data]);

  const gradeOptions = useMemo(() => {
    const list = [];
    const seen = new Set();
    for (const g of catalogQuery.data?.grades || []) {
      const label = `${g.code}${g.name && g.name !== g.code ? ` — ${g.name}` : ''}`;
      if (!seen.has(g.code)) {
        seen.add(g.code);
        list.push({ value: String(g.id), label, rawId: g.id, type: 'catalog' });
      }
    }
    for (const r of (refsQuery.data || []).filter((x) => x.kind === 'GRADE')) {
      if (!seen.has(r.name)) {
        seen.add(r.name);
        list.push({ value: `reference:${r.id}`, label: r.name, rawId: r.id, type: 'reference' });
      }
    }
    return list;
  }, [catalogQuery.data?.grades, refsQuery.data]);

  const jobTitleOptions = useMemo(() => {
    const list = [];
    const seen = new Set();
    for (const l of catalogQuery.data?.levels || []) {
      if (l.careerTitle && !seen.has(l.careerTitle.toLowerCase())) {
        seen.add(l.careerTitle.toLowerCase());
        list.push({
          value: `level:${l.id}`,
          label: l.careerTitle,
          rawId: l.id,
          careerProfileId: l.careerProfileId,
          gradeId: l.gradeId,
          type: 'level',
        });
      }
    }
    for (const r of (refsQuery.data || []).filter((x) => x.kind === 'JOB_TITLE')) {
      if (!seen.has(r.name.toLowerCase())) {
        seen.add(r.name.toLowerCase());
        list.push({ value: `reference:${r.id}`, label: r.name, rawId: r.id, type: 'reference' });
      }
    }
    return list;
  }, [catalogQuery.data?.levels, refsQuery.data]);

  const managerOptions = useMemo(() => {
    return (managersQuery.data || []).map((m) => {
      const name = m.name || `${m.firstName || ''} ${m.lastName || ''}`.trim();
      const label = m.employeeCode ? `${name} · ${m.employeeCode}` : (name || `Manager (${m.id})`);
      return {
        value: String(m.id),
        label,
      };
    });
  }, [managersQuery.data]);

  const handleSubmit = async (e) => {
    e.preventDefault();
    if (!selectedIds.length || busy) return;

    if (!updateDept && !updateGrade && !updateJobTitle && !updateManager && !updateEmpType && !updateEmpStatus && !updateLocation && !updatePromotionDate) {
      setErrorMsg('Please check at least one field to update.');
      return;
    }

    setBusy(true);
    setErrorMsg(null);

    const selectedEmployees = items.filter((emp) => selectedIds.includes(emp.id));
    let successCount = 0;

    for (let i = 0; i < selectedEmployees.length; i++) {
      const emp = selectedEmployees[i];
      setProgress({ current: i + 1, total: selectedEmployees.length, name: `${emp.firstName} ${emp.lastName}`.trim() });

      let careerProfileId = emp.careerProfileId || null;
      let catalogDepartmentId = emp.catalogDepartmentId || null;
      let departmentId = emp.departmentId || null;

      if (updateDept && departmentVal) {
        if (departmentVal.startsWith('profile:')) {
          careerProfileId = Number(departmentVal.split(':')[1]);
          catalogDepartmentId = careerProfileId;
          departmentId = null;
        } else if (departmentVal.startsWith('reference:')) {
          departmentId = Number(departmentVal.split(':')[1]);
          careerProfileId = null;
          catalogDepartmentId = null;
        }
      }

      let gradeId = emp.gradeId || null;
      let catalogGradeId = emp.catalogGradeId || null;

      if (updateGrade && gradeVal) {
        if (gradeVal.startsWith('reference:')) {
          gradeId = Number(gradeVal.split(':')[1]);
          catalogGradeId = null;
        } else {
          catalogGradeId = Number(gradeVal);
          gradeId = null;
        }
      }

      let careerLevelId = emp.careerLevelId || null;
      let jobTitleId = emp.jobTitleId || null;

      if (updateJobTitle && jobTitleVal) {
        if (jobTitleVal.startsWith('level:')) {
          careerLevelId = Number(jobTitleVal.split(':')[1]);
          jobTitleId = null;
          const matchedLevel = jobTitleOptions.find((j) => j.type === 'level' && j.rawId === careerLevelId);
          if (matchedLevel?.careerProfileId && !careerProfileId) {
            careerProfileId = matchedLevel.careerProfileId;
            catalogDepartmentId = matchedLevel.careerProfileId;
          }
          if (matchedLevel?.gradeId && !catalogGradeId) {
            catalogGradeId = matchedLevel.gradeId;
          }
        } else if (jobTitleVal.startsWith('reference:')) {
          jobTitleId = Number(jobTitleVal.split(':')[1]);
          careerLevelId = null;
        }
      }

      const managerEmployeeId = updateManager
        ? (managerVal ? Number(managerVal) : null)
        : (emp.managerEmployeeId ? Number(emp.managerEmployeeId) : null);

      const employmentType = updateEmpType ? (empTypeVal || emp.employmentType || 'FULL_TIME') : (emp.employmentType || 'FULL_TIME');
      const employmentStatus = updateEmpStatus ? (empStatusVal || emp.employmentStatus || 'ACTIVE') : (emp.employmentStatus || 'ACTIVE');
      const location = updateLocation ? locationVal : (emp.location || '');

      const careerEffectiveDate = updatePromotionDate
        ? (promotionDateVal || new Date().toISOString().slice(0, 10))
        : (updateGrade || updateJobTitle || updateDept ? new Date().toISOString().slice(0, 10) : null);

      const careerChangeNote = updatePromotionDate && promotionNoteVal
        ? promotionNoteVal.trim()
        : (updateGrade || updateJobTitle || updateDept ? 'Promotion / career update' : null);

      const body = {
        userId: Number(emp.userId),
        employeeCode: emp.employeeCode || `EMP-${emp.userId}`,
        firstName: emp.firstName || '',
        lastName: emp.lastName || '',
        preferredName: emp.preferredName || null,
        joiningDate: emp.joiningDate ? String(emp.joiningDate).slice(0, 10) : new Date().toISOString().slice(0, 10),
        endDate: emp.endDate ? String(emp.endDate).slice(0, 10) : null,
        departmentId,
        catalogDepartmentId,
        careerProfileId,
        gradeId,
        catalogGradeId,
        jobTitleId,
        careerLevelId,
        managerEmployeeId,
        employmentType,
        employmentStatus,
        location,
        phone: emp.phone || '',
        profilePhotoUrl: emp.profilePhotoUrl || null,
        careerEffectiveDate,
        careerChangeNote,
      };

      try {
        await saveEmployee({
          path: `/${emp.id}`,
          method: 'PUT',
          body,
          version: emp.version,
        }).unwrap();
        successCount++;
      } catch {
        /* Continue next */
      }
    }

    setBusy(false);
    setProgress(null);

    if (successCount > 0) {
      onSaved?.();
      onClose?.();
    } else {
      setErrorMsg('Failed to update selected employee profiles. Please try again.');
    }
  };

  return (
    <div className="modal-overlay" style={{
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      bottom: 0,
      backgroundColor: 'rgba(15, 23, 42, 0.6)',
      backdropFilter: 'blur(4px)',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      zIndex: 1000,
      padding: '20px'
    }}>
      <div className="panel" style={{
        width: '100%',
        maxWidth: '540px',
        backgroundColor: '#fff',
        borderRadius: '12px',
        boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 8px 10px -6px rgba(0,0,0,0.1)',
        padding: '24px',
        maxHeight: '90vh',
        overflowY: 'auto'
      }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '16px' }}>
          <div>
            <h2 style={{ fontSize: '18px', fontWeight: 600, color: '#0f172a', margin: 0 }}>
              Bulk update {selectedIds.length} employee{selectedIds.length === 1 ? '' : 's'}
            </h2>
            <p style={{ fontSize: '13px', color: '#64748b', margin: '4px 0 0 0' }}>
              Check the fields you want to update across all selected profiles.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            style={{ border: 'none', background: 'none', cursor: 'pointer', color: '#94a3b8', fontSize: '18px' }}
          >
            ✕
          </button>
        </div>

        {errorMsg && (
          <div className="error" role="alert" style={{ marginBottom: '16px' }}>
            <p>{errorMsg}</p>
          </div>
        )}

        {progress ? (
          <div style={{ padding: '24px 0', textAlign: 'center' }}>
            <div className="loading" role="status" style={{ marginBottom: '12px' }} />
            <p style={{ fontSize: '14px', fontWeight: 500, color: '#1e293b' }}>
              Updating {progress.current} of {progress.total}: {progress.name}...
            </p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
            {/* Department field */}
            <div
              style={{
                background: updateDept ? '#fffbeb' : '#f8fafc',
                border: updateDept ? '1px solid #f59e0b' : '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '12px 14px',
                transition: 'all 0.15s ease',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}
            >
              <label
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '10px',
                  cursor: 'pointer',
                  margin: 0,
                  padding: 0,
                  width: 'auto',
                  maxWidth: 'max-content',
                  userSelect: 'none'
                }}
              >
                <input
                  type="checkbox"
                  checked={updateDept}
                  onChange={(e) => setUpdateDept(e.target.checked)}
                  style={{
                    width: '18px',
                    height: '18px',
                    minWidth: '18px',
                    minHeight: '18px',
                    maxWidth: '18px',
                    maxHeight: '18px',
                    accentColor: '#f59e0b',
                    margin: 0,
                    padding: 0,
                    cursor: 'pointer',
                    flexShrink: 0,
                    appearance: 'checkbox',
                    WebkitAppearance: 'checkbox'
                  }}
                />
                <span style={{ fontSize: '13px', fontWeight: 600, color: '#1e293b' }}>
                  Update Department
                </span>
              </label>
              {updateDept && (
                <div style={{ width: '100%', marginTop: '4px' }}>
                  <CustomSelect
                    placeholder="Select department"
                    value={departmentVal}
                    onChange={(e) => setDepartmentVal(e.target.value)}
                    options={departmentOptions}
                  />
                </div>
              )}
            </div>

            {/* Grade field */}
            <div
              style={{
                background: updateGrade ? '#fffbeb' : '#f8fafc',
                border: updateGrade ? '1px solid #f59e0b' : '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '12px 14px',
                transition: 'all 0.15s ease',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}
            >
              <label
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '10px',
                  cursor: 'pointer',
                  margin: 0,
                  padding: 0,
                  width: 'auto',
                  maxWidth: 'max-content',
                  userSelect: 'none'
                }}
              >
                <input
                  type="checkbox"
                  checked={updateGrade}
                  onChange={(e) => setUpdateGrade(e.target.checked)}
                  style={{
                    width: '18px',
                    height: '18px',
                    minWidth: '18px',
                    minHeight: '18px',
                    maxWidth: '18px',
                    maxHeight: '18px',
                    accentColor: '#f59e0b',
                    margin: 0,
                    padding: 0,
                    cursor: 'pointer',
                    flexShrink: 0,
                    appearance: 'checkbox',
                    WebkitAppearance: 'checkbox'
                  }}
                />
                <span style={{ fontSize: '13px', fontWeight: 600, color: '#1e293b' }}>
                  Update Grade
                </span>
              </label>
              {updateGrade && (
                <div style={{ width: '100%', marginTop: '4px' }}>
                  <CustomSelect
                    placeholder="Select grade"
                    value={gradeVal}
                    onChange={(e) => setGradeVal(e.target.value)}
                    options={gradeOptions}
                  />
                </div>
              )}
            </div>

            {/* Job Title field */}
            <div
              style={{
                background: updateJobTitle ? '#fffbeb' : '#f8fafc',
                border: updateJobTitle ? '1px solid #f59e0b' : '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '12px 14px',
                transition: 'all 0.15s ease',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}
            >
              <label
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '10px',
                  cursor: 'pointer',
                  margin: 0,
                  padding: 0,
                  width: 'auto',
                  maxWidth: 'max-content',
                  userSelect: 'none'
                }}
              >
                <input
                  type="checkbox"
                  checked={updateJobTitle}
                  onChange={(e) => setUpdateJobTitle(e.target.checked)}
                  style={{
                    width: '18px',
                    height: '18px',
                    minWidth: '18px',
                    minHeight: '18px',
                    maxWidth: '18px',
                    maxHeight: '18px',
                    accentColor: '#f59e0b',
                    margin: 0,
                    padding: 0,
                    cursor: 'pointer',
                    flexShrink: 0,
                    appearance: 'checkbox',
                    WebkitAppearance: 'checkbox'
                  }}
                />
                <span style={{ fontSize: '13px', fontWeight: 600, color: '#1e293b' }}>
                  Update Job Title
                </span>
              </label>
              {updateJobTitle && (
                <div style={{ width: '100%', marginTop: '4px' }}>
                  <CustomSelect
                    placeholder="Select job title"
                    value={jobTitleVal}
                    onChange={(e) => setJobTitleVal(e.target.value)}
                    options={jobTitleOptions}
                  />
                </div>
              )}
            </div>

            {/* Manager field */}
            <div
              style={{
                background: updateManager ? '#fffbeb' : '#f8fafc',
                border: updateManager ? '1px solid #f59e0b' : '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '12px 14px',
                transition: 'all 0.15s ease',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}
            >
              <label
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '10px',
                  cursor: 'pointer',
                  margin: 0,
                  padding: 0,
                  width: 'auto',
                  maxWidth: 'max-content',
                  userSelect: 'none'
                }}
              >
                <input
                  type="checkbox"
                  checked={updateManager}
                  onChange={(e) => setUpdateManager(e.target.checked)}
                  style={{
                    width: '18px',
                    height: '18px',
                    minWidth: '18px',
                    minHeight: '18px',
                    maxWidth: '18px',
                    maxHeight: '18px',
                    accentColor: '#f59e0b',
                    margin: 0,
                    padding: 0,
                    cursor: 'pointer',
                    flexShrink: 0,
                    appearance: 'checkbox',
                    WebkitAppearance: 'checkbox'
                  }}
                />
                <span style={{ fontSize: '13px', fontWeight: 600, color: '#1e293b' }}>
                  Update Reporting Manager
                </span>
              </label>
              {updateManager && (
                <div style={{ width: '100%', marginTop: '4px' }}>
                  <CustomSelect
                    placeholder="Select manager"
                    value={managerVal}
                    onChange={(e) => setManagerVal(e.target.value)}
                    options={managerOptions}
                  />
                </div>
              )}
            </div>

            {/* Employment Type */}
            <div
              style={{
                background: updateEmpType ? '#fffbeb' : '#f8fafc',
                border: updateEmpType ? '1px solid #f59e0b' : '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '12px 14px',
                transition: 'all 0.15s ease',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}
            >
              <label
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '10px',
                  cursor: 'pointer',
                  margin: 0,
                  padding: 0,
                  width: 'auto',
                  maxWidth: 'max-content',
                  userSelect: 'none'
                }}
              >
                <input
                  type="checkbox"
                  checked={updateEmpType}
                  onChange={(e) => setUpdateEmpType(e.target.checked)}
                  style={{
                    width: '18px',
                    height: '18px',
                    minWidth: '18px',
                    minHeight: '18px',
                    maxWidth: '18px',
                    maxHeight: '18px',
                    accentColor: '#f59e0b',
                    margin: 0,
                    padding: 0,
                    cursor: 'pointer',
                    flexShrink: 0,
                    appearance: 'checkbox',
                    WebkitAppearance: 'checkbox'
                  }}
                />
                <span style={{ fontSize: '13px', fontWeight: 600, color: '#1e293b' }}>
                  Update Employment Type
                </span>
              </label>
              {updateEmpType && (
                <div style={{ width: '100%', marginTop: '4px' }}>
                  <CustomSelect
                    placeholder="Select employment type"
                    value={empTypeVal}
                    onChange={(e) => setEmpTypeVal(e.target.value)}
                    options={[
                      { value: 'FULL_TIME', label: 'Full time' },
                      { value: 'PART_TIME', label: 'Part time' },
                      { value: 'CONTRACT', label: 'Contract' },
                      { value: 'INTERN', label: 'Intern' },
                    ]}
                  />
                </div>
              )}
            </div>

            {/* Employment Status */}
            <div
              style={{
                background: updateEmpStatus ? '#fffbeb' : '#f8fafc',
                border: updateEmpStatus ? '1px solid #f59e0b' : '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '12px 14px',
                transition: 'all 0.15s ease',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}
            >
              <label
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '10px',
                  cursor: 'pointer',
                  margin: 0,
                  padding: 0,
                  width: 'auto',
                  maxWidth: 'max-content',
                  userSelect: 'none'
                }}
              >
                <input
                  type="checkbox"
                  checked={updateEmpStatus}
                  onChange={(e) => setUpdateEmpStatus(e.target.checked)}
                  style={{
                    width: '18px',
                    height: '18px',
                    minWidth: '18px',
                    minHeight: '18px',
                    maxWidth: '18px',
                    maxHeight: '18px',
                    accentColor: '#f59e0b',
                    margin: 0,
                    padding: 0,
                    cursor: 'pointer',
                    flexShrink: 0,
                    appearance: 'checkbox',
                    WebkitAppearance: 'checkbox'
                  }}
                />
                <span style={{ fontSize: '13px', fontWeight: 600, color: '#1e293b' }}>
                  Update Employment Status
                </span>
              </label>
              {updateEmpStatus && (
                <div style={{ width: '100%', marginTop: '4px' }}>
                  <CustomSelect
                    placeholder="Select employment status"
                    value={empStatusVal}
                    onChange={(e) => setEmpStatusVal(e.target.value)}
                    options={[
                      { value: 'ACTIVE', label: 'Active' },
                      { value: 'PROBATION', label: 'Probation' },
                      { value: 'NOTICE', label: 'Notice Period' },
                      { value: 'SUSPENDED', label: 'Suspended' },
                      { value: 'TERMINATED', label: 'Terminated' },
                    ]}
                  />
                </div>
              )}
            </div>

            {/* Location */}
            <div
              style={{
                background: updateLocation ? '#fffbeb' : '#f8fafc',
                border: updateLocation ? '1px solid #f59e0b' : '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '12px 14px',
                transition: 'all 0.15s ease',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}
            >
              <label
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '10px',
                  cursor: 'pointer',
                  margin: 0,
                  padding: 0,
                  width: 'auto',
                  maxWidth: 'max-content',
                  userSelect: 'none'
                }}
              >
                <input
                  type="checkbox"
                  checked={updateLocation}
                  onChange={(e) => setUpdateLocation(e.target.checked)}
                  style={{
                    width: '18px',
                    height: '18px',
                    minWidth: '18px',
                    minHeight: '18px',
                    maxWidth: '18px',
                    maxHeight: '18px',
                    accentColor: '#f59e0b',
                    margin: 0,
                    padding: 0,
                    cursor: 'pointer',
                    flexShrink: 0,
                    appearance: 'checkbox',
                    WebkitAppearance: 'checkbox'
                  }}
                />
                <span style={{ fontSize: '13px', fontWeight: 600, color: '#1e293b' }}>
                  Update Location
                </span>
              </label>
              {updateLocation && (
                <div style={{ width: '100%', marginTop: '4px' }}>
                  <input
                    type="text"
                    placeholder="e.g. Headquarters, Remote"
                    value={locationVal}
                    onChange={(e) => setLocationVal(e.target.value)}
                    style={{
                      width: '100%',
                      padding: '8px 12px',
                      borderRadius: '6px',
                      border: '1px solid #cbd5e1',
                      fontSize: '13px'
                    }}
                  />
                </div>
              )}
            </div>

            {/* Promotion / Effective Date */}
            <div
              style={{
                background: updatePromotionDate ? '#fffbeb' : '#f8fafc',
                border: updatePromotionDate ? '1px solid #f59e0b' : '1px solid #e2e8f0',
                borderRadius: '8px',
                padding: '12px 14px',
                transition: 'all 0.15s ease',
                display: 'flex',
                flexDirection: 'column',
                gap: '8px'
              }}
            >
              <label
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '10px',
                  cursor: 'pointer',
                  margin: 0,
                  padding: 0,
                  width: 'auto',
                  maxWidth: 'max-content',
                  userSelect: 'none'
                }}
              >
                <input
                  type="checkbox"
                  checked={updatePromotionDate}
                  onChange={(e) => setUpdatePromotionDate(e.target.checked)}
                  style={{
                    width: '18px',
                    height: '18px',
                    minWidth: '18px',
                    minHeight: '18px',
                    maxWidth: '18px',
                    maxHeight: '18px',
                    accentColor: '#f59e0b',
                    margin: 0,
                    padding: 0,
                    cursor: 'pointer',
                    flexShrink: 0,
                    appearance: 'checkbox',
                    WebkitAppearance: 'checkbox'
                  }}
                />
                <span style={{ fontSize: '13px', fontWeight: 600, color: '#1e293b' }}>
                  Update Promotion / Effective Date
                </span>
              </label>
              {updatePromotionDate && (
                <div style={{ width: '100%', marginTop: '4px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, color: '#475569', marginBottom: '4px' }}>
                      Effective Date
                    </label>
                    <input
                      type="date"
                      value={promotionDateVal}
                      onChange={(e) => setPromotionDateVal(e.target.value)}
                      style={{
                        width: '100%',
                        padding: '8px 12px',
                        borderRadius: '6px',
                        border: '1px solid #cbd5e1',
                        fontSize: '13px'
                      }}
                    />
                  </div>
                  <div>
                    <label style={{ display: 'block', fontSize: '12px', fontWeight: 500, color: '#475569', marginBottom: '4px' }}>
                      Change Note / Reason (Optional)
                    </label>
                    <input
                      type="text"
                      placeholder="e.g. Annual Promotion Cycle"
                      value={promotionNoteVal}
                      onChange={(e) => setPromotionNoteVal(e.target.value)}
                      style={{
                        width: '100%',
                        padding: '8px 12px',
                        borderRadius: '6px',
                        border: '1px solid #cbd5e1',
                        fontSize: '13px'
                      }}
                    />
                  </div>
                </div>
              )}
            </div>

            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '12px', marginTop: '16px' }}>
              <button
                type="button"
                className="button-clear"
                onClick={onClose}
                disabled={busy}
              >
                Cancel
              </button>
              <button
                type="submit"
                className="primary"
                disabled={busy}
              >
                Apply Updates
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
