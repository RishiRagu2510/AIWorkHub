import AccountField from './AccountField';
import { useEffect, useRef, useState } from 'react';
import Link from '../../components/PortalLink';
import {
  useUpdateProjectMutation,
  useMoveProjectMutation,
  useDeleteProjectMutation,
} from '../../services/api/api';
import { useAuth } from '../auth/context';
import { Field, SelectField } from '../../components/common';
import VersionConflict from '../../components/VersionConflict';
import { useUnsavedChanges } from '../../components/unsavedContext';
import { useNotice } from '../../components/noticeContext';
export default function ProjectManageDialog({ project, projects, mode, close }) {
  const [initial] = useState(project);
  const [version, setVersion] = useState(project.version);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  const dialog = useRef(null);
  const lock = useRef(false);
  const { can } = useAuth();
  const notice = useNotice();
  const { markDirty, clearDirty, confirmLeave } = useUnsavedChanges();
  const [update] = useUpdateProjectMutation();
  const [move] = useMoveProjectMutation();
  const [remove] = useDeleteProjectMutation();
  useEffect(() => {
    const previous = document.activeElement;
    const element = dialog.current;
    element.showModal();
    return () => {
      element.close();
      previous?.focus();
    };
  }, []);
  const descendants = new Set([String(initial.id)]);
  for (let i = 0; i < projects.length; i++)
    for (const item of projects)
      if (descendants.has(String(item.parentProjectId))) descendants.add(String(item.id));
  async function submit(event) {
    event.preventDefault();
    if (lock.current || error?.status === 412) return;
    const values = Object.fromEntries(new FormData(event.currentTarget));
    if (mode === 'edit' && !values.name.trim()) {
      setError({ message: 'Project name cannot be blank.' });
      return;
    }
    lock.current = true;
    setBusy(true);
    setError(null);
    try {
      const args = { id: initial.id, version };
      await (
        mode === 'edit'
          ? update({
              ...args,
              body: {
                accountName: values.name.trim(),
                ...(values.accountId ? { accountId: Number(values.accountId) } : {}),
                description: values.description,
                ...(values.displayName?.trim() !== (initial.projectName || initial.key)
                  ? { displayName: values.displayName.trim() }
                  : {}),
                projectType: values.projectType,
                status: values.status,
              },
            })
          : mode === 'move'
            ? move({
                ...args,
                parentProjectId: values.parentProjectId ? Number(values.parentProjectId) : null,
              })
            : remove(args)
      ).unwrap();
      clearDirty();
      notice(`Project ${mode === 'delete' ? 'deleted' : mode === 'move' ? 'moved' : 'updated'}.`);
      close();
    } catch (problem) {
      setError(
        problem.status === 409
          ? {
              ...problem,
              message:
                mode === 'delete'
                  ? 'This project still contains records. Archive it instead of deleting it.'
                  : 'The project hierarchy changed or this move would create a cycle. Choose another parent.',
            }
          : problem,
      );
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  return (
    <dialog
      className="project-create-dialog"
      ref={dialog}
      aria-labelledby="manage-project-title"
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) confirmLeave(close);
      }}
    >
      <form onSubmit={submit} onChange={markDirty}>
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
              {mode === 'delete' ? (
                <>
                  <line x1="9" y1="14" x2="15" y2="14" />
                </>
              ) : mode === 'move' ? (
                <>
                  <polyline points="12 11 16 15 12 19" />
                  <line x1="8" y1="15" x2="16" y2="15" />
                </>
              ) : (
                <>
                  <path d="M12 20h9" />
                  <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5z" />
                </>
              )}
            </svg>
          </div>
          <div className="header-text">
            <div className="header-eyebrow">GRIDHUB · PROJECT GOVERNANCE</div>
            <h2 id="manage-project-title">
              {mode === 'edit' ? 'Edit' : mode === 'move' ? 'Move' : 'Delete'} {initial.key}
            </h2>
            <p>
              {mode === 'edit'
                ? 'Update account ownership, display title, and project configuration.'
                : mode === 'move'
                  ? 'Reassign this project under a new parent or root structure.'
                  : 'Permanently remove an unused project from the workspace.'}
            </p>
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
        <fieldset disabled={busy}>
          <div className="form-body">
            {mode === 'edit' && (
              <>
                <AccountField
                  projects={projects}
                  initialName={initial.accountName || initial.name}
                />
                <p className="hint">
                  Changing the account moves only this project. Other projects in that account are
                  unchanged.
                </p>
                <Field
                  label="Project name"
                  name="displayName"
                  defaultValue={initial.projectName || initial.key}
                  required
                  maxLength={1000}
                />
                <Field label="Description">
                  <textarea name="description" defaultValue={initial.description || ''} />
                </Field>
                <SelectField
                  label="Project type"
                  name="projectType"
                  values={['SOFTWARE', 'SERVICE', 'BUSINESS', 'BANKING']}
                  defaultValue={initial.projectType}
                />
                <SelectField
                  label="Project status"
                  name="status"
                  values={['ACTIVE', 'ARCHIVED', 'DISABLED']}
                  defaultValue={initial.status}
                />
              </>
            )}
            {mode === 'move' && (
              <>
                <SelectField
                  label="New parent project"
                  name="parentProjectId"
                  empty="None — top-level project"
                  defaultValue={initial.parentProjectId ?? ''}
                  values={projects
                    .filter((item) => item.status === 'ACTIVE' && !descendants.has(String(item.id)))
                    .map((item) => ({ value: item.id, label: `${item.key} · ${item.name}` }))}
                />
                <p>
                  Moving this project also moves its subtree. Inherited access will follow the new
                  parent.
                </p>
              </>
            )}
            {mode === 'delete' && (
              <p>
                Delete this empty project permanently? Projects containing issues, subprojects,
                memberships, or other references cannot be deleted. Its audit history will remain
                available.
              </p>
            )}
            {can('AUDIT_VIEW') && (
              <Link to={`/audit?EntityType=PROJECT&EntityId=${initial.id}`}>
                View project history
              </Link>
            )}
            <VersionConflict
              error={error}
              path={`/api/projects/${initial.id}`}
              onReviewed={(current) => {
                setVersion(current.version);
                setError(null);
              }}
            />
          </div>
          <div className="form-footer">
            <button type="button" onClick={() => confirmLeave(close)}>
              Cancel
            </button>
            <button
              className={mode === 'delete' ? 'danger' : 'primary'}
              disabled={busy || error?.status === 412}
            >
              {busy
                ? 'Saving…'
                : mode === 'delete'
                  ? 'Delete project'
                  : mode === 'move'
                    ? 'Move project'
                    : 'Save project'}
            </button>
          </div>
        </fieldset>
      </form>
    </dialog>
  );
}
