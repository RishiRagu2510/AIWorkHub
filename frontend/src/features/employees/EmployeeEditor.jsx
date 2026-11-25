import EmployeePhoto from './EmployeePhoto';
import { useState, useMemo } from 'react';
import { useAuth } from '../auth/context';
import {
  useEmployeesQuery,
  useEmployeeUsersQuery,
  useEmployeeManagersQuery,
  useEmployeeCareerCatalogQuery,
  useSaveEmployeeMutation,
} from '../../services/api/employees';
import { ErrorState, Field, QueryState, Pagination, CustomSelect } from '../../components/common';
export function ReferenceSelect({ kind, references = [], value, onChange, placeholder = 'Select an option' }) {
  const options = references.filter((r) => r.kind === kind);
  return (
    <CustomSelect
      label={kind.toLowerCase().replaceAll('_', ' ')}
      value={value || ''}
      onChange={onChange}
      placeholder={options.length ? placeholder : 'No organization records configured'}
      options={options.map((r) => ({ value: String(r.id), label: r.name }))}
    />
  );
}
export default function EmployeeEditor({ employee, references, onClose, onSaved, onSwitchToBulk, initialValues, submitOverride, submitError, submitBusy, submitLabel = 'Save profile', approvedWorkEmail, self = false }) {
  const isEditing = Boolean(employee?.id);
  employee = employee || initialValues;
  const { session, updateUser } = useAuth();
  const [save, result] = useSaveEmployeeMutation();
  const [page, setPage] = useState(1);
  const employeesQuery = useEmployeesQuery({ pageSize: 100 }, { skip: isEditing || self, refetchOnMountOrArgChange: true });
  const users = useEmployeeUsersQuery({ page: approvedWorkEmail ? 1 : page, ...(approvedWorkEmail ? {search: approvedWorkEmail} : {}) }, { skip: isEditing || self, refetchOnMountOrArgChange: true, refetchOnFocus: true, refetchOnReconnect: true });

  const nextEmployeeCode = useMemo(() => {
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
    return `EMP-${String(max + 1).padStart(3, '0')}`;
  }, [employeesQuery.data]);

  const [userCode, setUserCode] = useState(null);
  const currentEmployeeCode = userCode ?? (employee?.employeeCode || nextEmployeeCode);

  const [userChoice, setUserId] = useState(employee?.userId || '');
  const approvedUser = approvedWorkEmail ? users.data?.items?.find(u => u.email?.trim().toLowerCase() === approvedWorkEmail.trim().toLowerCase()) : null;
  const userId = approvedWorkEmail ? (approvedUser?.id ? String(approvedUser.id) : (userChoice || 'jira-user')) : userChoice;
  const managers = useEmployeeManagersQuery(employee?.id, {skip: self, refetchOnMountOrArgChange: true});
  const [managerChoice, setManagerId] = useState(null);
  const eligibleManagers = (managers.data || []).filter(manager => String(manager.userId) !== String(userId));
  const managerId = managerChoice ?? (employee?.managerEmployeeId ? String(employee.managerEmployeeId) : eligibleManagers.length === 1 ? String(eligibleManagers[0].id) : '');
  const selectedManager = managers.data?.find(manager => String(manager.id) === managerId);
  const [version, setVersion] = useState(employee?.version);
  const [photoUrl, setPhotoUrl] = useState(employee?.profilePhotoUrl || null);
  const [photoError, setPhotoError] = useState(null);
  const [uploading, setUploading] = useState(false);
  const catalog = useEmployeeCareerCatalogQuery(undefined, { skip: self, refetchOnMountOrArgChange: true });
  const [selectedDepartment, setSelectedDepartment] = useState(null);
  const departmentValue =
    selectedDepartment ??
    (initialValues?.departmentId && !initialValues?.catalogDepartmentId
      ? `reference:${initialValues.departmentId}`
      : employee?.careerProfileId
      ? `profile:${employee.careerProfileId}`
      : employee?.departmentId
        ? `reference:${employee.departmentId}`
        : '');
  const departmentProfileId = departmentValue.startsWith('profile:')
    ? Number(departmentValue.split(':')[1])
    : null;
  const departmentName = departmentProfileId
    ? catalog.data?.profiles?.find(p => p.id === departmentProfileId)?.name
    : references.find(r => `reference:${r.id}` === departmentValue)?.name;
  const mappedProfileId = departmentProfileId ?? initialValues?.careerProfileId ?? catalog.data?.profiles?.find(p => p.name === departmentName)?.id;
  const availableGrades = (catalog.data?.grades || []).filter(g =>
    !!mappedProfileId && catalog.data?.levels?.some(l => l.careerProfileId === mappedProfileId && l.gradeId === g.id),
  );
  const [selectedGrade, setSelectedGrade] = useState(null);
  const [selectedJob, setSelectedJob] = useState(
    employee?.careerLevelId
      ? `level:${employee.careerLevelId}`
      : employee?.jobTitleId
        ? `reference:${employee.jobTitleId}`
        : '',
  );
  const existingLevel = catalog.data?.levels?.find((l) => l.id === employee?.careerLevelId);
  const existingGrade = catalog.data?.grades?.find(
    (g) => g.code === references.find((r) => r.id === employee?.gradeId)?.name,
  );
  const gradeValue =
    selectedGrade ??
    (existingLevel
      ? String(existingLevel.gradeId)
      : existingGrade
        ? String(existingGrade.id)
        : employee?.gradeId
          ? `reference:${employee.gradeId}`
          : '');
  const gradeCode = gradeValue.startsWith('reference:')
    ? references.find((r) => r.id === Number(gradeValue.split(':')[1]))?.name
    : catalog.data?.grades?.find((g) => String(g.id) === gradeValue)?.code;
  const gradeDesignations = { T0: 'Intern', T1: 'Junior', T2: '', T3: 'Senior', T4: 'Staff' };
  const mappedDesignation = gradeDesignations[gradeCode];
  const careerLevels = (catalog.data?.levels || []).filter(
    (l) =>
      !!gradeValue && String(l.gradeId) === gradeValue &&
      !!mappedProfileId && l.careerProfileId === mappedProfileId,
  );
  const [employmentType, setEmploymentType] = useState(employee?.employmentType || 'FULL_TIME');
  const [employmentStatus, setEmploymentStatus] = useState(employee?.employmentStatus || 'ACTIVE');
  const [designationValue] = useState(
    employee?.designationId
      ? `reference:${employee.designationId}`
      : (employee?.designation || '')
  );
  const [saved, setSaved] = useState(false);
  const [validationError, setValidationError] = useState(null);
  return (
    <section className="panel employee-panel employee-editor-panel">
      <header className="employee-editor-header">
        <div>
          <h2>{self ? 'Edit my profile' : isEditing ? 'Edit employee' : 'Add employee'}</h2>
          <p>{self ? 'Update your personal details, contact information and profile picture.' : isEditing ? 'Update employee record, role assignment and organization details.' : 'Create a new employee profile linked to an organization user.'}</p>
        </div>
        {onClose && (
          <button type="button" className="employee-editor-close-btn" onClick={onClose} aria-label="Close editor" title="Close editor">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        )}
      </header>

      {!self && !isEditing && onSwitchToBulk && (
        <div className="employee-creation-mode-tabs" role="tablist" aria-label="Creation mode">
          <button
            type="button"
            className="mode-tab is-active"
            aria-pressed="true"
          >
            Single employee
          </button>
          <button
            type="button"
            className="mode-tab"
            aria-pressed="false"
            onClick={onSwitchToBulk}
          >
            Bulk add employees
          </button>
        </div>
      )}
      {!self && !isEditing && (
        <div>
          <QueryState query={users}>
            <CustomSelect
              label="Linked login user"
              name="userId"
              value={userId}
              disabled={Boolean(approvedWorkEmail)}
              placeholder={approvedWorkEmail ? `${approvedWorkEmail} (from Jira request)` : "Select a user"}
              onChange={(e) => setUserId(e.target.value)}
              options={(approvedWorkEmail
                ? (approvedUser
                    ? [{ value: String(approvedUser.id), label: `${approvedUser.displayName} · ${approvedUser.email}` }]
                    : [{ value: 'jira-user', label: `${approvedWorkEmail} · Jira onboarding` }])
                : (users.data?.items || []).map((u) => ({
                    value: String(u.id),
                    label: `${u.displayName} · ${u.email}`,
                  })))}
            />
            {!approvedWorkEmail && users.data?.items?.length === 0 && <p role="status">No eligible users found. Users who already have employee profiles are excluded. Newly available users load automatically when you reopen this form or return to this window.</p>}
            {!approvedWorkEmail && users.data?.totalPages > 1 && (
              <Pagination data={users.data} onPage={setPage} busy={users.isFetching} />
            )}
          </QueryState>
          <p>
            {approvedWorkEmail
              ? `Using work email from approved Jira request: ${approvedWorkEmail}.`
              : 'Only existing organization users can be linked. Work email comes from their verified login identity.'}
          </p>
        </div>
      )}
      <form
        className={`employee-form employee-editor-form ${self ? 'employee-editor-form-self' : ''}`}
        onSubmit={async (e) => {
          e.preventDefault();
          const body = Object.fromEntries(new FormData(e.currentTarget));
          setValidationError(null);

          if (!self) {
            if (!isEditing && !approvedWorkEmail && (!userId || Number(userId) <= 0)) {
              setValidationError({ message: 'Please select a linked login user for this employee profile.' });
              return;
            }
            if (['ACTIVE', 'ON_LEAVE'].includes(employmentStatus) && body.endDate && body.endDate < new Date().toISOString().slice(0, 10)) {
              setValidationError({ message: 'Active employment cannot have a past employment end date. Clear Employment end date if the employee still works here. Record the end of an internship or a promotion in Career history.' });
              e.currentTarget.elements.namedItem('endDate')?.focus();
              return;
            }

            const level = catalog.data?.levels?.find((l) => `level:${l.id}` === selectedJob);
            if (gradeValue && !gradeValue.startsWith('reference:') && careerLevels.length > 0 && !level) {
              setValidationError({
                message: `Please select a valid job title matching grade ${gradeCode || ''} (e.g. ${careerLevels[0]?.careerTitle || ''}).`
              });
              return;
            }

            if (body.careerEffectiveDate) {
              const todayIso = new Date().toISOString().slice(0, 10);
              if (body.careerEffectiveDate > todayIso) {
                setValidationError({ message: 'Career change effective date cannot be in the future.' });
                e.currentTarget.elements.namedItem('careerEffectiveDate')?.focus();
                return;
              }
              if (employee?.joiningDate && body.careerEffectiveDate < employee.joiningDate.slice(0, 10)) {
                setValidationError({ message: `Career change effective date cannot be before joining date (${employee.joiningDate.slice(0, 10)}).` });
                e.currentTarget.elements.namedItem('careerEffectiveDate')?.focus();
                return;
              }
            }
          }

          let payload;
          if (self) {
            payload = {
              preferredName: body.preferredName || null,
              phone: body.phone || null,
              profilePhotoUrl: photoUrl || null,
              location: body.location || '',
              aadharNumber: body.aadharNumber?.trim() || null,
              permanentAddress: body.permanentAddress?.trim() || null,
              emergencyContactName: body.emergencyContactName?.trim() || null,
              emergencyContactPhone: body.emergencyContactPhone?.trim() || null,
              emergencyContactRelation: body.emergencyContactRelation?.trim() || null,
            };
            if (body.locationId) payload.locationId = Number(body.locationId);
          } else {
            const level = catalog.data?.levels?.find((l) => `level:${l.id}` === selectedJob);
            const isRefDesignation = body.designation?.startsWith('reference:');
            const resolvedDesignationId = isRefDesignation ? Number(body.designation.split(':')[1]) : null;
            const resolvedDesignation = isRefDesignation ? null : (mappedDesignation !== undefined ? mappedDesignation : body.designation || null);

            payload = {
              userId: Number(userId) > 0 ? Number(userId) : 0,
              employeeCode: body.employeeCode?.trim() || '',
              firstName: body.firstName?.trim() || '',
              lastName: body.lastName?.trim() || '',
              joiningDate: body.joiningDate || new Date().toISOString().slice(0, 10),
              preferredName: body.preferredName?.trim() || null,
              phone: body.phone?.trim() || null,
              profilePhotoUrl: photoUrl || null,
              aadharNumber: body.aadharNumber?.trim() || null,
              permanentAddress: body.permanentAddress?.trim() || null,
              emergencyContactName: body.emergencyContactName?.trim() || null,
              emergencyContactPhone: body.emergencyContactPhone?.trim() || null,
              emergencyContactRelation: body.emergencyContactRelation?.trim() || null,
              departmentId: departmentValue.startsWith('reference:')
                ? Number(departmentValue.split(':')[1])
                : null,
              disciplineId: employee?.disciplineId ?? null,
              jobTitleId: selectedJob.startsWith('reference:')
                ? Number(selectedJob.split(':')[1])
                : null,
              designationId: resolvedDesignationId,
              gradeId: gradeValue.startsWith('reference:')
                ? Number(gradeValue.split(':')[1])
                : null,
              locationId: body.locationId ? Number(body.locationId) : null,
              managerEmployeeId: managerId ? Number(managerId) : null,
              employmentType: employmentType || 'FULL_TIME',
              employmentStatus: employmentStatus || 'ACTIVE',
              endDate: body.endDate || null,
              careerProfileId: departmentProfileId ?? level?.careerProfileId ?? initialValues?.careerProfileId ?? null,
              careerLevelId: level?.id ?? null,
              location: body.location ?? '',
              catalogGradeId: gradeValue && !gradeValue.startsWith('reference:') ? Number(gradeValue) : null,
              catalogDepartmentId: departmentProfileId,
              designation: resolvedDesignation,
              careerEffectiveDate: body.careerEffectiveDate || null,
              careerChangeNote: body.careerChangeNote?.trim() || null,
            };
          }

          try {
            if (submitOverride) await submitOverride(payload);
            else await save({
              path: self ? '/me/profile' : isEditing ? `/${employee.id}` : '',
              method: isEditing || self ? 'PUT' : 'POST',
              version,
              body: payload,
            }).unwrap();
            setSaved(true);
            if (self || (employee && (String(employee.userId) === String(session?.user?.id) || String(employee.id) === String(session?.user?.employeeId)))) {
              updateUser?.({ profilePhotoUrl: photoUrl });
            }
            onSaved?.();
            onClose?.();
          } catch {
            /* error below */
          }
        }}
      >
        {self ? (
          <div className="employee-editor-self-container">
            <div className="employee-photo-section-card">
              <div className="photo-preview-box">
                {photoUrl ? (
                  <EmployeePhoto
                    src={photoUrl}
                    alt="Current profile photo"
                    className="photo-avatar-image"
                  />
                ) : (
                  <div className="photo-placeholder-avatar" aria-hidden="true">
                    <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
                      <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                      <circle cx="12" cy="7" r="4" />
                    </svg>
                  </div>
                )}
              </div>
              <div className="photo-info-actions">
                <div className="photo-info-text">
                  <h3>Profile photo</h3>
                  <p>JPG, PNG or WebP up to 2 MB. Saved immediately upon upload.</p>
                </div>
                <div className="photo-action-buttons">
                  <label className={`button photo-upload-action-btn ${uploading ? 'is-uploading' : ''}`}>
                    <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                      <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                      <polyline points="17 8 12 3 7 8" />
                      <line x1="12" y1="3" x2="12" y2="15" />
                    </svg>
                    <span>{uploading ? 'Uploading…' : photoUrl ? 'Change photo' : 'Upload photo'}</span>
                    <input
                      aria-label="Upload profile photo"
                      type="file"
                      accept="image/jpeg,image/png,image/webp"
                      disabled={uploading || (result.isLoading || submitBusy)}
                      className="visually-hidden-file-input"
                      onChange={async (event) => {
                        const file = event.target.files?.[0];
                        event.target.value = '';
                        if (!file) return;
                        if (file.size > 2 * 1024 * 1024) {
                          setPhotoError('Choose an image up to 2 MB.');
                          return;
                        }
                        setPhotoError(null);
                        setUploading(true);
                        const body = new FormData();
                        body.append('file', file);
                        try {
                          const response = await save({
                            path: `/${employee.id}/photo`,
                            body,
                            version,
                          }).unwrap();
                          setPhotoUrl(response.profilePhotoUrl);
                          setVersion(response.version);
                          if (self || (employee && (String(employee.userId) === String(session?.user?.id) || String(employee.id) === String(session?.user?.employeeId)))) {
                            updateUser?.({ profilePhotoUrl: response.profilePhotoUrl });
                          }
                        } catch {
                          setPhotoError('Photo upload failed. Please try again.');
                        } finally {
                          setUploading(false);
                        }
                      }}
                    />
                  </label>
                  {photoUrl && (
                    <button
                      type="button"
                      className="button photo-remove-action-btn"
                      disabled={uploading || (result.isLoading || submitBusy)}
                      onClick={() => {
                        setPhotoUrl(null);
                        if (self || (employee && (String(employee.userId) === String(session?.user?.id) || String(employee.id) === String(session?.user?.employeeId)))) {
                          updateUser?.({ profilePhotoUrl: null });
                        }
                      }}
                      title="Remove profile photo"
                    >
                      Remove
                    </button>
                  )}
                </div>
                {photoError && <p role="alert" className="custom-photo-error">{photoError}</p>}
              </div>
            </div>

            <div className="employee-self-fields-grid">
              <Field
                label="Preferred name"
                name="preferredName"
                maxLength={100}
                defaultValue={employee?.preferredName || ''}
              />
              <Field
                label="Phone"
                name="phone"
                type="tel"
                maxLength={40}
                defaultValue={employee?.phone || ''}
              />
              <Field
                label="Location"
                name="location"
                maxLength={150}
                placeholder="Enter city or office location"
                defaultValue={employee?.location || references.find((r) => r.id === employee?.locationId)?.name || ''}
              />
              <Field
                label="Aadhaar number"
                name="aadharNumber"
                maxLength={50}
                placeholder="e.g. 1234 5678 9012"
                defaultValue={employee?.aadharNumber || ''}
              />
              <Field
                label="Permanent address"
                name="permanentAddress"
                maxLength={1000}
                placeholder="Enter permanent residential address"
                defaultValue={employee?.permanentAddress || ''}
              />
              <Field
                label="Emergency contact name"
                name="emergencyContactName"
                maxLength={200}
                placeholder="Full name of emergency contact"
                defaultValue={employee?.emergencyContactName || ''}
              />
              <Field
                label="Relationship"
                name="emergencyContactRelation"
                maxLength={100}
                placeholder="e.g. Father, Mother, Spouse, Sibling"
                defaultValue={employee?.emergencyContactRelation || ''}
              />
              <Field
                label="Emergency contact phone"
                name="emergencyContactPhone"
                type="tel"
                maxLength={50}
                placeholder="Emergency contact phone number"
                defaultValue={employee?.emergencyContactPhone || ''}
              />
            </div>
          </div>
        ) : (
          <>
            <Field
              label="Employee code"
              name="employeeCode"
              required
              maxLength={80}
              value={currentEmployeeCode}
              onChange={(e) => setUserCode(e.target.value)}
            />
            {[
              ['firstName', 'First name'],
              ['lastName', 'Last name'],
            ].map(([key, title]) => (
              <Field
                key={key}
                label={title}
                name={key}
                required
                maxLength={100}
                defaultValue={employee?.[key] || ''}
              />
            ))}
            <Field
              label="Joining date"
              name="joiningDate"
              type="date"
              required
              defaultValue={employee?.joiningDate || ''}
            />
            <Field
              label="Employment end date (only when leaving the company)"
              name="endDate"
              type="date"
              defaultValue={employee?.endDate || ''}
            />
            <p>Leave employment end date blank while working here. An internship or promotion ends a career period, not your employment.</p>
            <Field
              label="Preferred name"
              name="preferredName"
              maxLength={100}
              defaultValue={employee?.preferredName || ''}
            />
            <Field
              label="Phone"
              name="phone"
              type="tel"
              maxLength={40}
              defaultValue={employee?.phone || ''}
            />
            <Field label="Profile photo">
              {isEditing ? (
                <div className="custom-photo-uploader">
                  <div className="custom-photo-preview-wrap">
                    {photoUrl ? (
                      <EmployeePhoto
                        src={photoUrl}
                        alt="Current profile photo"
                        className="custom-photo-avatar"
                      />
                    ) : (
                      <div className="custom-photo-placeholder" aria-hidden="true">
                        <svg width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round">
                          <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                          <circle cx="12" cy="7" r="4" />
                        </svg>
                      </div>
                    )}
                  </div>
                  <div className="custom-photo-meta">
                    <div className="custom-photo-actions">
                      <label className={`button custom-photo-upload-btn ${uploading ? 'is-uploading' : ''}`}>
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
                          <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                          <polyline points="17 8 12 3 7 8" />
                          <line x1="12" y1="3" x2="12" y2="15" />
                        </svg>
                        <span>{uploading ? 'Uploading…' : photoUrl ? 'Change photo' : 'Upload photo'}</span>
                        <input
                          aria-label="Upload profile photo"
                          type="file"
                          accept="image/jpeg,image/png,image/webp"
                          disabled={uploading || (result.isLoading || submitBusy)}
                          className="visually-hidden-file-input"
                          onChange={async (event) => {
                            const file = event.target.files?.[0];
                            event.target.value = '';
                            if (!file) return;
                            if (file.size > 2 * 1024 * 1024) {
                              setPhotoError('Choose an image up to 2 MB.');
                              return;
                            }
                            setPhotoError(null);
                            setUploading(true);
                            const body = new FormData();
                            body.append('file', file);
                            try {
                              const response = await save({
                                path: `/${employee.id}/photo`,
                                body,
                                version,
                              }).unwrap();
                              setPhotoUrl(response.profilePhotoUrl);
                              setVersion(response.version);
                              if (self || (employee && (String(employee.userId) === String(session?.user?.id) || String(employee.id) === String(session?.user?.employeeId)))) {
                                updateUser?.({ profilePhotoUrl: response.profilePhotoUrl });
                              }
                            } catch {
                              setPhotoError('Photo upload failed. Please try again.');
                            } finally {
                              setUploading(false);
                            }
                          }}
                        />
                      </label>
                      {photoUrl && (
                        <button
                          type="button"
                          className="button custom-photo-remove-btn"
                          disabled={uploading || (result.isLoading || submitBusy)}
                          onClick={() => {
                            setPhotoUrl(null);
                            if (self || (employee && (String(employee.userId) === String(session?.user?.id) || String(employee.id) === String(session?.user?.employeeId)))) {
                              updateUser?.({ profilePhotoUrl: null });
                            }
                          }}
                          title="Remove profile photo"
                        >
                          Remove
                        </button>
                      )}
                    </div>
                    <small className="custom-photo-hint">
                      PNG, JPEG or WebP, up to 2 MB. Saved immediately.
                    </small>
                  </div>
                </div>
              ) : (
                <p>Save the employee profile first, then open Edit profile to upload a photo.</p>
              )}
              {photoError && <p role="alert" className="custom-photo-error">{photoError}</p>}
            </Field>
            <Field
              label="Location"
              name="location"
              maxLength={150}
              placeholder="Enter city or office location"
              defaultValue={employee?.location || references.find((r) => r.id === employee?.locationId)?.name || ''}
            />
            <Field
              label="Aadhaar number"
              name="aadharNumber"
              maxLength={50}
              placeholder="e.g. 1234 5678 9012"
              defaultValue={employee?.aadharNumber || ''}
            />
            <Field
              label="Permanent address"
              name="permanentAddress"
              maxLength={1000}
              placeholder="Enter permanent residential address"
              defaultValue={employee?.permanentAddress || ''}
            />
            <Field
              label="Emergency contact name"
              name="emergencyContactName"
              maxLength={200}
              placeholder="Full name of emergency contact"
              defaultValue={employee?.emergencyContactName || ''}
            />
            <Field
              label="Relationship"
              name="emergencyContactRelation"
              maxLength={100}
              placeholder="e.g. Father, Mother, Spouse, Sibling"
              defaultValue={employee?.emergencyContactRelation || ''}
            />
            <Field
              label="Emergency contact phone"
              name="emergencyContactPhone"
              type="tel"
              maxLength={50}
              placeholder="Emergency contact phone number"
              defaultValue={employee?.emergencyContactPhone || ''}
            />
          </>
        )}
        {!self && (
          <div className="employee-catalog-fields">
            <QueryState query={catalog}>
              <CustomSelect
                label="Department"
                name="department"
                value={departmentValue}
                placeholder="Select department"
                onChange={(e) => {
                  setSelectedDepartment(e.target.value);
                  setSelectedGrade('');
                  setSelectedJob('');
                }}
                options={[
                  ...(catalog.data?.profiles || []).map((p) => ({
                    value: `profile:${p.id}`,
                    label: p.name,
                  })),
                  ...references
                    .filter((r) => r.kind === 'DEPARTMENT')
                    .map((r) => ({
                      value: `reference:${r.id}`,
                      label: r.name,
                    })),
                ]}
              />

              <CustomSelect
                label="Grade"
                name="grade"
                value={gradeValue}
                placeholder={!departmentValue ? 'Select department first' : availableGrades.length ? 'Select grade' : 'No grades configured for this department'}
                onChange={(e) => {
                  const newGradeId = e.target.value;
                  setSelectedGrade(newGradeId);
                  const matching = (catalog.data?.levels || []).filter(
                    (l) => String(l.gradeId) === newGradeId && !!mappedProfileId && l.careerProfileId === mappedProfileId,
                  );
                  if (matching.length === 1) {
                    setSelectedJob(`level:${matching[0].id}`);
                  } else {
                    setSelectedJob('');
                  }
                }}
                options={availableGrades.map((g) => ({
                  value: String(g.id),
                  label: `${g.code}${Object.hasOwn(gradeDesignations, g.code) ? ` — ${gradeDesignations[g.code] || 'No prefix'}` : ''}`,
                }))}
              />

              <CustomSelect
                label="Job title"
                name="jobTitle"
                value={selectedJob}
                placeholder={!departmentValue ? 'Select department first' : !gradeValue ? 'Select grade first' : careerLevels.length ? 'Select job title' : 'No job titles configured for this grade'}
                onChange={(e) => {
                  setSelectedJob(e.target.value);
                  const level = catalog.data?.levels?.find(
                    (l) => `level:${l.id}` === e.target.value,
                  );
                  if (level) setSelectedGrade(String(level.gradeId));
                }}
                options={[
                  ...careerLevels.map((l) => ({
                    value: `level:${l.id}`,
                    label: l.careerTitle,
                  })),
                  ...references
                    .filter((r) => r.kind === 'JOB_TITLE' && selectedDepartment === null && selectedGrade === null && `reference:${r.id}` === selectedJob)
                    .map((r) => ({
                      value: `reference:${r.id}`,
                      label: r.name,
                    })),
                ]}
              />

              {gradeValue && careerLevels.length === 0 && (
                <p>
                  No job titles are configured for this department and grade.
                </p>
              )}
            </QueryState>
          </div>
        )}
        {!self && <input type="hidden" name="designation" value={mappedDesignation ?? designationValue} />}
        {!self && isEditing && <>
          <Field label="Career change effective date" name="careerEffectiveDate" type="date" min={employee.joiningDate?.slice(0, 10)} />
          <Field label="Career change note" name="careerChangeNote" maxLength={1000} placeholder="For example: promoted from Intern to Junior" />
          <p>Enter an effective date when changing grade, designation, department or job title. Previous details are kept in Career history.</p>
        </>}
        {!self && (
          <>
            <div>
              <CustomSelect
                label="Reporting manager"
                name="managerEmployeeId"
                value={managerId}
                placeholder="No manager assigned"
                onChange={(event) => setManagerId(event.target.value)}
                options={[
                  ...(managerId && !selectedManager
                    ? [{ value: managerId, label: `Current manager (ID ${managerId})` }]
                    : []),
                  ...eligibleManagers.map((manager) => ({
                    value: String(manager.id),
                    label: `${manager.firstName} ${manager.lastName} · ${manager.employeeCode}`,
                  })),
                ]}
              />
              {selectedManager && <small style={{ marginTop: '4px', display: 'block', color: '#64748b' }}>{selectedManager.firstName} {selectedManager.lastName} · {selectedManager.workEmail}</small>}
              {managers.isLoading && <small style={{ marginTop: '4px', display: 'block' }}>Loading managers…</small>}
              {managers.error && <ErrorState error={managers.error} />}
              {managers.data && eligibleManagers.length === 0 && <small style={{ marginTop: '4px', display: 'block', color: '#64748b' }}>No eligible active employee profiles found.</small>}
            </div>
            <CustomSelect
              label="Employment type"
              name="employmentType"
              value={employmentType}
              placeholder="Select employment type"
              onChange={(e) => setEmploymentType(e.target.value)}
              options={['FULL_TIME', 'PART_TIME', 'CONTRACT', 'INTERN'].map((x) => ({
                value: x,
                label: x,
              }))}
            />
            <CustomSelect
              label="Employment status"
              name="employmentStatus"
              value={employmentStatus}
              placeholder="Select employment status"
              onChange={(e) => setEmploymentStatus(e.target.value)}
              options={['ACTIVE', 'ON_LEAVE', 'INACTIVE', 'TERMINATED'].map((x) => ({
                value: x,
                label: x,
              }))}
            />
          </>
        )}
        <div className="actions">
          <button className="primary" disabled={(result.isLoading || submitBusy) || (!self && !userId)}>
            {submitLabel}
          </button>
          <button type="button" onClick={onClose}>
            Cancel
          </button>
        </div>
      </form>
      <ErrorState error={validationError || submitError || result.error} />
      {result.error?.status === 409 && employee && result.error?.code !== 'EMPLOYEE_ALREADY_EXISTS' && (
        <p role="alert">
          This profile changed while you were editing. Copy your changes, cancel, and reopen the
          editor after refreshing the profile.
        </p>
      )}
      {saved && <p role="status">Profile saved.</p>}
    </section>
  );
}
