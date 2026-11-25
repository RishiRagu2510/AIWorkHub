import { useState, useRef } from 'react';
import { useEmployeeSectionQuery, useEmployeeCareerCatalogQuery, useSaveEmployeeMutation } from '../../services/api/employees';
import { QueryState, ErrorState, Field } from '../../components/common';
import { request } from '../../services/api/client';
const fields = {
  skills: [
    ['skillName', 'Skill', 'text'],
    ['category', 'Category', 'text', true],
    ['proficiency', 'Proficiency (1–5)', 'number'],
    ['yearsOfExperience', 'Years of experience', 'number'],
    ['lastUsedDate', 'Last used', 'date', true],
  ],
  experience: [
    ['company', 'Company', 'text'],
    ['title', 'Job title', 'text'],
    ['description', 'Description', 'text', true],
    ['startDate', 'Start date', 'date'],
    ['endDate', 'End date', 'date', true],
  ],
  education: [
    ['institution', 'Institution', 'text'],
    ['qualification', 'Degree', 'text'],
    ['fieldOfStudy', 'Field of study', 'text', true],
    ['startDate', 'Start date', 'date'],
    ['endDate', 'End date', 'date', true],
  ],
  certifications: [
    ['name', 'Certification', 'text'],
    ['issuer', 'Issuer', 'text'],
    ['credentialId', 'Credential ID', 'text', true],
    ['issuedDate', 'Issued date', 'date'],
    ['expiryDate', 'Expiry date', 'date', true],
    ['credentialUrl', 'Credential HTTPS URL', 'url', true],
    ['file', 'Upload Certificate', 'file', true],
  ],
  languages: [
    ['name', 'Language', 'text'],
    ['proficiency', 'Proficiency', 'text'],
  ],
  compensation: [
    ['ctc', 'Annual CTC', 'number'],
    ['bonus', 'Annual Bonus', 'number'],
    ['currency', 'Currency', 'select'],
    ['effectiveFrom', 'Effective from', 'date'],
    ['effectiveTo', 'Effective to', 'date', true],
  ],
};
const sectionIcons = {
  skills: (
    <svg className="career-section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#d97706" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="16 18 22 12 16 6" />
      <polyline points="8 6 2 12 8 18" />
    </svg>
  ),
  experience: (
    <svg className="career-section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#2563eb" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="2" y="7" width="20" height="14" rx="2" ry="2" />
      <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
    </svg>
  ),
  education: (
    <svg className="career-section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#7c3aed" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
      <path d="M6 12v5c3 3 9 3 12 0v-5" />
    </svg>
  ),
  certifications: (
    <svg className="career-section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#059669" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="8" r="7" />
      <polyline points="8.21 13.89 7 23 12 20 17 23 15.79 13.88" />
    </svg>
  ),
  languages: (
    <svg className="career-section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#0284c7" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <circle cx="12" cy="12" r="10" />
      <line x1="2" y1="12" x2="22" y2="12" />
      <path d="M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10z" />
    </svg>
  ),
  compensation: (
    <svg className="career-section-icon" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="#b45309" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
      <path d="M7 11V7a5 5 0 0 1 10 0v4" />
    </svg>
  ),
};

function formatCareerValue(section, key, value, item) {
  if (section === 'certifications' && key === 'file') {
    if (!item?.fileUrl) return <span className="summary-empty">—</span>;
    return (
      <a
        className="career-link"
        href={item.fileUrl}
        target="_blank"
        rel="noreferrer"
        download={item.fileName || 'certificate'}
      >
        <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <polyline points="14 2 14 8 20 8" />
          <line x1="12" y1="18" x2="12" y2="12" />
          <polyline points="9 15 12 18 15 15" />
        </svg>
        {item.fileName || 'Download'}
      </a>
    );
  }
  if (value === null || value === undefined || value === '') return <span className="summary-empty">—</span>;
  if (section === 'skills') {
    if (key === 'skillName') {
      return (
        <div className="career-skill-name">
          <span className="career-skill-dot" />
          <span className="career-skill-title">{value}</span>
        </div>
      );
    }
    if (key === 'category') {
      return <span className="summary-pill summary-pill-neutral">{value}</span>;
    }
    if (key === 'proficiency') {
      const profName = ['', 'Beginner', 'Basic', 'Intermediate', 'Advanced', 'Expert'][value] || value;
      return <span className={`profile-skill-badge level-${value}`}>{profName}</span>;
    }
    if (key === 'yearsOfExperience') {
      return <span className="career-metric-value">{value}</span>;
    }
    if (key === 'lastUsedDate') {
      return <span className="career-date-value">{value}</span>;
    }
  }
  if (section === 'languages') {
    if (key === 'name') {
      return (
        <div className="career-skill-name">
          <span className="career-skill-dot language-dot" />
          <span className="career-skill-title">{value}</span>
        </div>
      );
    }
    if (key === 'proficiency') {
      const num = Number(value);
      const isNum = !isNaN(num) && num >= 1 && num <= 5;
      const profName = isNum
        ? ['', 'Elementary', 'Limited Working', 'Professional Working', 'Full Professional', 'Native / Bilingual'][num]
        : value;

      const valStr = String(profName || '').toLowerCase();
      let levelClass = 'level-3';
      if (valStr.includes('native') || valStr.includes('bilingual') || num === 5) levelClass = 'level-5';
      else if (valStr.includes('full') || valStr.includes('fluent') || num === 4) levelClass = 'level-4';
      else if (valStr.includes('professional') || valStr.includes('working') || num === 3) levelClass = 'level-3';
      else if (valStr.includes('limited') || valStr.includes('basic') || num === 2) levelClass = 'level-2';
      else if (valStr.includes('elementary') || valStr.includes('beginner') || num === 1) levelClass = 'level-1';

      return (
        <span className={`profile-skill-badge ${levelClass}`}>
          {profName}
        </span>
      );
    }
  }
  if (section === 'compensation') {
    if (key === 'ctc' || key === 'bonus') {
      const num = Number(value);
      if (isNaN(num)) return value;
      return (
        <span className="career-metric-value" style={{ fontWeight: 600 }}>
          {item?.currency ? `${item.currency} ` : ''}
          {num.toLocaleString(undefined, { maximumFractionDigits: 2 })}
        </span>
      );
    }
    if (key === 'currency') {
      return <span className="summary-pill summary-pill-neutral" style={{ textTransform: 'uppercase', fontWeight: 600 }}>{value}</span>;
    }
  }
  if (key === 'credentialUrl') {
    return (
      <a className="career-link" href={value} target="_blank" rel="noreferrer">
        View credential
        <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M18 13v6a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h6" />
          <polyline points="15 3 21 3 21 9" />
          <line x1="10" y1="14" x2="21" y2="3" />
        </svg>
      </a>
    );
  }
  return value;
}

export default function EmployeeCareer({ employee, section, editable }) {
  const catalog = useEmployeeCareerCatalogQuery(undefined, { skip: section !== 'skills' });
  const query = useEmployeeSectionQuery({ id: employee.id, section });
  const [save, result] = useSaveEmployeeMutation();
  const [editing, setEditing] = useState(null);
  const [draftVersion, setDraftVersion] = useState(employee.version);
  const [message, setMessage] = useState('');
  const [selectedFile, setSelectedFile] = useState(null);
  const [uploading, setUploading] = useState(false);
  const [uploadError, setUploadError] = useState(null);
  const fileInputRef = useRef(null);
  const list = fields[section];
  async function remove(item) {
    if (!window.confirm('Delete this profile entry?')) return;
    try {
      await save({
        path: `/${employee.id}/${section}/${item.id}`,
        method: 'DELETE',
        version: employee.version,
      }).unwrap();
      setMessage('Entry deleted.');
    } catch {
      /* shown below */
    }
  }
  return (
    <section className="employee-panel career-section-panel">
      <div className="persona-record-card career-table-card">
        <header className="career-card-header">
          <div className="persona-card-title">
            <span className="career-icon-box">{sectionIcons[section] || sectionIcons.skills}</span>
            <h2>{section[0].toUpperCase() + section.slice(1)}</h2>
          </div>
          {query.data?.length > 0 && (
            <span className="persona-badge tech-badge">
              {query.data.length} {query.data.length === 1 ? 'entry' : 'entries'}
            </span>
          )}
        </header>

        <QueryState query={query}>
          {query.data?.length > 0 ? (
            <div className="career-table-wrapper">
              <div className="career-table-header-row">
                <div className="career-header-fields">
                  {list.map(([key, title]) => (
                    <div key={key} className={`career-th career-col-${key}`}>
                      {title}
                    </div>
                  ))}
                </div>
                {editable && <div className="career-th career-col-actions">Actions</div>}
              </div>

              <div className="career-table-body">
                {query.data.map((item) => (
                  <article className="employee-entry career-row" key={item.id}>
                    <dl className="career-row-fields">
                      {list.map(([key, title]) => (
                        <div key={key} className={`career-cell career-col-${key}`}>
                          <dt className="career-mobile-label">{title}</dt>
                          <dd>{formatCareerValue(section, key, item[key], item)}</dd>
                        </div>
                      ))}
                    </dl>
                    {editable && (
                      <div className="actions career-row-actions">
                        <button
                          type="button"
                          className="career-action-btn edit-btn"
                          onClick={() => {
                            setEditing(item);
                            setDraftVersion(employee.version);
                            setSelectedFile(null);
                            setUploadError(null);
                          }}
                        >
                          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <path d="M17 3a2.828 2.828 0 1 1 4 4L7.5 20.5 2 22l1.5-5.5L17 3z" />
                          </svg>
                          Edit
                        </button>
                        <button
                          type="button"
                          className="career-action-btn delete-btn"
                          onClick={() => remove(item)}
                          disabled={result.isLoading}
                        >
                          <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                            <polyline points="3 6 5 6 21 6" />
                            <path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" />
                          </svg>
                          Delete
                        </button>
                      </div>
                    )}
                  </article>
                ))}
              </div>
            </div>
          ) : (
            <div className="employee-career-empty-state">
              <p className="employee-career-empty">No {section} added yet.</p>
            </div>
          )}
        </QueryState>

        {editable && (
          <form
            key={editing?.id || 'new'}
            className="career-inline-form"
            onSubmit={async (e) => {
              e.preventDefault();
              setUploadError(null);
              const form = e.currentTarget;
              const body = Object.fromEntries(new FormData(form));
              for (const [key, , type, optional] of list) {
                if (type === 'number') body[key] = Number(body[key]);
                else if (optional && !body[key]) body[key] = null;
              }
              if (section === 'skills') {
                body.skillId = Number(body.skillId);
                delete body.skillName;
                delete body.category;
              }
              if (section === 'certifications') {
                delete body.file;
                body.fileUrl = editing?.fileUrl || null;
                body.fileName = editing?.fileName || null;
                if (selectedFile) {
                  setUploading(true);
                  try {
                    const uploadData = new FormData();
                    uploadData.append('file', selectedFile);
                    const uploadRes = await request(`/api/employees/${employee.id}/certifications/upload`, {
                      method: 'POST',
                      body: uploadData,
                      version: draftVersion,
                    });
                    body.fileUrl = uploadRes.fileUrl;
                    body.fileName = uploadRes.fileName;
                  } catch (err) {
                    setUploadError(err.message || 'File upload failed.');
                    setUploading(false);
                    return;
                  }
                  setUploading(false);
                }
              }
              try {
                const saved = await save({
                  path: `/${employee.id}/${section}${editing ? `/${editing.id}` : ''}`,
                  method: editing ? 'PUT' : 'POST',
                  version: draftVersion,
                  body,
                }).unwrap();
                setDraftVersion(saved.version);
                setEditing(null);
                setSelectedFile(null);
                if (fileInputRef.current) fileInputRef.current.value = '';
                form.reset();
                setMessage('Entry saved.');
              } catch {
                /* shown below */
              }
            }}
          >
            {editing && (
              <div className="career-editing-banner">
                <span className="summary-pill summary-pill-primary">Editing record</span>
              </div>
            )}
            <div className="career-form-row">
              <div className="career-form-inputs-group">
                {section === 'skills' && <>
                  <Field label="Skill"><select name="skillId" required defaultValue={editing?.skillId || ''}>
                    <option value="">Select a skill</option>
                    {(catalog.data?.skills || []).map(skill => <option key={skill.id} value={skill.id}>{skill.name}{skill.category ? ` · ${skill.category}` : ''}</option>)}
                  </select></Field>
                  <ErrorState error={catalog.error} />
                  {!catalog.isLoading && !catalog.error && !catalog.data?.skills?.length && <p>Ask an administrator to add skills in Employee setup.</p>}
                </>}
                {list.filter(([key]) => section !== 'skills' || !['skillName', 'category'].includes(key)).map(([key, title, type, optional]) => {
                  const placeholder = `${title}${optional ? ' (optional)' : ''}`;
                  if (type === 'file') {
                    return (
                      <Field key={key} label={title}>
                        <div className="career-file-input-wrapper">
                          <input
                            ref={fileInputRef}
                            type="file"
                            id={`cert-file-${editing?.id || 'new'}`}
                            accept=".pdf,image/png,image/jpeg,image/webp"
                            style={{ display: 'none' }}
                            onChange={(e) => {
                              const f = e.target.files?.[0];
                              if (f && f.size > 10 * 1024 * 1024) {
                                setUploadError('File size must not exceed 10 MB.');
                                e.target.value = '';
                                setSelectedFile(null);
                                return;
                              }
                              setUploadError(null);
                              setSelectedFile(f || null);
                            }}
                          />
                          <label
                            htmlFor={`cert-file-${editing?.id || 'new'}`}
                            className="career-file-button"
                            title={selectedFile ? selectedFile.name : editing?.fileName ? `Current: ${editing.fileName}` : 'Choose certificate file'}
                          >
                            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                              <polyline points="17 8 12 3 7 8" />
                              <line x1="12" y1="3" x2="12" y2="15" />
                            </svg>
                            <span className="career-file-name-text">
                              {selectedFile ? selectedFile.name : editing?.fileName ? `Change: ${editing.fileName}` : 'Upload file (PDF/Image)'}
                            </span>
                          </label>
                        </div>
                      </Field>
                    );
                  }
                  if (type === 'date') {
                    const initialVal = editing?.[key] ? String(editing[key]).slice(0, 10) : '';
                    return (
                      <Field key={key} label={title}>
                        <input
                          type={initialVal ? 'date' : 'text'}
                          name={key}
                          required={!optional}
                          defaultValue={initialVal}
                          placeholder={placeholder}
                          title={title}
                          onFocus={(e) => {
                            e.target.type = 'date';
                            if (e.target.showPicker) {
                              try { e.target.showPicker(); } catch {}
                            }
                          }}
                          onBlur={(e) => {
                            if (!e.target.value) e.target.type = 'text';
                          }}
                        />
                      </Field>
                    );
                  }
                  if (type === 'select' && key === 'currency') {
                    const defaultCur = editing?.currency || 'USD';
                    return (
                      <Field key={key} label={title}>
                        <select
                          name={key}
                          required={!optional}
                          defaultValue={defaultCur}
                          title={title}
                        >
                          {['USD', 'EUR', 'GBP', 'INR', 'CAD', 'AUD', 'SGD', 'AED'].map((cur) => (
                            <option key={cur} value={cur}>
                              {cur}
                            </option>
                          ))}
                        </select>
                      </Field>
                    );
                  }
                  return (
                    <Field
                      key={key}
                      label={title}
                      name={key}
                      type={type}
                      required={!optional}
                      min={type === 'number' ? (key === 'proficiency' ? 1 : 0) : undefined}
                      max={section === 'skills' && key === 'proficiency' ? 5 : undefined}
                      step={type === 'number' ? (key === 'proficiency' ? 1 : 'any') : undefined}
                      defaultValue={editing?.[key] ?? ''}
                      maxLength={4000}
                      placeholder={section === 'compensation' && type === 'number' ? `${title} (e.g. 50000)` : `Enter ${title.toLowerCase()}…`}
                      title={title}
                    />
                  );
                })}
              </div>
              <div className="actions career-form-actions">
                <button type="submit" className="career-submit-btn" disabled={result.isLoading || uploading}>
                  <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M12 5v14M5 12h14" />
                  </svg>
                  {uploading ? 'Uploading…' : editing ? 'Save changes' : 'Add entry'}
                </button>
                {editing && (
                  <button
                    type="button"
                    className="career-cancel-btn"
                    onClick={() => {
                      setEditing(null);
                      setDraftVersion(employee.version);
                      setSelectedFile(null);
                      setUploadError(null);
                    }}
                  >
                    Cancel
                  </button>
                )}
              </div>
            </div>
          </form>
        )}
      </div>
      {uploadError && <p role="alert" style={{ color: '#dc2626', fontSize: '13px', margin: '8px 0 0' }}>{uploadError}</p>}
      <ErrorState error={result.error} />
      {result.error?.status === 409 && (
        <p role="alert">
          This profile changed. Copy your draft, refresh the page, and reapply your changes before
          saving.
        </p>
      )}
      {message && <p role="status">{message}</p>}
    </section>
  );
}
