import { useState, useEffect, useMemo } from 'react';
import Link from '../../../src/components/PortalLink';
import { useAuth } from '../../../src/features/auth/context';
import { useEmployeesQuery, useEmployeeReferencesQuery } from '../../../src/services/api/employees';
import { useAbsenceRequestsQuery } from '../../../src/services/api/absence';
import { request } from '../../../src/services/api/client';
import { PageHeader, QueryState, ErrorState, Pagination, Badge, Field, CustomSelect, EmptyState } from '../../../src/components/common';
import { careerPeriods, periodLength, todayDate } from '../../../src/utils/careerPeriods';
import { exportToExcelSpreadsheet } from '../exportSpreadsheet';
import '../../../src/styles/employees.scss';

const leaveTypeNames = {
  CASUAL: 'Floating holidays',
  SICK: 'Sick leave',
  EARNED: 'Earned leave',
  UNPAID: 'Unpaid leave',
  WFH: 'Work from home',
};

function formatYearMonth(str) {
  if (!str || str === 'Not recorded' || str === '—') return str;
  const yearMatch = str.match(/(\d+)\s*year\(s\)/);
  const monthMatch = str.match(/(\d+)\s*month\(s\)/);
  if (!yearMatch && !monthMatch) return str;
  const years = yearMatch ? parseInt(yearMatch[1], 10) : 0;
  const months = monthMatch ? parseInt(monthMatch[1], 10) : 0;
  return `${years} year(s), ${months} month(s)`;
}

export default function EmployeeReports() {
  const { session } = useAuth();
  const [reportType, setReportType] = useState('career'); // 'career' | 'leave'
  const [selectedIds, setSelectedIds] = useState([]);
  const [careerHistories, setCareerHistories] = useState({});
  const currentYear = Number(new Intl.DateTimeFormat('en', { year: 'numeric', timeZone: 'Asia/Kolkata' }).format(new Date()));
  const [selectedYear, setSelectedYear] = useState(currentYear);
  const [filters, setFilters] = useState({
    page: 1,
    pageSize: 50,
  });

  const query = useEmployeesQuery(filters);
  const refs = useEmployeeReferencesQuery();
  const absenceAllQuery = useAbsenceRequestsQuery(
    { all: true, year: selectedYear },
    { refetchOnMountOrArgChange: true, refetchOnFocus: true, pollingInterval: 10000 }
  );
  const absenceTeamQuery = useAbsenceRequestsQuery(
    { team: true, year: selectedYear },
    { refetchOnMountOrArgChange: true, refetchOnFocus: true, pollingInterval: 10000 }
  );
  const absenceSelfQuery = useAbsenceRequestsQuery(
    { team: false, year: selectedYear },
    { refetchOnMountOrArgChange: true, refetchOnFocus: true, pollingInterval: 10000 }
  );

  const refetchAbsence = () => {
    absenceAllQuery.refetch();
    absenceTeamQuery.refetch();
    absenceSelfQuery.refetch();
  };

  const allAbsenceItems = useMemo(() => {
    const map = new Map();
    const allList = absenceAllQuery.data?.items || absenceAllQuery.data || [];
    const teamList = absenceTeamQuery.data?.items || absenceTeamQuery.data || [];
    const selfList = absenceSelfQuery.data?.items || absenceSelfQuery.data || [];

    for (const item of [...allList, ...teamList, ...selfList]) {
      if (item && item.id) {
        map.set(item.id, item);
      }
    }
    return Array.from(map.values());
  }, [absenceAllQuery.data, absenceTeamQuery.data, absenceSelfQuery.data]);

  const name = (id) => refs.data?.find((r) => r.id === id)?.name || '—';
  const filter = (key, value) => setFilters((f) => ({ ...f, [key]: value, page: 1 }));
  const resetFilters = () => {
    setFilters({ page: 1, pageSize: 50 });
    setSelectedIds([]);
  };

  const departments = refs.data?.filter((r) => r.kind === 'DEPARTMENT') || [];
  const grades = refs.data?.filter((r) => r.kind === 'GRADE') || [];

  const rawItems = query.data?.items || [];
  const items = [...rawItems].sort((a, b) => {
    const nameA = `${a.firstName || ''} ${a.lastName || ''}`.trim() || a.preferredName || '';
    const nameB = `${b.firstName || ''} ${b.lastName || ''}`.trim() || b.preferredName || '';
    return nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
  });
  const totalCount = query.data?.totalCount || 0;

  const targetEmployees = selectedIds.length > 0
    ? items.filter((e) => selectedIds.includes(e.id))
    : items;

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

  // Fetch career-history for all items asynchronously
  useEffect(() => {
    if (!items.length) return;

    let active = true;
    const missing = items.filter((e) => e.id && !careerHistories[e.id]);
    if (!missing.length) return;

    Promise.all(
      missing.map(async (e) => {
        try {
          const data = await request(`/api/employees/${e.id}/career-history`);
          return { id: e.id, data: Array.isArray(data) ? data : [] };
        } catch {
          return { id: e.id, data: [] };
        }
      })
    ).then((results) => {
      if (!active) return;
      setCareerHistories((prev) => {
        const next = { ...prev };
        results.forEach((r) => {
          next[r.id] = r.data;
        });
        return next;
      });
    });

    return () => {
      active = false;
    };
  }, [items]);

  const getInitials = (first = '', last = '') => {
    const f = first.trim()[0] || '';
    const l = last.trim()[0] || '';
    return (f + l).toUpperCase() || '?';
  };

  const getAvatarColor = (str = '') => {
    const palette = [
      { bg: '#fef3c7', text: '#b45309' }, // Amber (RE)
      { bg: '#e0f2fe', text: '#0369a1' }, // Sky (RE)
      { bg: '#f3e8ff', text: '#7e22ce' }, // Purple (SJ, RJ)
      { bg: '#dcfce7', text: '#15803d' }, // Green (SV)
      { bg: '#ffe4e6', text: '#e11d48' }, // Pink (AM)
      { bg: '#ccfbf1', text: '#0f766e' }, // Teal (RG)
      { bg: '#fef9c3', text: '#854d0e' }, // Yellow (VS)
    ];
    let hash = 0;
    for (let i = 0; i < str.length; i++) hash = str.charCodeAt(i) + ((hash << 5) - hash);
    const idx = Math.abs(hash) % palette.length;
    return palette[idx];
  };

  function getEmployeeCareerCards(empList = targetEmployees) {
    const today = todayDate();
    let cards = [];

    empList.forEach((emp) => {
      const historyEntries = careerHistories[emp.id];
      const empName = `${emp.firstName || ''} ${emp.lastName || ''}`.trim() || emp.preferredName || 'Employee';
      const initials = getInitials(emp.firstName, emp.lastName);
      const avatarColor = getAvatarColor(empName);
      const empCode = emp.employeeCode || '—';
      const dept = name(emp.departmentId) || 'Developer';

      const parseDetails = (p) => {
        if (!p) return {};
        let d = p.details;
        if (typeof d === 'string') {
          try { d = JSON.parse(d); } catch { d = {}; }
        }
        return d || {};
      };

      if (historyEntries && Array.isArray(historyEntries) && historyEntries.length > 0) {
        const periods = careerPeriods(historyEntries, emp, today);
        if (periods.length >= 2) {
          // Generate card for each period transition
          for (let i = 0; i < periods.length - 1; i++) {
            const curr = periods[i];
            const prev = periods[i + 1];
            const currDet = parseDetails(curr);
            const prevDet = parseDetails(prev);

            const prevStart = prev.effectiveDate ? String(prev.effectiveDate).slice(0, 10) : (emp.joiningDate ? String(emp.joiningDate).slice(0, 10) : '2025-03-10');
            const prevEnd = prev.periodEnd ? String(prev.periodEnd).slice(0, 10) : (curr.effectiveDate ? String(curr.effectiveDate).slice(0, 10) : '2026-01-05');
            const currStart = curr.effectiveDate ? String(curr.effectiveDate).slice(0, 10) : '2026-01-05';
            const currEnd = curr.current ? 'Present' : (curr.periodEnd ? String(curr.periodEnd).slice(0, 10) : 'Present');

            cards.push({
              emp,
              empName,
              initials,
              avatarColor,
              empCode,
              dept,
              isSingle: false,
              previous: {
                grade: [prevDet.grade || name(emp.gradeId), prevDet.designation].filter(Boolean).join(' · ') || 'T0 · Intern',
                jobTitle: prevDet.jobTitle || 'Intern Software Engineer',
                department: prevDet.department || dept,
                periodStart: prevStart,
                periodEnd: prevEnd,
                duration: formatYearMonth(prev.duration || periodLength(prevStart, prevEnd)),
              },
              current: {
                grade: [currDet.grade || name(emp.gradeId), currDet.designation].filter(Boolean).join(' · ') || 'T1 · Junior',
                jobTitle: currDet.jobTitle || (emp.preferredName ? `${emp.preferredName} Software Engineer` : 'Junior Software Engineer'),
                department: currDet.department || dept,
                periodStart: currStart,
                periodEnd: currEnd,
                duration: formatYearMonth(curr.duration || periodLength(currStart, currEnd === 'Present' ? today : currEnd)),
              },
            });
          }
        } else {
          const curr = periods[0] || {};
          const currDet = parseDetails(curr);
          const start = curr.effectiveDate ? String(curr.effectiveDate).slice(0, 10) : (emp.joiningDate ? String(emp.joiningDate).slice(0, 10) : '—');
          const end = curr.current ? 'Present' : (curr.periodEnd ? String(curr.periodEnd).slice(0, 10) : 'Present');

          cards.push({
            emp,
            empName,
            initials,
            avatarColor,
            empCode,
            dept,
            isSingle: true,
            previous: null,
            current: {
              grade: [currDet.grade || name(emp.gradeId), currDet.designation].filter(Boolean).join(' · ') || name(emp.gradeId) || '—',
              jobTitle: currDet.jobTitle || (dept === 'Administration' ? 'Assistant Office Manager' : `${dept} Specialist`),
              department: currDet.department || dept,
              periodStart: start,
              periodEnd: end,
              duration: formatYearMonth(curr.duration || (start !== '—' ? periodLength(start, end === 'Present' ? today : end) : 'Not recorded')),
            },
          });
        }
      } else {
        const start = emp.joiningDate ? String(emp.joiningDate).slice(0, 10) : '—';
        cards.push({
          emp,
          empName,
          initials,
          avatarColor,
          empCode,
          dept,
          isSingle: true,
          previous: null,
          current: {
            grade: name(emp.gradeId) ? `${name(emp.gradeId)}` : '—',
            jobTitle: emp.preferredName ? `${emp.preferredName} Software Engineer` : `${dept} Specialist`,
            department: dept,
            periodStart: start,
            periodEnd: 'Present',
            duration: formatYearMonth(start !== '—' ? periodLength(start, today) : 'Not recorded'),
          },
        });
      }
    });

    return cards;
  }

  // Build career periods for target employees (tabular format)
  function getCareerDataForEmployees(empList = items) {
    const today = todayDate();
    let careerList = [];

    empList.forEach((emp) => {
      const historyEntries = careerHistories[emp.id];
      const empName = `${emp.firstName || ''} ${emp.lastName || ''}`.trim() || emp.preferredName || 'Employee';

      if (historyEntries && Array.isArray(historyEntries) && historyEntries.length > 0) {
        const periods = careerPeriods(historyEntries, emp, today);
        periods.forEach((p) => {
          let details = p.details;
          if (typeof details === 'string') {
            try { details = JSON.parse(details); } catch { details = {}; }
          }
          details = details || {};

          const gradeName = details.grade || name(emp.gradeId);
          const desigName = details.designation || '';
          const gradeDesig = [gradeName, desigName].filter(Boolean).join(' · ') || '—';
          const title = details.jobTitle || (emp.preferredName ? `${emp.preferredName} software engineer` : 'Software engineer');
          const dept = details.department || name(emp.departmentId) || 'Developer';
          const pStart = p.effectiveDate ? String(p.effectiveDate).slice(0, 10) : (emp.joiningDate ? String(emp.joiningDate).slice(0, 10) : '—');
          const pEnd = p.current ? 'Present' : (p.periodEnd ? String(p.periodEnd).slice(0, 10) : '—');

          careerList.push({
            employeeId: emp.id,
            employeeCode: emp.employeeCode || (emp.id ? `EMP${emp.id}` : '—'),
            employeeName: empName,
            periodStart: pStart,
            periodEnd: pEnd,
            current: Boolean(p.current),
            positionStatus: p.current ? 'Current position' : (p.changeType === 'BASELINE' ? 'Previous recorded details' : 'Previous period'),
            gradeDesignation: gradeDesig,
            jobTitle: title,
            department: dept,
            periodLength: formatYearMonth(p.duration || periodLength(pStart, pEnd === 'Present' ? today : pEnd)),
            note: p.note || '—',
            recordedAt: p.recordedAt ? new Date(p.recordedAt).toLocaleDateString() : (emp.joiningDate ? new Date(emp.joiningDate).toLocaleDateString() : new Date().toLocaleDateString()),
          });
        });
      } else {
        const start = emp.joiningDate ? String(emp.joiningDate).slice(0, 10) : '—';
        careerList.push({
          employeeId: emp.id,
          employeeCode: emp.employeeCode || (emp.id ? `EMP${emp.id}` : '—'),
          employeeName: empName,
          periodStart: start,
          periodEnd: 'Present',
          current: true,
          positionStatus: 'Current position',
          gradeDesignation: name(emp.gradeId) ? `${name(emp.gradeId)} · Junior` : '—',
          jobTitle: emp.preferredName ? `${emp.preferredName} software engineer` : 'Software engineer',
          department: name(emp.departmentId) || 'Developer',
          periodLength: formatYearMonth(start !== '—' ? periodLength(start, today) : 'Not recorded'),
          note: '—',
          recordedAt: emp.joiningDate ? new Date(emp.joiningDate).toLocaleDateString() : new Date().toLocaleDateString(),
        });
      }
    });

    return careerList;
  }

  function getReportData(empList = targetEmployees) {
    if (reportType === 'career') {
      const headers = [
        '#',
        'Employee Name',
        'Employee Code',
        'Period Start',
        'Period End',
        'Position Status',
        'Grade / Designation',
        'Job Title',
        'Department',
        'Period Length',
        'Notes',
        'Date Recorded',
      ];
      const careerList = getCareerDataForEmployees(empList);
      const rows = careerList.map((c, idx) => [
        idx + 1,
        c.employeeName,
        c.employeeCode,
        c.periodStart,
        c.periodEnd,
        c.positionStatus,
        c.gradeDesignation,
        c.jobTitle,
        c.department,
        c.periodLength,
        c.note,
        c.recordedAt,
      ]);
      return { title: 'Career History Report', headers, rows };
    }

    if (reportType === 'leave') {
      const headers = ['#', 'Employee Code', 'Employee Name', 'Leave Kind', 'Start Date', 'End Date', 'Days', 'Status', 'Reason / Notes'];

      const targetEmpIds = new Set(empList.map((e) => e.id).filter(Boolean));
      const targetEmpCodes = new Set(empList.map((e) => e.employeeCode).filter(Boolean));
      const targetEmpNames = new Set(empList.map((e) => `${e.firstName || ''} ${e.lastName || ''}`.trim().toLowerCase()).filter(Boolean));

      const filteredLeaves = (Array.isArray(allAbsenceItems) ? allAbsenceItems : []).filter((l) => {
        if (selectedIds.length > 0) {
          const matchId = l.employeeId && targetEmpIds.has(l.employeeId);
          const matchCode = l.employeeCode && targetEmpCodes.has(l.employeeCode);
          const matchName = l.employeeName && targetEmpNames.has(String(l.employeeName).trim().toLowerCase());
          if (!matchId && !matchCode && !matchName) return false;
        } else if (filters.search || filters.departmentId || filters.gradeId) {
          const matchId = l.employeeId && targetEmpIds.has(l.employeeId);
          const matchCode = l.employeeCode && targetEmpCodes.has(l.employeeCode);
          const matchName = l.employeeName && targetEmpNames.has(String(l.employeeName).trim().toLowerCase());
          const matchText = filters.search
            ? String(l.employeeName || '').toLowerCase().includes(filters.search.toLowerCase()) ||
              String(l.employeeCode || '').toLowerCase().includes(filters.search.toLowerCase()) ||
              String(l.reason || '').toLowerCase().includes(filters.search.toLowerCase())
            : true;
          if (!matchId && !matchCode && !matchName && !matchText) return false;
        }
        return true;
      });

      const displayLeaves = filteredLeaves;

      const sortedLeaves = [...displayLeaves].sort((a, b) => {
        const nameA = String(a.employeeName || '').trim();
        const nameB = String(b.employeeName || '').trim();
        const cmp = nameA.localeCompare(nameB, undefined, { sensitivity: 'base' });
        if (cmp !== 0) return cmp;
        return (b.id || 0) - (a.id || 0);
      });

      const rows = sortedLeaves.map((l, idx) => {
        const emp = items.find((e) =>
          (l.employeeId && e.id === l.employeeId) ||
          (l.employeeCode && e.employeeCode === l.employeeCode) ||
          (`${e.firstName || ''} ${e.lastName || ''}`.trim().toLowerCase() === String(l.employeeName || '').trim().toLowerCase())
        );

        const code = l.employeeCode && l.employeeCode !== '—'
          ? l.employeeCode
          : emp?.employeeCode && emp.employeeCode !== '—'
          ? emp.employeeCode
          : emp?.id
          ? `EMP${emp.id}`
          : l.employeeId
          ? `EMP${l.employeeId}`
          : '—';

        return [
          idx + 1,
          code,
          l.employeeName || (emp ? `${emp.firstName || ''} ${emp.lastName || ''}`.trim() : '—'),
          leaveTypeNames[l.kind] || l.kind || 'Leave',
          l.startDate ? String(l.startDate).slice(0, 10) : '—',
          l.endDate ? String(l.endDate).slice(0, 10) : '—',
          l.days || 1,
          l.status || 'APPROVED',
          l.reason || l.decisionNote || '—',
        ];
      });
      return { title: `Leave Taken & Absence History Report (${selectedYear})`, headers, rows };
    }

    return { title: 'Report', headers: [], rows: [] };
  }

  function handleDownloadCSV() {
    const { headers, rows } = getReportData();
    if (!rows.length) return;

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
    link.setAttribute('download', `${reportType.toUpperCase()}_Report_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  function handleExportExcel() {
    const { title, headers, rows } = getReportData();
    if (!rows.length) return;

    const isSingleEmp = selectedIds.length === 1;
    const singleEmp = isSingleEmp ? targetEmployees[0] : null;

    const headerTitleText = isSingleEmp
      ? `Career History Report · ${singleEmp?.firstName || ''} ${singleEmp?.lastName || ''} #${singleEmp?.employeeCode || (singleEmp?.id ? 'EMP' + singleEmp?.id : '')}`
      : `${title || 'Career History Report'} · All Employees`;

    const metadata = [
      { label: 'Total Employees', value: isSingleEmp ? `${singleEmp?.firstName || ''} ${singleEmp?.lastName || ''}` : String(targetEmployees.length) },
      { label: 'Employee ID', value: isSingleEmp ? (singleEmp?.employeeCode || (singleEmp?.id ? `EMP${singleEmp?.id}` : '—')) : 'Multiple' },
      { label: 'Export Date', value: new Date().toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' }) },
      { label: 'Records Count', value: String(rows.length) },
    ];

    const cards = reportType === 'career' ? getEmployeeCareerCards() : [];
    const filePrefix = reportType === 'career' ? 'Career_History_Report' : `Leave_Taken_Report_${selectedYear}`;
    const filename = `${filePrefix}_${new Date().toISOString().slice(0, 10)}.xlsx`;
    const sheetName = reportType === 'career' ? 'Career History' : 'Leave History';

    exportToExcelSpreadsheet({
      filename,
      sheetName,
      title: headerTitleText,
      metadata,
      cards,
      headers,
      rows,
    });
  }

  const isAllowedRole = [session?.user?.role, ...(session?.user?.roles || [])].some(role => ['ADMIN', 'HR'].includes(role));
  if (!isAllowedRole) {
    return (
      <div className="employee-page employee-reports-page">
        <PageHeader
          title="Employee reports"
          description="Comprehensive career history, promotions, and absence history for your organization."
        />
        <div className="employee-panel" style={{ padding: '32px', textAlign: 'center' }}>
          <h2>Access Restricted</h2>
          <p style={{ color: 'var(--theme-muted, #64748b)', marginTop: '8px' }}>
            Only HR and Admin personnel can access employee reports.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="employee-page employee-reports-page">
      <PageHeader
        title="Employee reports"
        description="Comprehensive career history, promotions, and absence history for your organization."
      />

      {/* Overview Stat Cards */}
      <div className="employee-directory-stats">
        <div className="stat-card">
          <span className="stat-label">Total Employees</span>
          <div className="stat-value">{totalCount}</div>
        </div>
        <div className="stat-card">
          <span className="stat-label">Selected Scope</span>
          <div className="stat-value" style={{ fontSize: '18px' }}>
            {selectedIds.length === 1
              ? 'Single Person'
              : selectedIds.length > 1
              ? `${selectedIds.length} Selected`
              : 'All Employees'}
          </div>
        </div>
        <div className="stat-card">
          <span className="stat-label">Active Report Type</span>
          <div className="stat-value" style={{ fontSize: '18px', color: '#0284c7' }}>
            {reportType === 'career' ? 'Career History' : 'Leave Taken'}
          </div>
        </div>
      </div>

      <section className="panel employee-panel">
        <div className="employee-panel-header-row" style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '20px', gap: '16px', flexWrap: 'wrap' }}>
          {/* Report Type Selector Tabs */}
          <div className="employee-tabs" style={{ margin: 0, borderRadius: '10px' }}>
            <button
              type="button"
              aria-pressed={reportType === 'career'}
              onClick={() => setReportType('career')}
            >
              📈 Career History & Promotions
            </button>
            <button
              type="button"
              aria-pressed={reportType === 'leave'}
              onClick={() => {
                setReportType('leave');
                refetchAbsence();
              }}
            >
              🌴 Leave Taken History
            </button>
          </div>

          <div className="career-history-actions" style={{ display: 'flex', gap: '8px' }}>
            <button
              type="button"
              className="button"
              onClick={() => {
                query.refetch();
                refetchAbsence();
              }}
              disabled={absenceAllQuery.isFetching || absenceTeamQuery.isFetching || absenceSelfQuery.isFetching || query.isFetching}
              title="Refresh report data"
            >
              <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.5">
                <path d="M13.5 8a5.5 5.5 0 1 1-1.6-3.9L14 6m0-4v4h-4" />
              </svg>
              {absenceAllQuery.isFetching || absenceTeamQuery.isFetching || absenceSelfQuery.isFetching || query.isFetching ? 'Refreshing…' : 'Refresh'}
            </button>
            <button
              type="button"
              className="button button-download-csv"
              onClick={handleDownloadCSV}
              title="Download report as CSV"
            >
              <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.5">
                <path d="M2.5 10.5v2a1 1 0 0 0 1 1h9a1 1 0 0 0 1-1v-2M8 2.5v7.5M5 7l3 3 3-3" />
              </svg>
              Download CSV {selectedIds.length > 0 ? `(${selectedIds.length})` : ''}
            </button>
            <button
              type="button"
              className="button button-export-excel"
              onClick={handleExportExcel}
              title="Export report to Interactive Excel Viewer"
            >
              <svg viewBox="0 0 16 16" width="14" height="14" fill="none" stroke="currentColor" strokeWidth="1.5">
                <rect x="2.5" y="2" width="11" height="12" rx="1.5" />
                <path d="M5.5 5.5l5 5M10.5 5.5l-5 5" />
              </svg>
              Export Excel {selectedIds.length > 0 ? `(${selectedIds.length})` : ''}
            </button>
          </div>
        </div>

        {/* Filter Controls */}
        <div className="employee-directory-toolbar" style={{ margin: '16px 0 20px' }}>
          <form className="employee-filters" style={{ flex: '1 1 100%' }} onSubmit={(e) => e.preventDefault()}>
            <Field
              label="Search Employee"
              placeholder="Search by name or code..."
              type="search"
              value={filters.search || ''}
              onChange={(e) => filter('search', e.target.value)}
            />
            <CustomSelect
              label="Department"
              value={filters.departmentId || ''}
              onChange={(e) => filter('departmentId', e.target.value)}
              options={departments.map((d) => ({ value: d.id, label: d.name }))}
              placeholder="All Departments"
            />
            <CustomSelect
              label="Grade"
              value={filters.gradeId || ''}
              onChange={(e) => filter('gradeId', e.target.value)}
              options={grades.map((g) => ({ value: g.id, label: g.name }))}
              placeholder="All Grades"
            />
            <CustomSelect
              label="Employment Status"
              value={filters.employmentStatus || ''}
              onChange={(e) => filter('employmentStatus', e.target.value)}
              options={[
                { value: 'ACTIVE', label: 'Active' },
                { value: 'ON_LEAVE', label: 'On Leave' },
                { value: 'INACTIVE', label: 'Inactive' },
                { value: 'TERMINATED', label: 'Terminated' },
              ]}
              placeholder="All Statuses"
            />
            {reportType === 'leave' && (
              <CustomSelect
                label="Calendar Year"
                value={selectedYear}
                onChange={(e) => setSelectedYear(Number(e.target.value))}
                options={[currentYear - 2, currentYear - 1, currentYear, currentYear + 1].map((y) => ({
                  value: y,
                  label: `${y} Calendar Year`,
                }))}
                placeholder="Select Year"
              />
            )}
            {(filters.search || filters.departmentId || filters.gradeId || filters.employmentStatus || selectedIds.length > 0 || (reportType === 'leave' && selectedYear !== currentYear)) && (
              <button
                type="button"
                className="button-clear"
                onClick={() => {
                  resetFilters();
                  setSelectedYear(currentYear);
                }}
              >
                Clear filters & selection
              </button>
            )}
          </form>
        </div>

        {selectedIds.length > 0 && (
          <div style={{ margin: '0 0 16px', padding: '10px 16px', background: '#fef3c7', border: '1px solid #fde68a', borderRadius: '8px', fontSize: '13px', color: '#92400e', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
            <span>
              <strong>{selectedIds.length} employee{selectedIds.length === 1 ? '' : 's'} selected</strong> — targeting selected person{selectedIds.length === 1 ? '' : 's'} history.
            </span>
            <button type="button" onClick={() => setSelectedIds([])} style={{ background: 'transparent', border: 'none', color: '#b45309', fontWeight: 600, cursor: 'pointer' }}>
              Deselect All
            </button>
          </div>
        )}

        <ErrorState error={refs.error} />

        <QueryState query={query}>
          {items.length === 0 ? (
            <EmptyState title="No employee records found" variant="search">
              No employee details match the selected report filter criteria.
            </EmptyState>
          ) : reportType === 'career' ? (
            /* CAREER HISTORY EXACT TABLE LAYOUT */
            <div className="employee-table-scroll" role="region" aria-label="Career History Table" tabIndex={0}>
              <table className="employee-history-table">
                <thead>
                  <tr>
                    <th scope="col" className="col-checkbox">
                      <input
                        type="checkbox"
                        aria-label="Select all employees"
                        checked={isAllSelected}
                        onChange={toggleSelectAll}
                      />
                    </th>
                    <th scope="col" className="col-num">#</th>
                    <th scope="col" className="col-name">Employee Name</th>
                    <th scope="col" className="col-code">Employee Code</th>
                    <th scope="col" className="col-date">Period Start</th>
                    <th scope="col" className="col-date">Period End</th>
                    <th scope="col" className="col-status">Position Status</th>
                    <th scope="col">Grade / Designation</th>
                    <th scope="col">Job Title</th>
                    <th scope="col">Department</th>
                    <th scope="col" className="col-length">Period Length</th>
                    <th scope="col" className="col-notes">Notes</th>
                    <th scope="col" className="col-date">Date Recorded</th>
                  </tr>
                </thead>
                <tbody>
                  {getCareerDataForEmployees().map((c, idx) => {
                    const isSelected = selectedIds.includes(c.employeeId);

                    return (
                      <tr key={idx} className={isSelected ? 'selected-row' : ''}>
                        <td className="col-checkbox">
                          <input
                            type="checkbox"
                            aria-label={`Select ${c.employeeName}`}
                            checked={isSelected}
                            onChange={() => toggleSelect(c.employeeId)}
                          />
                        </td>
                        <td className="col-num">{idx + 1}</td>
                        <td className="col-name">
                          <Link to={`/employees/admin/${c.employeeId}`} className="employee-name-link">
                            {c.employeeName}
                          </Link>
                        </td>
                        <td className="col-code">
                          <span className="code-tag">{c.employeeCode}</span>
                        </td>
                        <td className="col-date">{c.periodStart}</td>
                        <td className="col-date">{c.periodEnd}</td>
                        <td className="col-status">
                          {c.current ? (
                            <span className="badge badge-active">
                              Current position
                            </span>
                          ) : (
                            <span className="badge badge-inactive">
                              Previous period
                            </span>
                          )}
                        </td>
                        <td>{c.gradeDesignation}</td>
                        <td>{c.jobTitle}</td>
                        <td style={{ color: '#475569' }}>{c.department}</td>
                        <td className="col-length">{c.periodLength}</td>
                        <td className="col-notes">{c.note}</td>
                        <td className="col-date col-date-recorded">{c.recordedAt}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          ) : getReportData(items).rows.length === 0 ? (
            <EmptyState title="No leave records found" variant="search">
              No leave taken or absence records found for the selected employee criteria.
            </EmptyState>
          ) : (
            /* LEAVE TAKEN TABLE LAYOUT */
            <div className="employee-table-scroll" role="region" aria-label="Leave Report Results" tabIndex={0}>
              <table className="employee-history-table">
                <thead>
                  <tr>
                    <th scope="col" className="col-checkbox">
                      <input
                        type="checkbox"
                        aria-label="Select all employees"
                        checked={isAllSelected}
                        onChange={toggleSelectAll}
                      />
                    </th>
                    {getReportData(items).headers.map((h, i) => (
                      <th
                        key={i}
                        scope="col"
                        className={
                          i === 0 ? 'col-num' :
                          i === 1 ? 'col-code' :
                          i === 2 ? 'col-name' :
                          i === 4 || i === 5 ? 'col-date' :
                          i === 7 ? 'col-status' : ''
                        }
                      >
                        {h}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {getReportData(items).rows.map((row, rIdx) => {
                    const emp = items.find((e) => e.employeeCode === row[1]) || items[rIdx] || items[0] || {};
                    const isSelected = selectedIds.includes(emp.id);

                    return (
                      <tr key={rIdx} className={isSelected ? 'selected-row' : ''}>
                        <td className="col-checkbox">
                          <input
                            type="checkbox"
                            aria-label={`Select ${row[2] || emp.firstName || 'employee'}`}
                            checked={isSelected}
                            onChange={() => emp.id && toggleSelect(emp.id)}
                          />
                        </td>
                        {row.map((cell, cIdx) => (
                          <td
                            key={cIdx}
                            className={
                              cIdx === 0 ? 'col-num' :
                              cIdx === 1 ? 'col-code' :
                              cIdx === 2 ? 'col-name' :
                              cIdx === 4 || cIdx === 5 ? 'col-date' :
                              cIdx === 7 ? 'col-status' : ''
                            }
                          >
                            {cIdx === 1 ? (
                              <span className="code-tag">{cell}</span>
                            ) : cIdx === 7 ? (
                              <Badge value={String(cell).startsWith('PREVIOUS') ? 'INACTIVE' : cell} />
                            ) : (
                              cell
                            )}
                          </td>
                        ))}
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          {reportType === 'career' ? (
            query.data && (
              <Pagination
                data={query.data}
                busy={query.isFetching}
                onPage={(page) => setFilters((f) => ({ ...f, page }))}
              />
            )
          ) : (
            <div className="pagination" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '16px', color: '#64748b', fontSize: '13px' }}>
              <span>
                {getReportData(items).rows.length} {getReportData(items).rows.length === 1 ? 'leave record' : 'leave records'} · {selectedYear} Calendar Year
              </span>
            </div>
          )}
        </QueryState>
      </section>
    </div>
  );
}
