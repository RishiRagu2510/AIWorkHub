import React, { useMemo, useState } from 'react';
import { useAuth } from '../features/auth/context';
import { request } from '../services/api/client';
import { useDeleteWorkLogMutation, useWorkLogsQuery } from '../services/api/timesheets';
import { useAbsenceRequestsQuery, useSaveAbsenceMutation } from '../services/api/absence';
import { useEmployeeQuery, useEmployeeSectionQuery, useSaveEmployeeMutation } from '../services/api/employees';
import { useCommentsQuery, useDeleteCommentMutation } from '../services/api/api';

export const DELETE_KINDS = [
  { id: 'worklog', label: 'Time logs', hint: 'Your time logs from the last 60 days' },
  { id: 'leave', label: 'Leave requests', hint: 'Your pending leave and work-from-home requests' },
  { id: 'skills', label: 'Skills', hint: 'Skills on your profile' },
  { id: 'education', label: 'Education', hint: 'Education on your profile' },
  { id: 'certifications', label: 'Certifications', hint: 'Certifications on your profile' },
  { id: 'experience', label: 'Experience', hint: 'Work experience on your profile' },
  { id: 'languages', label: 'Languages', hint: 'Languages on your profile' },
  { id: 'comments', label: 'My comments', hint: 'Your comments on an issue' },
];

const messageOf = (error) => error?.message || error?.data?.message || 'It was not deleted.';
const pad = (n) => String(n).padStart(2, '0');
const dateText = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const shorten = (text, n = 90) => (text && text.length > n ? `${text.slice(0, n - 1)}…` : text || '');
const hoursText = (minutes) => {
  const m = Number(minutes) || 0;
  return `${Math.floor(m / 60)}h${m % 60 ? ` ${m % 60}m` : ''}`;
};

/** One entry with a Delete button that asks once more. Nothing is removed until the person presses the red button. */
function Entry({ title, detail, lockedReason, verb = 'Delete', busy, onConfirm, error }) {
  const [asking, setAsking] = useState(false);
  return (
    <li className="skills-row delete-row">
      <div className="delete-row-main">
        <span className="skills-name">{title}</span>
        {detail && <span className="delete-detail">{detail}</span>}
        {lockedReason && <span className="delete-locked">{lockedReason}</span>}
        {error && <span className="worklog-hint error" role="alert">{error}</span>}
      </div>
      {!lockedReason &&
        (asking ? (
          <span className="skills-confirm">
            <span className="delete-ask">{verb} this?</span>
            <button
              type="button"
              className="skills-btn danger"
              disabled={busy}
              onClick={async () => {
                const done = await onConfirm();
                if (done === false) setAsking(false);
              }}
            >
              {verb}
            </button>
            <button type="button" className="skills-btn" onClick={() => setAsking(false)} disabled={busy}>Keep</button>
          </span>
        ) : (
          <button type="button" className="skills-btn danger-outline" aria-label={`${verb} ${title}`} disabled={busy} onClick={() => setAsking(true)}>
            {verb}
          </button>
        ))}
    </li>
  );
}

function Listing({ query, empty, children, loading = 'Loading…' }) {
  if (query.isLoading) return <p className="skills-empty">{loading}</p>;
  if (query.isError) return <div className="worklog-hint error" role="alert">{messageOf(query.error)}</div>;
  if (!children || (Array.isArray(children) && children.length === 0)) return <p className="skills-empty">{empty}</p>;
  return <ul className="skills-list">{children}</ul>;
}

function useOutcome() {
  const [notice, setNotice] = useState('');
  const [errors, setErrors] = useState({});
  return {
    notice,
    errors,
    ok: (text) => setNotice(text),
    fail: (id, err) => setErrors((prev) => ({ ...prev, [id]: messageOf(err) })),
    clear: (id) => setErrors((prev) => ({ ...prev, [id]: undefined })),
  };
}

function Notice({ outcome }) {
  return outcome.notice ? <div className="leave-days-note" role="status">{outcome.notice}</div> : null;
}

function WorkLogs({ disabled }) {
  const { session } = useAuth();
  const me = session?.user?.id;
  const range = useMemo(() => {
    const to = new Date();
    const from = new Date();
    from.setDate(from.getDate() - 60);
    return { From: dateText(from), To: dateText(to) };
  }, []);
  const query = useWorkLogsQuery({ ...range, UserId: me, Page: 1, PageSize: 50 }, { refetchOnMountOrArgChange: true });
  const [remove, { isLoading }] = useDeleteWorkLogMutation();
  const outcome = useOutcome();
  const rows = (query.data?.items || [])
    .filter((w) => me == null || String(w.userId) === String(me))
    .sort((a, b) => String(b.workDate).localeCompare(String(a.workDate)) || b.id - a.id);

  return (
    <>
      <Listing query={query} empty="You have no time logs in the last 60 days.">
        {rows.map((w) => (
          <Entry
            key={w.id}
            title={`${w.issueKey || 'Time log'} · ${hoursText(w.durationMinutes)} · ${w.workDate}`}
            detail={shorten(w.description || w.issueSummary)}
            lockedReason={['DRAFT', 'REJECTED'].includes(w.periodStatus) ? '' : `The timesheet is ${String(w.periodStatus || 'submitted').toLowerCase()}, so this log can't be deleted.`}
            busy={isLoading || disabled}
            error={outcome.errors[w.id]}
            onConfirm={async () => {
              outcome.clear(w.id);
              try {
                await remove({ id: w.id, version: w.version }).unwrap();
                outcome.ok(`Deleted the ${hoursText(w.durationMinutes)} log on ${w.issueKey || 'the issue'} (${w.workDate}).`);
              } catch (err) {
                outcome.fail(w.id, err);
                return false;
              }
              return true;
            }}
          />
        ))}
      </Listing>
      <Notice outcome={outcome} />
    </>
  );
}

function Leave({ disabled }) {
  const year = new Date().getFullYear();
  const thisYear = useAbsenceRequestsQuery({ year }, { refetchOnMountOrArgChange: true });
  const nextYear = useAbsenceRequestsQuery({ year: year + 1 }, { refetchOnMountOrArgChange: true });
  const [save, { isLoading }] = useSaveAbsenceMutation();
  const outcome = useOutcome();
  const rows = [...(thisYear.data?.items || []), ...(nextYear.data?.items || [])]
    .filter((r) => r.status === 'PENDING')
    .sort((a, b) => String(a.startDate).localeCompare(String(b.startDate)));
  const kindName = (kind) => String(kind || 'leave').replace(/_/g, ' ').toLowerCase();

  return (
    <>
      <Listing query={thisYear} empty="You have no pending leave or work-from-home requests. Approved ones can't be cancelled here.">
        {rows.length === 0 ? null : rows.map((r) => (
          <Entry
            key={r.id}
            verb="Cancel request"
            title={`${kindName(r.kind)} · ${r.startDate}${r.endDate && r.endDate !== r.startDate ? ` to ${r.endDate}` : ''}`}
            detail={`${r.days} day${Number(r.days) === 1 ? '' : 's'}${r.reason ? ` · ${shorten(r.reason, 70)}` : ''}`}
            busy={isLoading || disabled}
            error={outcome.errors[r.id]}
            onConfirm={async () => {
              outcome.clear(r.id);
              try {
                await save({ id: r.id, body: { action: 'CANCEL', version: r.version } }).unwrap();
                outcome.ok(`Cancelled your ${kindName(r.kind)} request for ${r.startDate}.`);
              } catch (err) {
                outcome.fail(r.id, err);
                return false;
              }
              return true;
            }}
          />
        ))}
      </Listing>
      <Notice outcome={outcome} />
    </>
  );
}

const PROFILE = {
  skills: {
    noun: 'skill',
    title: (e) => e.skillName || e.name || 'Skill',
    detail: (e) => [e.category, e.proficiency ? `level ${e.proficiency}/5` : '', e.yearsOfExperience != null ? `${e.yearsOfExperience} yrs` : ''].filter(Boolean).join(' · '),
  },
  education: {
    noun: 'education entry',
    title: (e) => e.institution || 'Education',
    detail: (e) => [e.qualification, e.fieldOfStudy].filter(Boolean).join(' · '),
  },
  certifications: {
    noun: 'certification',
    title: (e) => e.name || 'Certification',
    detail: (e) => [e.issuer, e.expiryDate ? `expires ${String(e.expiryDate).slice(0, 10)}` : ''].filter(Boolean).join(' · '),
  },
  experience: {
    noun: 'experience entry',
    title: (e) => [e.title, e.company].filter(Boolean).join(' at ') || 'Experience',
    detail: (e) => `${String(e.startDate || '').slice(0, 10)} → ${e.endDate ? String(e.endDate).slice(0, 10) : 'Present'}`,
  },
  languages: {
    noun: 'language',
    title: (e) => e.name || 'Language',
    detail: (e) => e.proficiency || '',
  },
};

function ProfileEntries({ section, disabled }) {
  const meQuery = useEmployeeQuery('me', { refetchOnMountOrArgChange: true });
  const list = useEmployeeSectionQuery({ id: meQuery.data?.id, section }, { skip: !meQuery.data?.id, refetchOnMountOrArgChange: true });
  const [save, { isLoading }] = useSaveEmployeeMutation();
  const outcome = useOutcome();
  const spec = PROFILE[section];
  const rows = Array.isArray(list.data) ? list.data : list.data?.items || [];
  // Each delete changes the profile's version; the list refetches afterwards, so wait for it before the next one.
  const busy = isLoading || disabled || meQuery.isFetching || list.isFetching;
  const query = meQuery.isLoading || meQuery.isError ? meQuery : list;

  return (
    <>
      <Listing query={query} empty={`There are no ${spec.noun}s on your profile.`}>
        {rows.length === 0 ? null : rows.map((e) => (
          <Entry
            key={e.id}
            title={spec.title(e)}
            detail={spec.detail(e)}
            busy={busy}
            error={outcome.errors[e.id]}
            onConfirm={async () => {
              outcome.clear(e.id);
              try {
                await save({ path: `/me/${section}/${e.id}`, method: 'DELETE', version: meQuery.data.version }).unwrap();
                outcome.ok(`Deleted ${spec.title(e)} from your ${spec.noun}s.`);
              } catch (err) {
                outcome.fail(e.id, err);
                return false;
              }
              return true;
            }}
          />
        ))}
      </Listing>
      <Notice outcome={outcome} />
    </>
  );
}

function IssueComments({ issueKey, disabled }) {
  const { session } = useAuth();
  const me = session?.user?.id;
  const isAdmin = session?.user?.role === 'ADMIN';
  const [typed, setTyped] = useState(issueKey || '');
  const [issue, setIssue] = useState(null);
  const [finding, setFinding] = useState(false);
  const [findError, setFindError] = useState('');
  const comments = useCommentsQuery(issue?.id, { skip: !issue, refetchOnMountOrArgChange: true });
  const [remove, { isLoading }] = useDeleteCommentMutation();
  const outcome = useOutcome();

  async function find(e) {
    e?.preventDefault();
    const key = typed.trim().toUpperCase();
    if (!key || finding) return;
    setFinding(true);
    setFindError('');
    setIssue(null);
    try {
      const found = await request(`/api/issues/key/${encodeURIComponent(key)}`);
      setIssue({ id: found.id, key: found.key || found.issueKey || key });
    } catch (err) {
      setFindError(err?.status === 404 ? `I could not find an issue ${key}.` : messageOf(err));
    } finally {
      setFinding(false);
    }
  }

  // Open straight away when the message named the issue.
  React.useEffect(() => {
    if (issueKey) find();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const rows = (comments.data || []).filter((c) => isAdmin || String(c.authorUserId) === String(me));

  return (
    <>
      <form className="delete-find" onSubmit={find}>
        <input
          className="worklog-title-input"
          value={typed}
          onChange={(e) => setTyped(e.target.value)}
          placeholder="Issue key, for example FLP360-17"
          aria-label="Issue key"
        />
        <button type="submit" className="skills-btn" disabled={!typed.trim() || finding || disabled}>{finding ? 'Finding…' : 'Show comments'}</button>
      </form>
      {findError && <div className="worklog-hint error" role="alert">{findError}</div>}
      {issue && (
        <Listing query={comments} empty={`You have no comments on ${issue.key}.`}>
          {rows.length === 0 ? null : rows.map((c) => (
            <Entry
              key={c.id}
              title={shorten(c.body, 110)}
              detail={`${issue.key} · ${String(c.createdAt || '').slice(0, 10)}`}
              busy={isLoading || disabled}
              error={outcome.errors[c.id]}
              onConfirm={async () => {
                outcome.clear(c.id);
                try {
                  await remove({ id: issue.id, commentId: c.id, version: c.version }).unwrap();
                  outcome.ok(`Deleted your comment on ${issue.key}.`);
                } catch (err) {
                  outcome.fail(c.id, err);
                  return false;
                }
                return true;
              }}
            />
          ))}
        </Listing>
      )}
      <Notice outcome={outcome} />
    </>
  );
}

/**
 * The Delete form in the chat. It lists the person's own entries of one kind, and each has a Delete button that asks
 * again. It deletes through the same endpoints, and so the same permission checks, as the portals; Athenna itself never
 * deletes anything.
 */
export default function DeleteCard({ form, disabled }) {
  const [kind, setKind] = useState(DELETE_KINDS.some((k) => k.id === form?.kind) ? form.kind : '');
  const current = DELETE_KINDS.find((k) => k.id === kind);

  return (
    <section className="worklog-card skills-card delete-card" aria-label="Delete">
      <div className="issue-create-card-header">
        <div className="issue-create-title-group">
          <span className="issue-create-title-icon">🗑️</span>
          <div className="worklog-title">Delete</div>
        </div>
      </div>

      <div className="worklog-label" id="delete-kind-label">What do you want to delete?</div>
      <div className="skills-chips" role="group" aria-labelledby="delete-kind-label">
        {DELETE_KINDS.map((k) => (
          <button
            key={k.id}
            type="button"
            className={`skills-chip ${kind === k.id ? 'selected' : ''}`}
            aria-pressed={kind === k.id}
            onClick={() => setKind(k.id)}
          >
            {k.label}
          </button>
        ))}
      </div>

      {current ? <p className="skills-empty">{current.hint}. Press Delete next to one, then confirm. Nothing is removed before that.</p> : <p className="skills-empty">Pick one to see your entries. Nothing is removed until you confirm it.</p>}

      {kind === 'worklog' && <WorkLogs key={kind} disabled={disabled} />}
      {kind === 'leave' && <Leave key={kind} disabled={disabled} />}
      {PROFILE[kind] && <ProfileEntries key={kind} section={kind} disabled={disabled} />}
      {kind === 'comments' && <IssueComments key={kind} issueKey={form?.issueKey} disabled={disabled} />}
    </section>
  );
}
