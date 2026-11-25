import AccountField from './AccountField';
import { useUnsavedChanges } from '../../components/unsavedContext';
import { useEffect, useRef, useState } from 'react';
import { Field, CustomSelect, ErrorState } from '../../components/common';
import { useNotice } from '../../components/noticeContext';
import { useCreateProjectMutation } from '../../services/api/api';

const failureMessages = {
  400: 'Please check the project details and try again.',
  422: 'Please check the project details and try again.',
  409: 'This project name is already in use. Choose a different project name.',
  404: 'The account or parent project is no longer available. Check your selection and try again.',
  403: 'You do not have permission to create projects.',
  401: 'Your session has expired. Please sign in again.',
};

export default function ProjectCreateDialog({ projects, parent, close }) {
  const { markDirty, clearDirty, confirmLeave } = useUnsavedChanges();
  const dialog = useRef(null);
  const submitting = useRef(false);
  const [create, { isLoading }] = useCreateProjectMutation();
  const [error, setError] = useState(null);
  const [keyError, setKeyError] = useState('');
  const [projectType, setProjectType] = useState('SOFTWARE');
  const notice = useNotice();
  useEffect(() => {
    const element = dialog.current;
    const previous = document.activeElement;
    element.showModal();
    element.querySelector('[name="name"]')?.focus();
    return () => {
      element.close();
      previous?.focus();
    };
  }, []);
  async function submit(event) {
    event.preventDefault();
    if (submitting.current) return;
    setKeyError('');
    setError(null);
    const form = event.currentTarget;
    const values = Object.fromEntries(new FormData(form));
    const name = values.name.trim();
    const key = values.key.trim();
    const active = projects.filter((p) => p.status === 'ACTIVE');
    let invalid;
    if (!name || name.length > 200)
      invalid = ['name', 'Enter an account name of 1–200 characters.'];
    else if (!/^[A-Za-z][A-Za-z0-9_ -]{0,999}$/.test(key))
      invalid = [
        'key',
        'Use up to 1000 characters, starting with a letter. Spaces, numbers, underscores, and hyphens are allowed.',
      ];
    else if (!['SOFTWARE', 'SERVICE', 'BUSINESS', 'BANKING'].includes(values.projectType))
      invalid = ['projectType', 'Select a valid project type.'];
    else if (values.parentProjectId && !active.some((p) => String(p.id) === values.parentProjectId))
      invalid = ['parentProjectId', failureMessages[404]];
    if (invalid) {
      if (invalid[0] === 'key') setKeyError(invalid[1]);
      else setError({ message: invalid[1] });
      form.elements.namedItem(invalid[0])?.focus();
      return;
    }
    submitting.current = true;
    setError(null);
    try {
      await create({
        accountName: name,
        projectName: key,
        ...(values.displayName?.trim() ? { displayName: values.displayName.trim() } : {}),
        ...(values.accountId ? { accountId: Number(values.accountId) } : {}),
        description: values.description.trim(),
        projectType: values.projectType,
        parentProjectId: values.parentProjectId ? Number(values.parentProjectId) : null,
      }).unwrap();
      notice(`Project “${key}” created in ${name}.`);
      clearDirty();
      close();
    } catch (failure) {
      setError({
        message:
          failureMessages[failure.status] ||
          failure.message ||
          'Could not create the project. Please try again.',
      });
    } finally {
      submitting.current = false;
    }
  }
  return (
    <dialog
      className="project-create-dialog"
      ref={dialog}
      aria-labelledby="project-create-title"
      onCancel={(event) => {
        event.preventDefault();
        if (!submitting.current) confirmLeave(close);
      }}
    >
      <form onSubmit={submit} onChange={markDirty} noValidate>
        <div className="section-header project-create-header">
          <div className="header-icon-badge" aria-hidden="true">
            <svg
              width="22"
              height="22"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z" />
              <line x1="12" y1="11" x2="12" y2="17" />
              <line x1="9" y1="14" x2="15" y2="14" />
            </svg>
          </div>
          <div className="header-text">
            <div className="header-eyebrow">GRIDHUB · WORKSPACE</div>
            <h2 id="project-create-title">{parent ? 'Add subproject' : 'New project'}</h2>
            <p>Create a project under a new or existing account.</p>
          </div>
          <button
            type="button"
            className="dialog-close-btn"
            aria-label="Close dialog"
            onClick={() => confirmLeave(close)}
          >
            <svg
              width="18"
              height="18"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <line x1="18" y1="6" x2="6" y2="18" />
              <line x1="6" y1="6" x2="18" y2="18" />
            </svg>
          </button>
        </div>
        <fieldset disabled={isLoading}>
          <div className="form-body">
            <AccountField
              projects={projects}
              initialName={parent?.accountName || parent?.name || ''}
            />
            <div className="form-grid">
              <div className="project-key-field">
                <Field
                  label="Project *"
                  name="key"
                  required
                  maxLength={1000}
                  placeholder="e.g. IT-OPS or ITHO"
                  aria-invalid={Boolean(keyError)}
                  aria-describedby={keyError ? 'project-key-error' : 'project-key-hint'}
                  onChange={() => setKeyError('')}
                />
                {keyError ? (
                  <p className="project-field-error" id="project-key-error" role="alert">
                    {keyError}
                  </p>
                ) : (
                  <p className="hint field-subhint" id="project-key-hint">
                    Unique key for issue identifiers (e.g. <code>IT-101</code>).
                  </p>
                )}
              </div>
              <div className="project-type-wrapper">
                <CustomSelect
                  label="Project type"
                  name="projectType"
                  defaultValue="SOFTWARE"
                  value={projectType}
                  allowEmpty={false}
                  options={[
                    { value: 'SOFTWARE', label: 'Software' },
                    { value: 'SERVICE', label: 'Service' },
                    { value: 'BUSINESS', label: 'Business' },
                    { value: 'BANKING', label: 'Banking' },
                  ]}
                  onChange={(e) => setProjectType(e.target.value)}
                />
                <div className="project-type-tag-preview" aria-live="polite">
                  {projectType === 'SOFTWARE' && '💻 Sprints, backlogs, repos & versions'}
                  {projectType === 'SERVICE' && '🎧 Helpdesk, IT operations & SLAs'}
                  {projectType === 'BUSINESS' && '📊 HR, legal, finance & operations'}
                  {projectType === 'BANKING' && '🏦 High-security governance & audit'}
                </div>
              </div>
            </div>
            <div className="project-display-name-field">
              <Field
                label="Project name"
                name="displayName"
                maxLength={1000}
                placeholder="e.g. IT Helpdesk & Operations"
              />
              <p className="hint field-subhint">
                Friendly display name for dashboards and boards (optional).
              </p>
            </div>
            <Field label="Description">
              <textarea
                name="description"
                rows={3}
                placeholder="What is this project about? (e.g. IT support, hardware requests, onboarding access)"
              />
            </Field>
            <div className="parent-project-section">
              <CustomSelect
                label="Parent project"
                name="parentProjectId"
                defaultValue={parent?.id ? String(parent.id) : ''}
                placeholder="None — top-level project"
                allowEmpty={true}
                options={projects
                  .filter((p) => p.status === 'ACTIVE')
                  .map((p) => ({ value: String(p.id), label: `${p.key} · ${p.name}` }))}
              />
              <div className="project-parent-callout">
                <svg
                  width="16"
                  height="16"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  aria-hidden="true"
                >
                  <circle cx="12" cy="12" r="10" />
                  <line x1="12" y1="16" x2="12" y2="12" />
                  <line x1="12" y1="8" x2="12.01" y2="8" />
                </svg>
                <p className="hint">
                  Parent-project members inherit access to subprojects. Developers still see only issues
                  they report or are assigned.
                </p>
              </div>
            </div>
            <ErrorState error={error} />
          </div>
          <div className="form-footer">
            <button
              type="button"
              className="btn-cancel"
              onClick={() => confirmLeave(close)}
            >
              Cancel
            </button>
            <button type="submit" className="primary btn-create">
              {isLoading ? (
                <>
                  <span className="spinner" aria-hidden="true" />
                  Creating…
                </>
              ) : (
                <>
                  <svg
                    width="16"
                    height="16"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth="2.5"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    aria-hidden="true"
                  >
                    <line x1="12" y1="5" x2="12" y2="19" />
                    <line x1="5" y1="12" x2="19" y2="12" />
                  </svg>
                  Create project
                </>
              )}
            </button>
          </div>
        </fieldset>
      </form>
    </dialog>
  );
}
