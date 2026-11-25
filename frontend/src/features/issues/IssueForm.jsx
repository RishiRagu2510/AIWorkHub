import OnboardingFields, { onboardingPayload } from '../employees/OnboardingFields';
import ParentIssuePicker from './ParentIssuePicker';
import { useWorkflowQuery } from '../../services/api/collaboration';
import { assignmentError } from './assignment';
import UserIdentity from '../../components/UserIdentity';
import { useUnsavedChanges } from '../../components/unsavedContext';
import { useEffect, useRef, useState } from 'react';
import Link from '../../components/PortalLink';
import { Field, CustomSelect, ErrorState } from '../../components/common';
import { useProjectsQuery } from '../../services/api/api';
import { useAuth } from '../auth/context';
import { FeatureGuard } from '../auth/FeatureGuard';
import { ATHENNA } from '../auth/features';
import { issueTypes, priorities, issuePayload } from './contracts';
import { formValues, label } from '../../utils/format';
import IssueAssignee from './IssueAssignee';
import { useRunAthennaCopilotMutation } from '../../services/api/agent';
export function IssueForm({
  issue,
  save,
  busy,
  error,
  saveBlocked = false,
  defaults = {},
  onDraft,
  restored = false,
}) {
  const { markDirty, clearDirty } = useUnsavedChanges();
  const { can, session, canAccessPortal } = useAuth();
  // Onboarding requests belong to Persona: a company without it is not offered them.
  const canRequestEmployee =
    (canAccessPortal ? canAccessPortal('employee') : true) &&
    can('ISSUE_CREATE') &&
    [session?.user?.role, ...(session?.user?.roles || [])].includes('TALENT_ACQUISITION');
  const canAssign = can('ISSUE_ASSIGN');
  const initial = { ...defaults, ...issue };
  const restoredMarked = useRef(false);
  useEffect(() => {
    if (restored && !restoredMarked.current) {
      markDirty();
      restoredMarked.current = true;
    }
  }, [restored, markDirty]);
  const [projectId, setProjectId] = useState(initial?.projectId ? String(initial.projectId) : '');
  const [requestKind, setRequestKind] = useState(initial.requestKind || 'ISSUE');
  const [selectedType, setSelectedType] = useState(initial?.issueType || 'TASK');
  const workflow = useWorkflowQuery(projectId, { skip: Boolean(issue) || !projectId });
  const [clears, setClears] = useState(
    Object.fromEntries(
      ['clearParentIssue', 'clearComponent', 'clearStoryPoints', 'clearDueDate'].map((flag) => [
        flag,
        initial[flag] === 'on',
      ]),
    ),
  );
  const [assignee, setAssignee] = useState(null);
  const projects = useProjectsQuery(undefined, { skip: Boolean(issue) || !can('PROJECT_VIEW') });
  const [validation, setValidation] = useState(null);
  const [fieldErrors, setFieldErrors] = useState({});
  const [runCopilot, { isLoading: isCopilotLoading }] = useRunAthennaCopilotMutation();
  const descRef = useRef(null);
  const formRef = useRef(null);

  async function handleGenerateCriteria() {
    const summaryInput = formRef.current?.elements?.summary?.value;
    const currentDesc = descRef.current?.value || '';
    const promptInput = summaryInput ? `Summary: ${summaryInput}\nDescription: ${currentDesc}` : currentDesc;
    if (!promptInput.trim()) {
      alert('Please enter a summary or brief description first.');
      return;
    }
    try {
      const res = await runCopilot({
        feature: 'GenerateIssueAcceptanceCriteria',
        inputText: promptInput,
      }).unwrap();
      if (res?.outputText && descRef.current) {
        descRef.current.value = (currentDesc ? currentDesc + '\n\n' : '') + res.outputText;
        markDirty();
      }
    } catch (err) {
      console.error('Failed to run Athenna copilot:', err);
    }
  }

  function submit(e) {
    e.preventDefault();
    if (busy || saveBlocked) return;
    setValidation(null);
    setFieldErrors({});

    if (!issue && requestKind === 'ADD_EMPLOYEE' && !canRequestEmployee) {
      setValidation({
        message:
          'Only Talent Acquisition can submit Add New Employee requests. Switch to Standard issue or ask your administrator to assign the role.',
      });
      return;
    }
    const values = formValues(e.currentTarget);
    const errors = {};
    if (!issue && !projectId) {
      errors.projectId = 'Please select a project.';
    }
    if (requestKind !== 'ADD_EMPLOYEE' && (!values.summary || !values.summary.trim())) {
      errors.summary = 'Summary is required.';
    }
    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      setValidation({ message: 'Please fill out all required fields marked with * before creating your issue.' });
      return;
    }
    const invalid = Array.from(e.currentTarget.elements).find(
      (field) => field.willValidate && !field.validity.valid,
    );
    if (invalid) {
      const title = invalid.getAttribute('aria-label') ||
        invalid.labels?.[0]?.querySelector('span')?.textContent || invalid.name;
      setValidation({ message: `${title}: ${invalid.validationMessage}` });
      const target = invalid.closest('.custom-select-container')?.querySelector('button') || invalid;
      target.scrollIntoView?.({ behavior: 'smooth', block: 'center' });
      target.focus();
      return;
    }
    if (!issue && (!workflow.currentData || workflow.isFetching)) {
      setValidation({ message: 'Load the project workflow before creating an issue.' });
      return;
    }
    const issueValues = !issue && requestKind === 'ADD_EMPLOYEE'
      ? { ...values, summary: 'Add New Employee', description: '', priority: 'MEDIUM' }
      : values;
    const body = issuePayload(issueValues, Boolean(issue));
    if (!issue) body.statusCode = workflow.currentData.definition.initialStatus;
    if (canAssign) {
      const list = Array.isArray(assignee) ? assignee : assignee ? [assignee] : [];
      if (list.length > 0) {
        body.assigneeUserId = list[0].id;
        if (list.length > 1) {
          body.assigneeUserIds = list.map((a) => a.id);
        }
      }
    }
    if (!issue && requestKind === 'ADD_EMPLOYEE') body.employeeRequest = onboardingPayload(values);
    save(body, clearDirty);
  }

  return (
    <form
      ref={formRef}
      className="panel issue-form"
      noValidate
      onSubmit={submit}
      onChange={(event) => {
        markDirty();
        onDraft?.(formValues(event.currentTarget));
      }}
    >
      <ErrorState error={workflow.error} retry={workflow.refetch} />
      <fieldset disabled={busy}>
        <div className="section-header">
          <div className="header-text">
            <h2>{issue ? 'Issue information' : 'What needs to be done?'}</h2>
            <p className="subtitle">
              {issue
                ? 'Review and update issue details.'
                : 'Create a new task, bug, or employee onboarding request.'}
            </p>
          </div>
          <span className="required-badge">* Required fields</span>
        </div>
        <div className="form-body">
          {!issue && (
            <div className="form-grid-3">
              <div className={`field-wrapper${fieldErrors.projectId ? ' has-error' : ''}`}>
                <CustomSelect
                  label="Project *"
                  name="projectId"
                  value={projectId}
                  placeholder={projects.isLoading ? 'Loading projects…' : 'Select a project'}
                  allowEmpty={true}
                  onChange={(event) => {
                    setProjectId(event.target.value);
                    setAssignee(null);
                    if (fieldErrors.projectId) setFieldErrors((prev) => ({ ...prev, projectId: null }));
                    if (validation) setValidation(null);
                  }}
                  options={(projects.data || []).map((p) => ({
                    value: String(p.id),
                    label: `${p.key} · ${p.name}`,
                  }))}
                />
                {fieldErrors.projectId && (
                  <span className="field-error-text" role="alert">{fieldErrors.projectId}</span>
                )}
              </div>
              <CustomSelect
                label="Issue type *"
                name="issueType"
                value={selectedType}
                allowEmpty={false}
                onChange={(e) => setSelectedType(e.target.value)}
                options={issueTypes.map((t) => ({ value: t, label: label(t) }))}
              />
              <CustomSelect
                label="Request category"
                name="requestKind"
                value={requestKind}
                allowEmpty={false}
                onChange={(e) => setRequestKind(e.target.value)}
                options={[
                  { value: 'ISSUE', label: 'Standard issue' },
                  ...(canRequestEmployee ? [{ value: 'ADD_EMPLOYEE', label: 'Add New Employee' }] : []),
                ]}
              />
            </div>
          )}
          {!issue && projects.error && (
            <ErrorState error={projects.error} retry={projects.refetch} />
          )}
          {!issue && !can('PROJECT_VIEW') && (
            <p className="hint">
              Project access is required to select a project. Contact your administrator.
            </p>
          )}
          {!issue && canRequestEmployee && requestKind === 'ADD_EMPLOYEE' && (
            <OnboardingFields initial={initial} />
          )}
          {!issue && requestKind === 'ADD_EMPLOYEE' && !canRequestEmployee && (
            <p role="alert">This employee request draft requires the Talent Acquisition role.</p>
          )}
          {requestKind !== 'ADD_EMPLOYEE' && (
            <>
              <div className={`field-wrapper${fieldErrors.summary ? ' has-error' : ''}`}>
                <Field
                  label="Summary *"
                  name="summary"
                  maxLength="500"
                  defaultValue={initial?.summary}
                  onChange={() => {
                    if (fieldErrors.summary) setFieldErrors((prev) => ({ ...prev, summary: null }));
                    if (validation) setValidation(null);
                  }}
                  placeholder="A short, clear description of the work"
                />
                {fieldErrors.summary && (
                  <span className="field-error-text" role="alert">{fieldErrors.summary}</span>
                )}
              </div>
              <input type="hidden" name="labels" value={Array.isArray(initial.labels) ? initial.labels.join(',') : initial.labels || ''} />
              <input type="hidden" name="originalEstimateMinutes" value={initial.originalEstimateMinutes ?? 0} />
              <input type="hidden" name="remainingEstimateMinutes" value={initial.remainingEstimateMinutes ?? 0} />
              <input type="hidden" name="fixVersionIds" value={Array.isArray(initial.fixVersionIds) ? initial.fixVersionIds.join(',') : initial.fixVersionIds || ''} />
              <input type="hidden" name="affectsVersionIds" value={Array.isArray(initial.affectsVersionIds) ? initial.affectsVersionIds.join(',') : initial.affectsVersionIds || ''} />
              {issue && (
                <p>
                  Current assignee: <UserIdentity id={issue.assigneeUserId} />. Leave the selection
                  blank to keep this assignment.
                </p>
              )}
              {canAssign && (
                <IssueAssignee
                  key={projectId}
                  projectIds={projectId ? [projectId] : []}
                  value={assignee}
                  onChange={(person) => {
                    setAssignee(person);
                    markDirty();
                  }}
                  disabled={busy}
                />
              )}
              <div className="field-with-copilot">
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                  <label htmlFor="issue-description" style={{ fontSize: '0.85rem', fontWeight: 600, color: 'var(--text-primary, #0f172a)' }}>
                    Description
                  </label>
                  <FeatureGuard feature={ATHENNA}>
                    <button
                    type="button"
                    onClick={handleGenerateCriteria}
                    disabled={isCopilotLoading || busy}
                    style={{
                      background: 'linear-gradient(135deg, rgba(79, 70, 229, 0.08) 0%, rgba(124, 58, 237, 0.08) 100%)',
                      border: '1px solid rgba(124, 58, 237, 0.25)',
                      borderRadius: '16px',
                      padding: '3px 10px',
                      fontSize: '0.78rem',
                      fontWeight: 600,
                      color: 'var(--primary, #4f46e5)',
                      cursor: isCopilotLoading ? 'wait' : 'pointer',
                      display: 'inline-flex',
                      alignItems: 'center',
                      gap: '4px',
                    }}
                    title="Generate BDD acceptance criteria & subtasks with Athenna"
                  >
                    <span>✦</span>
                    <span>{isCopilotLoading ? 'Generating...' : 'Athenna Copilot'}</span>
                    </button>
                  </FeatureGuard>
                </div>
                <textarea
                  id="issue-description"
                  ref={descRef}
                  name="description"
                  rows="6"
                  defaultValue={initial?.description || ''}
                  placeholder="Add context, requirements, and acceptance criteria…"
                  style={{ width: '100%', boxSizing: 'border-box' }}
                />
              </div>
              <div className="form-grid">
                <CustomSelect
                  label="Priority"
                  name="priority"
                  defaultValue={initial?.priority || 'MEDIUM'}
                  allowEmpty={false}
                  options={priorities.map((p) => ({ value: p, label: label(p) }))}
                />
                <Field
                  label="Due date"
                  name="dueDate"
                  disabled={Boolean(issue && clears.clearDueDate)}
                  type="date"
                  defaultValue={initial?.dueDate || ''}
                />
                <Field
                  label="Story points"
                  name="storyPoints"
                  disabled={Boolean(issue && clears.clearStoryPoints)}
                  type="number"
                  min="0"
                  step="1"
                  defaultValue={initial?.storyPoints ?? ''}
                />
                <ParentIssuePicker
                  key={`${projectId}-${selectedType}`}
                  projectId={projectId}
                  issueType={selectedType}
                  issueId={issue?.id}
                  initialId={initial?.parentIssueId}
                  disabled={Boolean(issue && clears.clearParentIssue)}
                />
                <input
                  type="hidden"
                  name="componentId"
                  value={initial?.componentId ? String(initial.componentId) : ''}
                />
              </div>
            </>
          )}
          {issue && (
            <fieldset className="explicit-clears">
              <legend>Remove optional values</legend>
              <p className="hint">
                Select only the values you want to remove. Other blank fields keep their current
                values.
              </p>
              {[
                ['clearParentIssue', 'Parent issue'],
                ['clearComponent', 'Component'],
                ['clearStoryPoints', 'Story points'],
                ['clearDueDate', 'Due date'],
              ].map(([flag, title]) => (
                <label key={flag}>
                  <input
                    type="checkbox"
                    name={flag}
                    checked={clears[flag]}
                    onChange={(e) => setClears({ ...clears, [flag]: e.target.checked })}
                  />
                  Clear {title.toLowerCase()}
                </label>
              ))}
            </fieldset>
          )}
        </div>
        <div className="form-submit-feedback" aria-live="polite">
          <ErrorState error={validation || (assignee ? assignmentError(error) : error)} />
          {saveBlocked && <p role="status">Saving is currently unavailable. Resolve the blocking issue before trying again.</p>}
          {!issue && workflow.isFetching && <p role="status">Loading project workflow…</p>}
          {!issue && workflow.error && <ErrorState error={workflow.error} retry={workflow.refetch} />}
        </div>
        <div className="form-footer">
          <p className="footer-hint">
            Fill out required fields marked with * before creating your issue.
          </p>
          <div className="actions">
            <Link className="button" to={issue ? `/issues/${issue.id}` : '/issues'}>
              Cancel
            </Link>
            <button
              className="primary"
              disabled={
                busy || saveBlocked
              }
            >
              {busy ? 'Saving…' : issue ? 'Save changes' : 'Create issue'}
            </button>
          </div>
        </div>
      </fieldset>
    </form>
  );
}
