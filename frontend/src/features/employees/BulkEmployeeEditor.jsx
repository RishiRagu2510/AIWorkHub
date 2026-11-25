import { useState, useEffect, useMemo } from 'react';
import {
  useEmployeesQuery,
  useEmployeeUsersQuery,
  useEmployeeManagersQuery,
  useEmployeeCareerCatalogQuery,
  useEmployeeReferencesQuery,
  useSaveEmployeeMutation,
} from '../../services/api/employees';
import { QueryState, CustomSelect, Field } from '../../components/common';
import CustomDatePicker from '../../components/CustomDatePicker';
import '../../../src/styles/employees.scss';

export default function BulkEmployeeEditor({ references = [], onClose, onSaved }) {
  const [save] = useSaveEmployeeMutation();
  const employeesQuery = useEmployeesQuery({ pageSize: 100 }, { refetchOnMountOrArgChange: true });
  const usersQuery = useEmployeeUsersQuery({ page: 1, pageSize: 100 }, { refetchOnMountOrArgChange: true });
  const managersQuery = useEmployeeManagersQuery(undefined, { refetchOnMountOrArgChange: true });
  const catalogQuery = useEmployeeCareerCatalogQuery(undefined, { refetchOnMountOrArgChange: true });
  const refsQuery = useEmployeeReferencesQuery(undefined, { refetchOnMountOrArgChange: true });

  const allReferences = useMemo(() => {
    return (references && references.length > 0) ? references : (refsQuery.data || []);
  }, [references, refsQuery.data]);

  const departmentOptions = useMemo(() => {
    const list = [];
    const seen = new Set();

    // 1. From Career Catalog profiles
    for (const p of catalogQuery.data?.profiles || []) {
      if (!seen.has(p.name)) {
        seen.add(p.name);
        list.push({
          value: `profile:${p.id}`,
          label: p.name,
          rawId: p.id,
          type: 'profile',
        });
      }
    }

    // 2. From References DEPARTMENT
    for (const r of allReferences.filter((x) => x.kind === 'DEPARTMENT')) {
      if (!seen.has(r.name)) {
        seen.add(r.name);
        list.push({
          value: `reference:${r.id}`,
          label: r.name,
          rawId: r.id,
          type: 'reference',
        });
      }
    }

    return list;
  }, [catalogQuery.data?.profiles, allReferences]);

  const gradeOptions = useMemo(() => {
    const list = [];
    const seen = new Set();

    for (const g of catalogQuery.data?.grades || []) {
      const label = `${g.code}${g.name && g.name !== g.code ? ` — ${g.name}` : ''}`;
      if (!seen.has(g.code)) {
        seen.add(g.code);
        list.push({
          value: String(g.id),
          label,
          rawId: g.id,
          type: 'catalog',
        });
      }
    }

    for (const r of allReferences.filter((x) => x.kind === 'GRADE')) {
      if (!seen.has(r.name)) {
        seen.add(r.name);
        list.push({
          value: `reference:${r.id}`,
          label: r.name,
          rawId: r.id,
          type: 'reference',
        });
      }
    }

    return list;
  }, [catalogQuery.data?.grades, allReferences]);

  const getGradesForDept = (deptVal) => {
    if (!deptVal) return gradeOptions;
    const deptObj = departmentOptions.find(d => String(d.value) === String(deptVal) || String(d.rawId) === String(deptVal));
    const deptText = (deptObj?.label || deptVal || '').toLowerCase();
    const isHr = deptText.includes('hr') || deptText.includes('human');
    const isDev = deptText.includes('dev') || deptText.includes('eng') || deptText.includes('tech') || deptText.includes('data') || deptText.includes('software');
    const isDesign = deptText.includes('design') || deptText.includes('delivery');

    const filtered = gradeOptions.filter(g => {
      const code = g.label.toUpperCase();
      if (isHr) return code.startsWith('H');
      if (isDev) return code.startsWith('T');
      if (isDesign) return code.startsWith('D');
      return true;
    });
    return filtered.length > 0 ? filtered : gradeOptions;
  };

  const jobTitleOptions = useMemo(() => {
    const list = [];
    const seen = new Set();

    // 1. From Career Catalog levels
    for (const l of catalogQuery.data?.levels || []) {
      const key = `${l.careerProfileId}:${l.gradeId}:${l.careerTitle?.toLowerCase()}`;
      if (l.careerTitle && !seen.has(key)) {
        seen.add(key);
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

    // 2. From References JOB_TITLE
    for (const r of allReferences.filter((x) => x.kind === 'JOB_TITLE')) {
      if (!seen.has(`ref:${r.name.toLowerCase()}`)) {
        seen.add(`ref:${r.name.toLowerCase()}`);
        list.push({
          value: `reference:${r.id}`,
          label: r.name,
          rawId: r.id,
          type: 'reference',
        });
      }
    }

    return list;
  }, [catalogQuery.data?.levels, allReferences]);

  const getJobTitlesForDeptAndGrade = (deptVal, gradeVal) => {
    if (!deptVal && !gradeVal) return jobTitleOptions;
    let deptRawId = null;
    if (deptVal) {
      if (deptVal.startsWith('profile:')) deptRawId = Number(deptVal.split(':')[1]);
      else {
        const matched = departmentOptions.find((d) => String(d.rawId) === String(deptVal) || d.value === String(deptVal));
        if (matched?.type === 'profile') deptRawId = matched.rawId;
      }
    }
    let gradeRawId = null;
    let gradeCode = '';
    if (gradeVal) {
      if (!gradeVal.startsWith('reference:')) {
        gradeRawId = Number(gradeVal);
      }
      const matchedGrade = gradeOptions.find((g) => String(g.rawId) === String(gradeVal) || g.value === String(gradeVal));
      gradeCode = (matchedGrade?.label || gradeVal || '').toUpperCase();
    }

    const filtered = jobTitleOptions.filter((j) => {
      if (j.type === 'level') {
        if (deptRawId && j.careerProfileId !== deptRawId) return false;
        if (gradeRawId && j.gradeId !== gradeRawId) return false;
      } else if (gradeCode) {
        const titleLower = (j.label || '').toLowerCase();
        if (gradeCode.startsWith('T0') && !titleLower.includes('intern') && !titleLower.includes('trainee')) return false;
        if (gradeCode.startsWith('T1') && !titleLower.includes('junior') && !titleLower.includes('associate')) return false;
        if (gradeCode.startsWith('T3') && !titleLower.includes('senior') && !titleLower.includes('sr')) return false;
        if (gradeCode.startsWith('T4') && !titleLower.includes('lead') && !titleLower.includes('staff') && !titleLower.includes('principal')) return false;
        if (gradeCode.startsWith('H1') && !titleLower.includes('associate') && !titleLower.includes('trainee') && !titleLower.includes('coordinator')) return false;
        if (gradeCode.startsWith('H2') && !titleLower.includes('executive') && !titleLower.includes('specialist') && !titleLower.includes('generalist')) return false;
        if (gradeCode.startsWith('H3') && !titleLower.includes('partner') && !titleLower.includes('bp') && !titleLower.includes('senior')) return false;
        if (gradeCode.startsWith('H4') && !titleLower.includes('manager') && !titleLower.includes('head') && !titleLower.includes('director')) return false;
      }
      return true;
    });

    return filtered.length > 0 ? filtered : jobTitleOptions;
  };

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

  const unlinkedUsers = useMemo(() => usersQuery.data?.items || [], [usersQuery.data]);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedUserIds, setSelectedUserIds] = useState([]);
  const [rows, setRows] = useState([]);
  const [busy, setBusy] = useState(false);
  const [progress, setProgress] = useState(null);
  const [results, setResults] = useState(null);

  // Shared defaults state
  const [defaultDepartment, setDefaultDepartment] = useState('');
  const [defaultGrade, setDefaultGrade] = useState('');
  const [defaultJobTitle, setDefaultJobTitle] = useState('');
  const [defaultManager, setDefaultManager] = useState('');
  const [defaultJoiningDate, setDefaultJoiningDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [defaultEmploymentType, setDefaultEmploymentType] = useState('FULL_TIME');
  const defaultEmploymentStatus = 'ACTIVE';
  const [defaultLocation, setDefaultLocation] = useState('');

  const filteredUsers = useMemo(() => {
    if (!searchTerm.trim()) return unlinkedUsers;
    const lower = searchTerm.toLowerCase();
    return unlinkedUsers.filter(
      (u) =>
        u.displayName?.toLowerCase().includes(lower) ||
        u.email?.toLowerCase().includes(lower) ||
        String(u.id).includes(lower)
    );
  }, [unlinkedUsers, searchTerm]);

  const maxExistingEmpNum = useMemo(() => {
    let max = employeesQuery.data?.maxEmployeeNumber || 0;
    for (const emp of employeesQuery.data?.items || []) {
      const code = emp.employeeCode || emp.code || '';
      const match = code.match(/EMP-(\d+)/i);
      if (match) {
        const num = parseInt(match[1], 10);
        if (!isNaN(num) && num > max) {
          max = num;
        }
      }
    }
    return max;
  }, [employeesQuery.data]);

  // Sync rows when selectedUserIds change
  useEffect(() => {
    setRows((prevRows) => {
      const existingMap = new Map(prevRows.map((r) => [r.userId, r]));
      return selectedUserIds.map((uid, index) => {
        if (existingMap.has(uid)) return existingMap.get(uid);

        const userObj = unlinkedUsers.find((u) => u.id === uid);
        const nameParts = (userObj?.displayName || '').trim().split(/\s+/);
        const firstName = nameParts[0] || (userObj?.email?.split('@')[0] || `User${uid}`);
        const lastName = nameParts.slice(1).join(' ') || '';
        const codeNum = maxExistingEmpNum + index + 1;
        const paddedNum = String(codeNum).padStart(3, '0');

        return {
          userId: uid,
          displayName: userObj?.displayName || userObj?.email || `User #${uid}`,
          email: userObj?.email || '',
          employeeCode: `EMP-${paddedNum}`,
          firstName,
          lastName,
          preferredName: '',
          departmentId: defaultDepartment || '',
          gradeId: defaultGrade || '',
          jobTitleId: defaultJobTitle || '',
          managerEmployeeId: defaultManager || '',
          joiningDate: defaultJoiningDate,
          employmentType: defaultEmploymentType,
          employmentStatus: defaultEmploymentStatus,
          location: defaultLocation,
          status: 'pending',
          errorMessage: null,
        };
      });
    });
  }, [selectedUserIds, unlinkedUsers, maxExistingEmpNum]);

  function handleSelectAll() {
    if (selectedUserIds.length === filteredUsers.length) {
      setSelectedUserIds([]);
    } else {
      setSelectedUserIds(filteredUsers.map((u) => u.id));
    }
  }

  function handleToggleUser(userId) {
    setSelectedUserIds((prev) =>
      prev.includes(userId) ? prev.filter((id) => id !== userId) : [...prev, userId]
    );
  }

  function handleDefaultDepartmentChange(val) {
    setDefaultDepartment(val);
    setDefaultGrade('');
    setDefaultJobTitle('');
    setRows((prev) =>
      prev.map((r) => ({
        ...r,
        departmentId: val,
        gradeId: '',
        jobTitleId: '',
      }))
    );
  }

  function handleDefaultGradeChange(val) {
    setDefaultGrade(val);
    setDefaultJobTitle('');
    setRows((prev) =>
      prev.map((r) => ({
        ...r,
        gradeId: val,
        jobTitleId: '',
      }))
    );
  }

  function handleDefaultJobTitleChange(val) {
    setDefaultJobTitle(val);
    setRows((prev) =>
      prev.map((r) => ({
        ...r,
        jobTitleId: val,
      }))
    );
  }

  function handleDefaultManagerChange(val) {
    setDefaultManager(val);
    setRows((prev) => prev.map((r) => ({ ...r, managerEmployeeId: val })));
  }

  function handleDefaultJoiningDateChange(val) {
    setDefaultJoiningDate(val);
    setRows((prev) => prev.map((r) => ({ ...r, joiningDate: val })));
  }

  function handleDefaultEmploymentTypeChange(val) {
    setDefaultEmploymentType(val);
    setRows((prev) => prev.map((r) => ({ ...r, employmentType: val })));
  }

  function handleDefaultLocationChange(val) {
    setDefaultLocation(val);
    setRows((prev) => prev.map((r) => ({ ...r, location: val })));
  }

  function handleApplyDefaults() {
    setRows((prev) =>
      prev.map((r) => ({
        ...r,
        departmentId: defaultDepartment || r.departmentId,
        gradeId: defaultGrade || r.gradeId,
        jobTitleId: defaultJobTitle || r.jobTitleId,
        managerEmployeeId: defaultManager || r.managerEmployeeId,
        joiningDate: defaultJoiningDate || r.joiningDate,
        employmentType: defaultEmploymentType || r.employmentType,
        employmentStatus: defaultEmploymentStatus || r.employmentStatus,
        location: defaultLocation !== undefined && defaultLocation !== '' ? defaultLocation : r.location,
      }))
    );
  }

  function handleUpdateRow(userId, field, value) {
    setRows((prev) =>
      prev.map((r) => (r.userId === userId ? { ...r, [field]: value } : r))
    );
  }

  function handleRemoveRow(userId) {
    setSelectedUserIds((prev) => prev.filter((id) => id !== userId));
  }

  async function handleBatchSubmit(e) {
    e.preventDefault();
    if (!rows.length || busy) return;

    setBusy(true);
    const successList = [];
    const errorList = [];

    for (let i = 0; i < rows.length; i++) {
      const row = rows[i];
      setProgress({ current: i + 1, total: rows.length, currentName: `${row.firstName} ${row.lastName}`.trim() });
      handleUpdateRow(row.userId, 'status', 'saving');

      const deptVal = row.departmentId ? String(row.departmentId) : '';
      let careerProfileId = null;
      let catalogDepartmentId = null;
      let departmentId = null;

      if (deptVal.startsWith('profile:')) {
        careerProfileId = Number(deptVal.split(':')[1]);
        catalogDepartmentId = careerProfileId;
      } else if (deptVal.startsWith('reference:')) {
        departmentId = Number(deptVal.split(':')[1]);
      } else if (deptVal) {
        const matched = departmentOptions.find((d) => String(d.rawId) === String(deptVal) || d.value === String(deptVal));
        if (matched?.type === 'profile') {
          careerProfileId = matched.rawId;
          catalogDepartmentId = matched.rawId;
        } else if (matched?.type === 'reference') {
          departmentId = matched.rawId;
        } else {
          departmentId = Number(deptVal) || null;
        }
      }

      const gradeVal = row.gradeId ? String(row.gradeId) : '';
      let gradeId = null;
      let catalogGradeId = null;

      if (gradeVal.startsWith('reference:')) {
        gradeId = Number(gradeVal.split(':')[1]);
      } else if (gradeVal) {
        const matchedGrade = gradeOptions.find((g) => String(g.rawId) === String(gradeVal) || g.value === String(gradeVal));
        if (matchedGrade?.type === 'reference') {
          gradeId = matchedGrade.rawId;
        } else {
          catalogGradeId = Number(gradeVal) || null;
        }
      }

      const jobTitleVal = row.jobTitleId ? String(row.jobTitleId) : '';
      let careerLevelId = null;
      let jobTitleId = null;

      if (jobTitleVal.startsWith('level:')) {
        careerLevelId = Number(jobTitleVal.split(':')[1]);
      } else if (jobTitleVal.startsWith('reference:')) {
        jobTitleId = Number(jobTitleVal.split(':')[1]);
      } else if (jobTitleVal) {
        const matchedJob = jobTitleOptions.find((j) => String(j.rawId) === String(jobTitleVal) || j.value === String(jobTitleVal));
        if (matchedJob?.type === 'level') {
          careerLevelId = matchedJob.rawId;
        } else if (matchedJob?.type === 'reference') {
          jobTitleId = matchedJob.rawId;
        } else {
          jobTitleId = Number(jobTitleVal) || null;
        }
      }

      // Ensure careerProfileId is aligned with department
      if (!careerProfileId && departmentId) {
        const refName = allReferences.find((r) => r.id === departmentId)?.name;
        if (refName) {
          const matchedProfile = (catalogQuery.data?.profiles || []).find((p) => p.name?.toLowerCase() === refName.toLowerCase());
          if (matchedProfile) {
            careerProfileId = matchedProfile.id;
            catalogDepartmentId = matchedProfile.id;
          }
        }
      }

      // Ensure catalogGradeId is aligned with grade
      if (!catalogGradeId && gradeId) {
        const refCode = allReferences.find((r) => r.id === gradeId)?.name;
        if (refCode) {
          const matchedGrade = (catalogQuery.data?.grades || []).find((g) => g.code?.toUpperCase() === refCode.toUpperCase());
          if (matchedGrade) {
            catalogGradeId = matchedGrade.id;
          }
        }
      }

      // If careerProfile and catalogGrade are known, resolve the exact active career level
      if (careerProfileId && catalogGradeId) {
        const exactLevel = (catalogQuery.data?.levels || []).find(
          (l) => l.careerProfileId === careerProfileId && l.gradeId === catalogGradeId
        );
        if (exactLevel) {
          careerLevelId = exactLevel.id;
        }
      } else if (careerLevelId) {
        let matchedLevel = (catalogQuery.data?.levels || []).find((j) => j.id === careerLevelId);
        if (careerProfileId && matchedLevel && matchedLevel.careerProfileId !== careerProfileId) {
          const profileLevel = (catalogQuery.data?.levels || []).find(
            (j) => j.careerProfileId === careerProfileId && (j.gradeId === (catalogGradeId || matchedLevel.gradeId) || j.careerTitle?.toLowerCase() === matchedLevel.careerTitle?.toLowerCase())
          );
          if (profileLevel) {
            careerLevelId = profileLevel.id;
            matchedLevel = profileLevel;
          }
        }
        if (matchedLevel) {
          if (!careerProfileId) {
            careerProfileId = matchedLevel.careerProfileId;
            catalogDepartmentId = matchedLevel.careerProfileId;
          }
          if (!catalogGradeId) {
            catalogGradeId = matchedLevel.gradeId;
          }
        }
      }

      const body = {
        userId: Number(row.userId),
        employeeCode: row.employeeCode?.trim() || `EMP-${row.userId}`,
        firstName: row.firstName?.trim() || 'Employee',
        lastName: row.lastName?.trim() || '',
        preferredName: row.preferredName?.trim() || null,
        joiningDate: row.joiningDate || new Date().toISOString().slice(0, 10),
        endDate: null,
        departmentId,
        catalogDepartmentId,
        careerProfileId,
        gradeId,
        catalogGradeId,
        jobTitleId,
        careerLevelId,
        managerEmployeeId: row.managerEmployeeId ? Number(row.managerEmployeeId) : null,
        employmentType: row.employmentType || 'FULL_TIME',
        employmentStatus: row.employmentStatus || 'ACTIVE',
        location: row.location?.trim() || '',
        phone: '',
        profilePhotoUrl: null,
      };

      try {
        await save({
          path: '',
          method: 'POST',
          body,
        }).unwrap();

        handleUpdateRow(row.userId, 'status', 'saved');
        successList.push(row);
      } catch (err) {
        const msg = err?.data?.message || err?.message || 'Failed to create employee profile';
        handleUpdateRow(row.userId, 'status', 'error');
        handleUpdateRow(row.userId, 'errorMessage', msg);
        errorList.push({ ...row, error: msg });
      }
    }

    setBusy(false);
    setProgress(null);
    setResults({ success: successList, errors: errorList });

    if (errorList.length === 0 && successList.length > 0) {
      setTimeout(() => {
        onSaved?.();
        onClose?.();
      }, 1200);
    }
  }

  function handleAutoFixNextCodes() {
    let nextNum = maxExistingEmpNum + 1;
    rows.forEach((r) => {
      const match = (r.employeeCode || '').match(/EMP-(\d+)/i);
      if (match) {
        const n = parseInt(match[1], 10);
        if (!isNaN(n) && n >= nextNum && r.status === 'saved') {
          nextNum = n + 1;
        }
      }
    });

    setRows((prev) =>
      prev.map((r) => {
        if (r.status === 'error') {
          const code = `EMP-${String(nextNum).padStart(3, '0')}`;
          nextNum++;
          return {
            ...r,
            employeeCode: code,
            status: 'pending',
            errorMessage: null,
          };
        }
        return r;
      })
    );
    setResults(null);
  }

  const isAllSelected = filteredUsers.length > 0 && selectedUserIds.length === filteredUsers.length;

  return (
    <section className="panel employee-panel employee-editor-panel bulk-employee-panel">
      <header className="employee-editor-header">
        <div>
          <h2>Bulk add employees</h2>
          <p>Create employee profiles for multiple organization users at once with shared defaults or customized fields.</p>
        </div>
        {onClose && (
          <button
            type="button"
            className="employee-editor-close-btn"
            onClick={onClose}
            aria-label="Close editor"
            title="Close editor"
          >
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        )}
      </header>

      {/* Step 1: User Picker */}
      <div className="bulk-step-card">
        <div className="bulk-step-header">
          <div>
            <h3>1. Select unlinked organization users</h3>
            <p>Choose the users who need employee records created.</p>
          </div>
          <div className="bulk-step-actions">
            <span className="bulk-badge-count">{selectedUserIds.length} of {unlinkedUsers.length} selected</span>
            <button
              type="button"
              className="button-sm"
              onClick={handleSelectAll}
              disabled={unlinkedUsers.length === 0}
            >
              {isAllSelected ? 'Deselect all' : 'Select all'}
            </button>
          </div>
        </div>

        <QueryState query={usersQuery}>
          {unlinkedUsers.length === 0 ? (
            <div className="bulk-empty-users">
              <p>No unlinked organization users found. All current users already have employee profiles.</p>
            </div>
          ) : (
            <>
              <div className="bulk-search-row">
                <input
                  type="search"
                  placeholder="Filter users by name or email..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="bulk-search-input"
                />
              </div>

              <div className="bulk-user-chips-grid">
                {filteredUsers.map((u) => {
                  const isChecked = selectedUserIds.includes(u.id);
                  return (
                    <div
                      key={u.id}
                      className={`bulk-user-chip ${isChecked ? 'is-selected' : ''}`}
                      onClick={() => handleToggleUser(u.id)}
                    >
                      <input
                        type="checkbox"
                        checked={isChecked}
                        onChange={() => {}}
                        aria-label={`Select ${u.displayName}`}
                      />
                      <div className="chip-avatar">
                        {(u.displayName || u.email || '?')[0].toUpperCase()}
                      </div>
                      <div className="chip-info">
                        <strong>{u.displayName}</strong>
                        <small>{u.email}</small>
                      </div>
                    </div>
                  );
                })}
              </div>
            </>
          )}
        </QueryState>
      </div>

      {/* Step 2: Shared Defaults (Shown when >= 1 user selected) */}
      {selectedUserIds.length > 0 && (
        <div className="bulk-step-card bulk-defaults-card">
          <div className="bulk-step-header">
            <div>
              <h3>2. Set shared defaults (optional)</h3>
              <p>Apply common department, manager, or joining dates across all selected employees.</p>
            </div>
            <button type="button" className="button primary button-sm" onClick={handleApplyDefaults}>
              Apply to all {selectedUserIds.length} rows
            </button>
          </div>

          <div className="bulk-defaults-grid">
            <CustomSelect
              label="Department"
              value={defaultDepartment}
              onChange={(e) => handleDefaultDepartmentChange(e.target.value)}
              placeholder="Select department"
              options={departmentOptions}
            />

            <CustomSelect
              label="Grade"
              value={defaultGrade}
              onChange={(e) => handleDefaultGradeChange(e.target.value)}
              placeholder="Select grade"
              options={getGradesForDept(defaultDepartment)}
            />

            <CustomSelect
              label="Job Title"
              value={defaultJobTitle}
              onChange={(e) => handleDefaultJobTitleChange(e.target.value)}
              placeholder="Select job title"
              options={getJobTitlesForDeptAndGrade(defaultDepartment, defaultGrade)}
            />

            <CustomSelect
              label="Reporting manager"
              value={defaultManager}
              onChange={(e) => handleDefaultManagerChange(e.target.value)}
              placeholder="Select manager"
              options={managerOptions}
            />

            <Field
              label="Joining date"
              type="date"
              value={defaultJoiningDate}
              onChange={(e) => handleDefaultJoiningDateChange(e.target.value)}
            />

            <CustomSelect
              label="Employment type"
              value={defaultEmploymentType}
              onChange={(e) => handleDefaultEmploymentTypeChange(e.target.value)}
              options={[
                { value: 'FULL_TIME', label: 'Full time' },
                { value: 'PART_TIME', label: 'Part time' },
                { value: 'CONTRACT', label: 'Contract' },
                { value: 'INTERN', label: 'Intern' },
              ]}
            />

            <Field
              label="Location"
              placeholder="e.g. Headquarters, Remote"
              value={defaultLocation}
              onChange={(e) => handleDefaultLocationChange(e.target.value)}
            />
          </div>
        </div>
      )}

      {/* Step 3: Interactive Editable Table */}
      {rows.length > 0 && (
        <form onSubmit={handleBatchSubmit} className="bulk-step-card bulk-table-card">
          <div className="bulk-step-header">
            <div>
              <h3>3. Review and customize employee records ({rows.length})</h3>
              <p>Verify employee codes and details before creating profiles.</p>
            </div>
          </div>

          <div className="table-scroll bulk-table-scroll">
            <table className="bulk-table">
              <thead>
                <tr>
                  <th>User & Email</th>
                  <th>Emp Code *</th>
                  <th>First Name *</th>
                  <th>Last Name</th>
                  <th>Department</th>
                  <th>Grade</th>
                  <th>Job Title</th>
                  <th>Manager</th>
                  <th>Joining Date</th>
                  <th>Status</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr key={row.userId} className={`row-status-${row.status}`}>
                    <td>
                      <div className="bulk-table-user">
                        <strong>{row.displayName}</strong>
                        <small>{row.email}</small>
                      </div>
                    </td>
                    <td>
                      <input
                        type="text"
                        required
                        className="bulk-table-input"
                        value={row.employeeCode}
                        onChange={(e) => handleUpdateRow(row.userId, 'employeeCode', e.target.value)}
                        placeholder="EMP-001"
                      />
                    </td>
                    <td>
                      <input
                        type="text"
                        required
                        className="bulk-table-input"
                        value={row.firstName}
                        onChange={(e) => handleUpdateRow(row.userId, 'firstName', e.target.value)}
                        placeholder="First name"
                      />
                    </td>
                    <td>
                      <input
                        type="text"
                        className="bulk-table-input"
                        value={row.lastName}
                        onChange={(e) => handleUpdateRow(row.userId, 'lastName', e.target.value)}
                        placeholder="Last name"
                      />
                    </td>
                    <td>
                      <select
                        className="bulk-table-select"
                        value={row.departmentId}
                        onChange={(e) => {
                          const newDept = e.target.value;
                          const validGrades = getGradesForDept(newDept);
                          const newGrade = validGrades.some((g) => g.value === row.gradeId) ? row.gradeId : (validGrades[0]?.value || '');
                          const validTitles = getJobTitlesForDeptAndGrade(newDept, newGrade);
                          const newTitle = validTitles.some((j) => j.value === row.jobTitleId) ? row.jobTitleId : (validTitles[0]?.value || '');
                          setRows((prev) =>
                            prev.map((r) =>
                              r.userId === row.userId
                                ? { ...r, departmentId: newDept, gradeId: newGrade, jobTitleId: newTitle }
                                : r
                            )
                          );
                        }}
                      >
                        <option value="">No dept</option>
                        {departmentOptions.map((d) => (
                          <option key={d.value} value={d.value}>{d.label}</option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <select
                        className="bulk-table-select"
                        value={row.gradeId}
                        onChange={(e) => {
                          const newGrade = e.target.value;
                          const validTitles = getJobTitlesForDeptAndGrade(row.departmentId, newGrade);
                          const newTitle = validTitles.some((j) => j.value === row.jobTitleId) ? row.jobTitleId : (validTitles[0]?.value || '');
                          setRows((prev) =>
                            prev.map((r) =>
                              r.userId === row.userId
                                ? { ...r, gradeId: newGrade, jobTitleId: newTitle }
                                : r
                            )
                          );
                        }}
                      >
                        <option value="">No grade</option>
                        {getGradesForDept(row.departmentId).map((g) => (
                          <option key={g.value} value={g.value}>{g.label}</option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <select
                        className="bulk-table-select"
                        value={row.jobTitleId || ''}
                        onChange={(e) => handleUpdateRow(row.userId, 'jobTitleId', e.target.value)}
                      >
                        <option value="">No title</option>
                        {getJobTitlesForDeptAndGrade(row.departmentId, row.gradeId).map((j) => (
                          <option key={j.value} value={j.value}>{j.label}</option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <select
                        className="bulk-table-select"
                        value={row.managerEmployeeId}
                        onChange={(e) => handleUpdateRow(row.userId, 'managerEmployeeId', e.target.value)}
                      >
                        <option value="">No manager</option>
                        {managerOptions.map((m) => (
                          <option key={m.value} value={m.value}>{m.label}</option>
                        ))}
                      </select>
                    </td>
                    <td>
                      <CustomDatePicker
                        required
                        className="bulk-table-input"
                        value={row.joiningDate}
                        onChange={(e) => handleUpdateRow(row.userId, 'joiningDate', e.target.value)}
                        placeholder="YYYY-MM-DD"
                      />
                    </td>
                    <td>
                      {row.status === 'saving' && <span className="bulk-row-status is-saving">Saving…</span>}
                      {row.status === 'saved' && <span className="bulk-row-status is-saved">✓ Created</span>}
                      {row.status === 'error' && (
                        <span className="bulk-row-status is-error" title={row.errorMessage}>
                          ✕ Failed
                        </span>
                      )}
                      {row.status === 'pending' && <span className="bulk-row-status is-pending">Ready</span>}
                    </td>
                    <td>
                      <button
                        type="button"
                        className="bulk-remove-row-btn"
                        onClick={() => handleRemoveRow(row.userId)}
                        aria-label={`Remove ${row.displayName}`}
                        title="Remove from batch"
                      >
                        ✕
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Progress / Status */}
          {progress && (
            <div className="bulk-progress-bar-container">
              <div className="bulk-progress-text">
                <span>Creating employee {progress.current} of {progress.total}: <strong>{progress.currentName}</strong></span>
                <span>{Math.round((progress.current / progress.total) * 100)}%</span>
              </div>
              <div className="bulk-progress-track">
                <div
                  className="bulk-progress-fill"
                  style={{ width: `${(progress.current / progress.total) * 100}%` }}
                />
              </div>
            </div>
          )}

          {results && (
            <div className={`bulk-results-summary ${results.errors.length ? 'has-errors' : 'is-success'}`}>
              <strong>
                {results.success.length} of {rows.length} employees created successfully.
              </strong>
              {results.errors.length > 0 && (
                <>
                  <ul className="bulk-error-list">
                    {results.errors.map((err) => (
                      <li key={err.userId}>
                        {err.displayName}: {err.error}
                      </li>
                    ))}
                  </ul>
                  <div style={{ marginTop: '12px' }}>
                    <button
                      type="button"
                      className="button primary"
                      style={{ fontSize: '0.85rem', padding: '6px 14px' }}
                      onClick={handleAutoFixNextCodes}
                    >
                      Auto-assign next available code(s) & retry
                    </button>
                  </div>
                </>
              )}
            </div>
          )}

          <div className="bulk-actions-footer">
            <button
              type="submit"
              className="primary"
              disabled={busy || rows.length === 0}
            >
              {busy ? 'Creating employees…' : `Create ${rows.length} employee${rows.length === 1 ? '' : 's'}`}
            </button>
            {onClose && (
              <button type="button" onClick={onClose} disabled={busy}>
                Cancel
              </button>
            )}
          </div>
        </form>
      )}
    </section>
  );
}
