import React, { useState } from 'react';
import { useLazyEmployeeQuery, useSaveEmployeeMutation } from '../services/api/employees';

export const LANGUAGE_LEVELS = ['Elementary', 'Limited Working', 'Professional Working', 'Full Professional', 'Native / Bilingual'];

const messageOf = (error) => error?.message || error?.data?.message || 'The change was not saved.';

const DATE_FORMAT = new Intl.DateTimeFormat('en', { month: 'short', year: 'numeric', timeZone: 'UTC' });
const month = (value) => (value ? DATE_FORMAT.format(new Date(`${value}T00:00:00Z`)) : '');
const levelOf = (value) => {
  const n = Number(value);
  return Number.isInteger(n) && n >= 1 && n <= 5 ? LANGUAGE_LEVELS[n - 1] : value || '';
};

// Field kinds: text, textarea, date, url, level. `max` mirrors the limits the Persona service enforces.
const SECTIONS = {
  personal: {
    title: 'Update my personal details',
    icon: '🪪',
    fields: [
      { key: 'preferredName', label: 'Preferred name', max: 100 },
      { key: 'phone', label: 'Phone', max: 40 },
      { key: 'emergencyContactName', label: 'Emergency contact name', max: 200 },
      { key: 'emergencyContactPhone', label: 'Emergency contact phone', max: 50 },
      { key: 'emergencyContactRelation', label: 'Relationship', max: 100 },
    ],
  },
  experience: {
    title: 'Update my work experience',
    icon: '💼',
    noun: 'job',
    addLabel: 'Add a job',
    fields: [
      { key: 'company', label: 'Company', required: true, max: 200 },
      { key: 'title', label: 'Job title', required: true, max: 200 },
      { key: 'startDate', label: 'Start date', type: 'date', required: true },
      { key: 'endDate', label: 'End date', type: 'date', hint: 'Leave empty if you still work there' },
      { key: 'description', label: 'What you did', type: 'textarea', max: 4000 },
    ],
    name: (e) => e.title,
    detail: (e) => e.company,
    period: (e) => `${month(e.startDate)} – ${e.endDate ? month(e.endDate) : 'Present'}`,
  },
  education: {
    title: 'Update my education',
    icon: '🎓',
    noun: 'qualification',
    addLabel: 'Add a qualification',
    fields: [
      { key: 'institution', label: 'Institution', required: true, max: 200 },
      { key: 'qualification', label: 'Degree', required: true, max: 200 },
      { key: 'fieldOfStudy', label: 'Field of study', max: 200 },
      { key: 'startDate', label: 'Start date', type: 'date', required: true },
      { key: 'endDate', label: 'End date', type: 'date', hint: 'Leave empty if you are still studying' },
    ],
    name: (e) => [e.qualification, e.fieldOfStudy].filter(Boolean).join(', '),
    detail: (e) => e.institution,
    period: (e) => `${month(e.startDate)} – ${e.endDate ? month(e.endDate) : 'Present'}`,
  },
  certifications: {
    title: 'Update my certifications',
    icon: '📜',
    noun: 'certification',
    addLabel: 'Add a certification',
    fields: [
      { key: 'name', label: 'Certification', required: true, max: 200 },
      { key: 'issuer', label: 'Issuer', required: true, max: 200 },
      { key: 'issuedDate', label: 'Issued date', type: 'date', required: true },
      { key: 'expiryDate', label: 'Expiry date', type: 'date', hint: 'Leave empty if it does not expire' },
      { key: 'credentialId', label: 'Credential ID', max: 200 },
      { key: 'credentialUrl', label: 'Credential link (https)', type: 'url', max: 2000 },
    ],
    // An uploaded certificate file is kept as it is when the entry is edited.
    keep: ['fileUrl', 'fileName'],
    name: (e) => e.name,
    detail: (e) => e.issuer,
    period: (e) => [e.issuedDate && `Issued ${month(e.issuedDate)}`, e.expiryDate && `expires ${month(e.expiryDate)}`].filter(Boolean).join(', '),
  },
  languages: {
    title: 'Update my languages',
    icon: '🗣️',
    noun: 'language',
    addLabel: 'Add a language',
    fields: [
      { key: 'name', label: 'Language', required: true, max: 100 },
      { key: 'proficiency', label: 'Level', type: 'level', required: true, max: 100 },
    ],
    name: (e) => e.name,
    detail: (e) => levelOf(e.proficiency),
    period: () => '',
  },
};

const isDate = (v) => /^\d{4}-\d{2}-\d{2}$/.test(v) && !Number.isNaN(Date.parse(`${v}T00:00:00Z`));

/** The first thing wrong with the values, in plain words, or '' when they can be saved. Mirrors the Persona checks. */
export function problemWith(section, values) {
  const config = SECTIONS[section];
  for (const f of config.fields) {
    const value = String(values[f.key] ?? '').trim();
    if (f.required && !value) return `${f.label} is required`;
    if (f.max && value.length > f.max) return `${f.label} is too long (${f.max} characters at most)`;
    if (value && f.type === 'date' && !isDate(value)) return `${f.label} is not a valid date`;
    if (value && f.type === 'url' && !/^https:\/\/\S+$/i.test(value)) return `${f.label} must start with https://`;
  }
  const start = values.startDate || values.issuedDate;
  const end = values.endDate || values.expiryDate;
  if (start && end && end < start) return section === 'certifications' ? 'The expiry date is before the issued date' : 'The end date is before the start date';
  return '';
}

const clean = (value) => {
  const text = String(value ?? '').trim();
  return text === '' ? null : text;
};

function blank(config, prefill = {}) {
  return Object.fromEntries(config.fields.map((f) => [f.key, prefill[f.key] ?? '']));
}

function fromEntry(config, entry) {
  return Object.fromEntries(config.fields.map((f) => [f.key, entry[f.key] ?? '']));
}

/**
 * The form for one of the person's own profile tabs, in the chat: the entries already there (pick one to change) and a form
 * to add a new one. It saves through the same endpoints, and so the same checks, as the tab in Persona. Removing an entry is
 * the Delete form, not this one.
 */
export default function ProfileSectionCard({ form, disabled }) {
  const config = SECTIONS[form.section];
  const personal = form.section === 'personal';
  const [save, { isLoading }] = useSaveEmployeeMutation();
  const [loadMe] = useLazyEmployeeQuery();
  const [items, setItems] = useState(form.items || []);
  const [version, setVersion] = useState(form.version);
  const hasPrefill = Object.keys(form.prefill || {}).length > 0;
  // editing: null (nothing open), 'new', or the entry being changed. Personal details are always open; so is a prefilled form.
  const [editing, setEditing] = useState(personal || hasPrefill ? 'new' : null);
  const [values, setValues] = useState(() => (personal ? blank(config, form.values) : blank(config, form.prefill)));
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState('');
  const [busy, setBusy] = useState(false);

  const locked = isLoading || busy || disabled;
  const problem = editing ? problemWith(form.section, values) : '';
  const changed = (key, value) => setValues((prev) => ({ ...prev, [key]: value }));

  function open(entry) {
    setEditing(entry || 'new');
    setValues(entry ? fromEntry(config, entry) : blank(config));
    setError(null);
    setNotice('');
  }

  function close() {
    setEditing(null);
    setError(null);
  }

  async function savePersonal() {
    // The profile is saved as a whole, so read it fresh and change only these fields; nothing else on it is touched.
    const current = await loadMe('me', false).unwrap();
    const body = {
      preferredName: current.preferredName ?? null,
      phone: current.phone ?? null,
      locationId: current.locationId ?? null,
      profilePhotoUrl: current.profilePhotoUrl ?? null,
      aadharNumber: current.aadharNumber ?? null,
      permanentAddress: current.permanentAddress ?? null,
      emergencyContactName: current.emergencyContactName ?? null,
      emergencyContactPhone: current.emergencyContactPhone ?? null,
      emergencyContactRelation: current.emergencyContactRelation ?? null,
    };
    for (const f of config.fields) body[f.key] = clean(values[f.key]);
    const saved = await save({ path: '/me/profile', method: 'PUT', version: current.version ?? version, body }).unwrap();
    if (saved?.version != null) setVersion(saved.version);
    setNotice('Your personal details are saved.');
  }

  async function saveEntry() {
    const entry = editing === 'new' ? null : editing;
    const body = {};
    for (const f of config.fields) body[f.key] = clean(values[f.key]);
    for (const key of config.keep || []) body[key] = entry?.[key] ?? null;
    const saved = await save({
      path: entry ? `/me/${form.section}/${entry.id}` : `/me/${form.section}`,
      method: entry ? 'PUT' : 'POST',
      version,
      body,
    }).unwrap();
    if (saved?.version != null) setVersion(saved.version);
    const next = { ...(entry || {}), ...body, id: saved?.item?.id ?? entry?.id };
    setItems((prev) => (entry ? prev.map((x) => (x.id === entry.id ? next : x)) : [...prev, next]));
    setNotice(`${entry ? 'Updated' : 'Added'} ${config.name(next)}.`);
    setEditing(null);
  }

  async function submit(e) {
    e.preventDefault();
    if (!editing || problem || locked) return;
    setError(null);
    setBusy(true);
    try {
      if (personal) await savePersonal();
      else await saveEntry();
    } catch (err) {
      setError(messageOf(err));
    } finally {
      setBusy(false);
    }
  }

  const labelId = `profile-${form.section}-label`;

  return (
    <form className="worklog-card skills-card profile-section-card" onSubmit={submit} aria-label={config.title}>
      <div className="issue-create-card-header">
        <div className="issue-create-title-group">
          <span className="issue-create-title-icon">{config.icon}</span>
          <div className="worklog-title">{config.title}</div>
        </div>
      </div>

      {!personal && (
        <>
          <div className="worklog-label" id={labelId}>{`On your profile${items.length > 0 ? ` (${items.length})` : ''}`}</div>
          {items.length === 0 ? (
            <p className="skills-empty">{`You have no ${config.noun}s on your profile yet.`}</p>
          ) : (
            <ul className="skills-list" aria-labelledby={labelId}>
              {items.map((entry) => (
                <li key={entry.id} className={`skills-row ${editing?.id === entry.id ? 'selected' : ''}`}>
                  <button
                    type="button"
                    className="skills-row-main"
                    aria-pressed={editing?.id === entry.id}
                    aria-label={`Change ${config.name(entry)}`}
                    onClick={() => open(entry)}
                    disabled={locked}
                  >
                    <span className="skills-name">{config.name(entry)}</span>
                    {config.detail(entry) && <span className="skills-category">{config.detail(entry)}</span>}
                    {config.period(entry) && <span className="profile-entry-period">{config.period(entry)}</span>}
                  </button>
                </li>
              ))}
            </ul>
          )}
          {editing === null && (
            <div className="worklog-actions">
              <button type="button" className="skills-btn" onClick={() => open(null)} disabled={locked}>{`+ ${config.addLabel}`}</button>
            </div>
          )}
        </>
      )}

      {editing && (
        <fieldset className="skills-editor profile-editor">
          <legend>{personal ? 'Your details' : editing === 'new' ? config.addLabel : `Change ${config.name(editing)}`}</legend>
          {config.fields.map((f) => (
            <label key={f.key} className="worklog-field">
              <span className="worklog-label">{f.label}{f.required ? ' *' : ''}</span>
              {f.type === 'textarea' ? (
                <textarea
                  className="worklog-title-input profile-textarea"
                  rows={3}
                  value={values[f.key]}
                  maxLength={f.max}
                  onChange={(e) => changed(f.key, e.target.value)}
                  aria-label={f.label}
                />
              ) : f.type === 'level' ? (
                <span className="skills-levels" role="group" aria-label={f.label}>
                  {LANGUAGE_LEVELS.map((name, i) => (
                    <button
                      key={name}
                      type="button"
                      className={`skills-level-btn ${values[f.key] === name || values[f.key] === String(i + 1) ? 'selected' : ''}`}
                      aria-pressed={values[f.key] === name || values[f.key] === String(i + 1)}
                      onClick={() => changed(f.key, name)}
                    >
                      <strong>{i + 1}</strong>
                      {name}
                    </button>
                  ))}
                </span>
              ) : (
                <input
                  className="worklog-title-input"
                  type={f.type === 'date' ? 'date' : f.type === 'url' ? 'url' : 'text'}
                  value={values[f.key]}
                  maxLength={f.max}
                  onChange={(e) => changed(f.key, e.target.value)}
                  aria-label={f.label}
                />
              )}
              {f.hint && <small className="profile-field-hint">{f.hint}</small>}
            </label>
          ))}
        </fieldset>
      )}

      {notice && <div className="leave-days-note" role="status">{notice}</div>}
      {error && <div className="worklog-hint error" role="alert">{error}</div>}

      {editing && (
        <div className="issue-create-footer-bar">
          {problem && (
            <span className="issue-create-hint">
              <span className="hint-bullet-dot">•</span>
              {problem}
            </span>
          )}
          <div className="worklog-actions">
            {!personal && <button type="button" className="skills-btn" onClick={close} disabled={locked}>Cancel</button>}
            <button type="submit" className="worklog-submit" disabled={!!problem || locked}>
              {isLoading || busy ? 'Saving…' : personal ? 'Save details' : editing === 'new' ? `Add ${config.noun}` : 'Save changes'}
            </button>
          </div>
        </div>
      )}
    </form>
  );
}
