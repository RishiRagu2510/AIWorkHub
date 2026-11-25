import { useState } from 'react';
import { Field, CustomSelect, ErrorState } from '../../components/common';
import './onboarding-fields.scss';
import { useOnboardingOptionsQuery } from '../../services/api/onboarding';
import { useRunAthennaCopilotMutation } from '../../services/api/agent';
import { FeatureGuard } from '../auth/FeatureGuard';
import { ATHENNA } from '../auth/features';
export function onboardingPayload(values) {
  const id = (name) => (values[`employee.${name}`] ? Number(values[`employee.${name}`]) : null);
  const department = values['employee.departmentId'] || '';
  const text = (name) => values[`employee.${name}`]?.trim() || null;
  return {
    workEmail: values.onboardingEmail?.trim(),
    reason: null,
    notes: null,
    requestedAccess: null,
    teamId: null,
    employee: {
      userId: 0,
      employeeCode: text('employeeCode') || '',
      firstName: text('firstName'),
      lastName: text('lastName'),
      preferredName: text('preferredName'),
      phone: text('phone'),
      joiningDate: text('joiningDate'),
      employmentType: text('employmentType'),
      employmentStatus: text('employmentStatus'),
      location: text('location'),
      departmentId: department.startsWith('reference:') ? Number(department.split(':')[1]) : null,
      catalogDepartmentId: department.startsWith('profile:')
        ? Number(department.split(':')[1])
        : null,
      careerProfileId: id('careerProfileId'),
      careerLevelId: id('careerLevelId'),
      catalogGradeId: id('catalogGradeId'),
      managerEmployeeId: id('managerEmployeeId'),
      designationId: text('designation')?.startsWith('reference:')
        ? Number(text('designation').split(':')[1])
        : null,
      designation: text('designation')?.startsWith('reference:') ? null : text('designation'),
    },
  };
}
export default function OnboardingFields({ initial = {} }) {
  const query = useOnboardingOptionsQuery();
  const data = query.data;
  const [profile, setProfile] = useState(initial['employee.careerProfileId'] || '');
  const [level, setLevel] = useState(initial['employee.careerLevelId'] || '');
  const [grade, setGrade] = useState(initial['employee.catalogGradeId'] || null);
  const [manager, setManager] = useState(initial['employee.managerEmployeeId'] || '');
  const [showAiModal, setShowAiModal] = useState(false);
  const [aiText, setAiText] = useState('');
  const [aiError, setAiError] = useState(null);
  const [extractedValues, setExtractedValues] = useState({});
  const [runCopilot, { isLoading: isAiExtracting }] = useRunAthennaCopilotMutation();

  const handleAiExtract = async () => {
    if (!aiText.trim()) return;
    setAiError(null);
    try {
      const res = await runCopilot({
        feature: 'ExtractOnboardingData',
        inputText: aiText,
      }).unwrap();

      if (res?.outputText) {
        // Find inputs in form and populate
        const text = res.outputText;
        const updates = {};
        
        // Match common fields from extracted text or JSON
        const emailMatch = text.match(/[\w.-]+@[\w.-]+\.\w+/);
        if (emailMatch) updates.onboardingEmail = emailMatch[0];

        const phoneMatch = text.match(/(?:\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}/);
        if (phoneMatch) updates['employee.phone'] = phoneMatch[0];

        // Name heuristics if in JSON or lines
        const nameMatch = text.match(/Full Name[:"\s]+([A-Za-z]+)\s+([A-Za-z]+)/i);
        if (nameMatch) {
          updates['employee.firstName'] = nameMatch[1];
          updates['employee.lastName'] = nameMatch[2];
        }

        setExtractedValues((prev) => ({ ...prev, ...updates }));

        // Apply directly to DOM inputs if available
        Object.entries(updates).forEach(([key, val]) => {
          const el = document.querySelector(`[name="${key}"]`);
          if (el) el.value = val;
        });

        setShowAiModal(false);
        setAiText('');
      }
    } catch {
      setAiError('Failed to parse candidate details with Athenna.');
    }
  };
  const selected = data?.levels?.find((l) => String(l.id) === String(level));
  const currentGrade = grade ?? selected?.gradeId ?? '';
  const profileLevels = (data?.levels || []).filter(
    (l) => String(l.careerProfileId) === String(profile),
  );
  const managerRecord = data?.managers?.find((m) => String(m.id) === String(manager));
  const fields = [
    ['Employee code (if known)', 'employeeCode', false],
    ['First name', 'firstName', true],
    ['Last name', 'lastName', true],
    ['Preferred name', 'preferredName', false],
    ['Phone number', 'phone', false],
  ];

  const departmentOptions = [
    ...(data?.references || [])
      .filter((r) => r.kind === 'DEPARTMENT')
      .map((r) => ({ value: `reference:${r.id}`, label: r.name })),
    ...(data?.profiles || [])
      .map((p) => ({ value: `profile:${p.id}`, label: p.name })),
  ];

  return (
    <section className="onboarding-fields" aria-label="Employee onboarding information">
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
        <h2 style={{ margin: 0 }}>Employee information</h2>
        <FeatureGuard feature={ATHENNA}>
          <button
          type="button"
          onClick={() => setShowAiModal(true)}
          style={{
            background: 'linear-gradient(135deg, rgba(79, 70, 229, 0.08) 0%, rgba(124, 58, 237, 0.08) 100%)',
            border: '1px solid rgba(124, 58, 237, 0.25)',
            borderRadius: '16px',
            padding: '4px 12px',
            fontSize: '0.8rem',
            fontWeight: 600,
            color: 'var(--primary, #4f46e5)',
            cursor: 'pointer',
            display: 'inline-flex',
            alignItems: 'center',
            gap: '5px',
          }}
          title="Extract candidate information from resume/offer text using Athenna"
        >
          <span>✦</span>
          <span>AI Pre-fill with Athenna</span>
          </button>
        </FeatureGuard>
      </div>

      {showAiModal && (
        <div style={{
          padding: '16px',
          marginBottom: '16px',
          background: '#f8fafc',
          border: '1px solid #cbd5e1',
          borderRadius: '8px'
        }}>
          <h4 style={{ margin: '0 0 8px 0', fontSize: '0.9rem', color: '#1e293b' }}>
            Paste Candidate Resume / Bio / Email
          </h4>
          <p style={{ margin: '0 0 8px 0', fontSize: '0.78rem', color: '#64748b' }}>
            Athenna will automatically parse the candidate's name, email, phone, and role.
          </p>
          <textarea
            rows={4}
            value={aiText}
            onChange={(e) => setAiText(e.target.value)}
            placeholder="Paste raw resume or onboarding text here..."
            style={{ width: '100%', boxSizing: 'border-box', marginBottom: '8px' }}
          />
          {aiError && <p style={{ color: '#ef4444', fontSize: '0.8rem', margin: '0 0 8px 0' }}>{aiError}</p>}
          <div style={{ display: 'flex', gap: '8px' }}>
            <button
              type="button"
              className="primary"
              onClick={handleAiExtract}
              disabled={isAiExtracting || !aiText.trim()}
              style={{ fontSize: '0.82rem', padding: '6px 14px' }}
            >
              {isAiExtracting ? 'Extracting…' : 'Extract & Pre-fill'}
            </button>
            <button
              type="button"
              onClick={() => { setShowAiModal(false); setAiText(''); }}
              style={{ fontSize: '0.82rem', padding: '6px 14px' }}
            >
              Cancel
            </button>
          </div>
        </div>
      )}

      <ErrorState error={query.error} retry={query.refetch} />
      {!data && <p role="status">Loading onboarding reference data…</p>}
      <div className="form-grid">
        {fields.map(([label, name, required]) => (
          <Field
            key={name}
            label={label}
            name={`employee.${name}`}
            required={required}
            maxLength={name === 'phone' ? 40 : name === 'employeeCode' ? 80 : 100}
            defaultValue={extractedValues[`employee.${name}`] || initial[`employee.${name}`] || ''}
          />
        ))}
        <Field
          label="Work email"
          name="onboardingEmail"
          type="email"
          required
          maxLength={320}
          defaultValue={extractedValues.onboardingEmail || initial.onboardingEmail || ''}
        />
      </div>
      <h2>Organization and career</h2>
      <div className="form-grid">
        <CustomSelect
          label="Department"
          name="employee.departmentId"
          required
          placeholder="Select department"
          defaultValue={initial['employee.departmentId'] || ''}
          options={departmentOptions}
        />
        <CustomSelect
          label="Career profile"
          name="employee.careerProfileId"
          required
          placeholder="Select career profile"
          value={profile}
          onChange={(e) => {
            setProfile(e.target.value);
            setLevel('');
            setGrade('');
          }}
          options={(data?.profiles || []).map((p) => ({ value: p.id, label: p.name }))}
        />
        <CustomSelect
          label="Grade"
          name="employee.catalogGradeId"
          required
          placeholder={profile ? 'Select grade' : 'Select career profile first'}
          disabled={!profile}
          value={currentGrade}
          onChange={(e) => {
            setGrade(e.target.value);
            setLevel('');
          }}
          options={Array.from(
            new Map(
              profileLevels.map((l) => [l.gradeId, { value: l.gradeId, label: l.grade }]),
            ).values(),
          )}
        />
        <CustomSelect
          label="Job title"
          name="employee.careerLevelId"
          required
          placeholder={currentGrade ? 'Select job title' : 'Select grade first'}
          disabled={!currentGrade}
          value={level}
          onChange={(e) => setLevel(e.target.value)}
          options={(data?.levels || [])
            .filter(
              (l) =>
                String(l.careerProfileId) === String(profile) &&
                String(l.gradeId) === String(currentGrade),
            )
            .map((l) => ({ value: l.id, label: l.careerTitle }))}
        />
        <div>
          <CustomSelect
            label="Reporting manager"
            name="employee.managerEmployeeId"
            required
            placeholder="Select an eligible manager"
            value={manager}
            onChange={(e) => setManager(e.target.value)}
            options={(data?.managers || []).map((m) => ({
              value: m.id,
              label: `${m.firstName} ${m.lastName} · ${m.employeeCode}`,
            }))}
          />
          {managerRecord && (
            <p className="onboarding-manager-hint">
              Manager employee ID: {managerRecord.employeeCode} · Email: {managerRecord.workEmail}
            </p>
          )}
        </div>
      </div>
      <h2>Employment</h2>
      <div className="form-grid">
        <Field
          label="Joining date / expected joining date"
          name="employee.joiningDate"
          type="date"
          required
          defaultValue={initial['employee.joiningDate'] || ''}
        />
        <CustomSelect
          label="Employment type"
          name="employee.employmentType"
          required
          defaultValue={initial['employee.employmentType'] || 'FULL_TIME'}
          options={(data?.employmentTypes || []).map((value) => ({ value, label: value }))}
        />
        <CustomSelect
          label="Employment status"
          name="employee.employmentStatus"
          required
          defaultValue={initial['employee.employmentStatus'] || 'ACTIVE'}
          options={(data?.employmentStatuses || []).map((value) => ({ value, label: value }))}
        />
        <Field
          label="Work location (if applicable)"
          name="employee.location"
          maxLength={150}
          defaultValue={initial['employee.location'] || ''}
        />
      </div>
    </section>
  );
}
