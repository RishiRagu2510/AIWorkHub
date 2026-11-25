import './onboarding-review.scss';
import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useAuth } from '../auth/context';
import {
  useOnboardingRequestQuery,
  useReviewOnboardingMutation,
  useCompleteOnboardingMutation,
} from '../../services/api/onboarding';
import { useEmployeeReferencesQuery } from '../../services/api/employees';
import { ErrorState, Field, LoadingState } from '../../components/common';
import UserIdentity from '../../components/UserIdentity';
import Link from '../../components/PortalLink';
import EmployeeEditor from './EmployeeEditor';
export default function OnboardingReview({ id, standalone = false, completing = false, onCompleteClose }) {
  const navigate = useNavigate();
  const query = useOnboardingRequestQuery(id);
  const request = query.data;
  const { session, can } = useAuth();
  const reviewer =
    [session?.user?.role, ...(session?.user?.roles || [])].some((r) =>
      ['ADMIN', 'HR'].includes(r),
    ) && can('EMPLOYEE_CREATE');
  const [review, reviewState] = useReviewOnboardingMutation();
  const [complete, completeState] = useCompleteOnboardingMutation();
  const editing = completing && reviewer;
  const [note, setNote] = useState('');
  const refs = useEmployeeReferencesQuery(undefined, { skip: !editing });
  if (query.error) return <ErrorState error={query.error} retry={query.refetch} />;
  if (query.isLoading) return <LoadingState />;
  if (!request?.isEmployeeRequest) return standalone ? <p role="status">This issue has no employee onboarding request.</p> : null;
  const { proposal, display } = request.data;
  const employee = proposal.employee;

  function formatEnum(val) {
    if (!val) return val;
    return val
      .toLowerCase()
      .replace(/_/g, ' ')
      .replace(/\b\w/g, (c) => c.toUpperCase());
  }

  const sections = [
    {
      title: 'Candidate & Contact',
      items: [
        ['Name', `${employee.firstName} ${employee.lastName}`],
        ['Preferred name', employee.preferredName],
        ['Work email', proposal.workEmail],
        ['Phone', employee.phone],
        ['Employee code', employee.employeeCode],
      ].filter(([, v]) => v),
    },
    {
      title: 'Role & Classification',
      items: [
        ['Department', display.department],
        ['Career profile', display.careerProfile],
        ['Grade', display.grade],
        ['Job title', display.jobTitle],
        ['Team', display.team],
      ].filter(([, v]) => v),
    },
    {
      title: 'Reporting Hierarchy',
      items: [
        ['Reporting manager', `${display.manager.firstName} ${display.manager.lastName}`],
        ['Manager employee ID', display.manager.employeeCode],
        ['Manager email', display.manager.workEmail],
      ].filter(([, v]) => v),
    },
    {
      title: 'Employment Terms',
      items: [
        ['Employment type', formatEnum(employee.employmentType)],
        ['Employment status', formatEnum(employee.employmentStatus)],
        ['Joining date', employee.joiningDate],
        ['Work location', employee.location],
      ].filter(([, v]) => v),
    },
    {
      title: 'Additional Request Context',
      items: [
        ['Reason', proposal.reason],
        ['Requested access / responsibilities', proposal.requestedAccess],
        ['Additional notes', proposal.notes],
      ].filter(([, v]) => v),
    },
  ].filter((s) => s.items.length > 0);

  const initials =
    `${employee.firstName?.[0] || ''}${employee.lastName?.[0] || ''}`.toUpperCase() || 'HR';

  return (
    <section className="panel onboarding-review">
      <header className="onboarding-review-heading">
        <div className="onboarding-heading-left">
          <div className="candidate-avatar" aria-hidden="true">
            {initials}
          </div>
          <div className="candidate-heading-text">
            <div className="onboarding-eyebrow-row">
              <p className="onboarding-eyebrow">Employee request</p>
              <span className={`onboarding-status status-${request.status.toLowerCase()}`}>
                <span className="status-dot" aria-hidden="true" />
                {request.status}
              </span>
            </div>
            <h2>{employee.firstName} {employee.lastName}</h2>
            <p className="onboarding-requester-info">
              Requested by <UserIdentity id={request.requestedBy} /> ·{' '}
              {new Date(request.requestedAt).toLocaleString()}
            </p>
          </div>
        </div>
      </header>

      {!editing && (
        <div className="onboarding-sections-grid">
          {sections.map((section) => (
            <div key={section.title} className="onboarding-review-group">
              <h3 className="onboarding-group-title">{section.title}</h3>
              <dl className="onboarding-review-details">
                {section.items.map(([key, value]) => (
                  <div key={key} className="onboarding-detail-cell">
                    <dt>{key}</dt>
                    <dd style={{ whiteSpace: 'pre-wrap' }}>{value}</dd>
                  </div>
                ))}
              </dl>
            </div>
          ))}
        </div>
      )}

      {(request.reviewedBy || request.employeeId) && (
        <div className="onboarding-audit-profile-card">
          <div className="audit-card-left">
            <div className="audit-badge-icon" aria-hidden="true">
              <svg
                width="20"
                height="20"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.2"
                strokeLinecap="round"
                strokeLinejoin="round"
              >
                <path d="M20 6L9 17l-5-5" />
              </svg>
            </div>
            <div className="audit-card-info">
              <div className="audit-card-title-row">
                <strong>Review Completed</strong>
                <span className="audit-verified-pill">Verified & Provisioned</span>
              </div>
              <p className="audit-meta">
                Reviewed by <UserIdentity id={request.reviewedBy} /> ·{' '}
                {new Date(request.reviewedAt).toLocaleString()} {request.reviewNote}
              </p>
            </div>
          </div>
          {request.employeeId && (
            <div className="audit-card-action">
              <span className="employee-created-tag">Employee created:</span>
              <Link
                className="button primary view-profile-btn"
                to={`/employees/${request.employeeId}`}
              >
                View employee profile
                <svg
                  width="14"
                  height="14"
                  viewBox="0 0 24 24"
                  fill="none"
                  stroke="currentColor"
                  strokeWidth="2"
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  aria-hidden="true"
                >
                  <line x1="5" y1="12" x2="19" y2="12" />
                  <polyline points="12 5 19 12 12 19" />
                </svg>
              </Link>
            </div>
          )}
        </div>
      )}
      {reviewer && request.status === 'SUBMITTED' && (
        <>
          <Field label="Review notes">
            <textarea value={note} maxLength={2000} onChange={(e) => setNote(e.target.value)} />
          </Field>
          <div className="form-actions">
            {['APPROVED', 'REJECTED'].map((decision) => (
              <button
                key={decision}
                disabled={reviewState.isLoading}
                onClick={() => review({ id, revision: request.revision, decision, note })}
              >
                {decision === 'APPROVED' ? 'Approve' : 'Reject'}
              </button>
            ))}
          </div>
        </>
      )}
      <ErrorState error={reviewState.error} />
      {!editing && <ErrorState error={completeState.error} />}
      {reviewer && request.status === 'APPROVED' && !editing && (
        <Link className="button primary" to={`/employees/admin?create=1&onboarding=${id}`}>Review and complete profile</Link>
      )}
      {editing && request.status === 'APPROVED' && refs.data && (
        <EmployeeEditor
          approvedWorkEmail={proposal.workEmail}
          initialValues={{ ...employee, userId: null }}
          references={refs.data}
          onClose={onCompleteClose || (() => navigate(`/employees/onboarding/${id}`))}
          submitOverride={(body) => complete({ id, body }).unwrap()}
          submitError={completeState.error}
          submitBusy={completeState.isLoading}
          submitLabel="Complete profile"
        />
      )}
      {editing && refs.isLoading && <LoadingState />}
      {editing && <ErrorState error={refs.error} retry={refs.refetch} />}
    </section>
  );
}
