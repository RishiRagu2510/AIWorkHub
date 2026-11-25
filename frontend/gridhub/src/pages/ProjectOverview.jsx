import ProjectWorkflow from '../../../src/features/collaboration/ProjectWorkflow';
import { useState } from 'react';
import { useParams } from 'react-router';
import Link from '../../../src/components/PortalLink';
import { useProjectQuery, useSearchIssuesQuery, useAuditQuery } from '../../../src/services/api/api';
import { useAuth } from '../../../src/features/auth/context';
import { FeatureGuard } from '../../../src/features/auth/FeatureGuard';
import { ATHENNA } from '../../../src/features/auth/features';
import { PageHeader, QueryState, Badge } from '../../../src/components/common';
import { liveQueryOptions } from '../../../src/services/api/live';
import ProjectMembers from '../../../src/features/projects/ProjectMembers';
import WorkIssueList from '../../../src/features/issues/WorkIssueList';
import { AuditTable } from '../../../src/features/audit/AuditTable';
import { useRunAthennaCopilotMutation } from '../../../src/services/api/agent';
import '../../../src/styles/projects.scss';

function ProjectIssues({ id }) {
  const query = useSearchIssuesQuery({ ProjectId: id, Page: 1, PageSize: 50 }, liveQueryOptions);
  const [runCopilot, { isLoading: isGeneratingNotes }] = useRunAthennaCopilotMutation();
  const [releaseNotes, setReleaseNotes] = useState(null);
  const [showNotesModal, setShowNotesModal] = useState(false);

  const handleGenerateReleaseNotes = async () => {
    const issues = query.data?.items || [];
    if (!issues.length) {
      alert('No issues available to generate release notes.');
      return;
    }
    const issueSummaries = issues
      .map((i) => `- [${i.statusCode || 'STATUS'}] ${i.issueKey || ''}: ${i.summary || ''}`)
      .join('\n');

    try {
      const res = await runCopilot({
        feature: 'SummarizeReleaseNotes',
        inputText: `Project #${id} Issues:\n${issueSummaries}`,
      }).unwrap();
      if (res?.outputText) {
        setReleaseNotes(res.outputText);
        setShowNotesModal(true);
      }
    } catch (err) {
      console.error('Failed to generate release notes:', err);
    }
  };

  return (
    <section className="panel project-issues-panel">
      <div className="section-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <div style={{ background: '#eff6ff', color: '#2563eb', width: '36px', height: '36px', borderRadius: '8px', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="10" />
              <line x1="12" y1="8" x2="12" y2="12" />
              <line x1="12" y1="16" x2="12.01" y2="16" />
            </svg>
          </div>
          <div>
            <h2 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Accessible issues · {query.data?.totalCount ?? '0'}</h2>
            <p style={{ margin: 0, fontSize: '0.85rem', color: '#64748b' }}>Work items assigned or tracked in this project</p>
          </div>
        </div>
        <div style={{ display: 'flex', gap: '8px' }}>
          <FeatureGuard feature={ATHENNA}>
            <button
            type="button"
            className="button"
            onClick={handleGenerateReleaseNotes}
            disabled={isGeneratingNotes}
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              background: 'linear-gradient(135deg, rgba(79, 70, 229, 0.08) 0%, rgba(124, 58, 237, 0.08) 100%)',
              border: '1px solid rgba(124, 58, 237, 0.25)',
              color: '#4f46e5',
              fontWeight: 600,
            }}
          >
            <span>✦</span>
            <span>{isGeneratingNotes ? 'Generating Notes…' : 'Generate Release Notes with Athenna'}</span>
            </button>
          </FeatureGuard>
          <Link className="button" to={`/issues?ProjectId=${id}`}>
            View all issues →
          </Link>
        </div>
      </div>

      {showNotesModal && (
        <div style={{
          margin: '14px 0',
          padding: '16px',
          background: '#f8fafc',
          border: '1px solid #cbd5e1',
          borderRadius: '8px',
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px' }}>
            <h3 style={{ margin: 0, fontSize: '1rem', fontWeight: 600, color: '#1e293b' }}>
              ✦ Athenna Release Notes Draft
            </h3>
            <button
              type="button"
              onClick={() => setShowNotesModal(false)}
              style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '1.2rem' }}
            >
              ✕
            </button>
          </div>
          <pre style={{
            whiteSpace: 'pre-wrap',
            fontFamily: 'inherit',
            fontSize: '0.88rem',
            lineHeight: 1.5,
            color: '#334155',
            background: '#ffffff',
            padding: '12px',
            borderRadius: '6px',
            border: '1px solid #e2e8f0',
          }}>
            {releaseNotes}
          </pre>
        </div>
      )}

      <QueryState query={query}>
        <WorkIssueList issues={query.data?.items || []} />
      </QueryState>
    </section>
  );
}

function History({ id }) {
  const query = useAuditQuery(
    { EntityType: 'PROJECT', EntityId: id, Page: 1, PageSize: 20 },
    liveQueryOptions,
  );
  return (
    <section className="panel project-history-panel">
      <div className="section-header">
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <div style={{ background: '#f8fafc', color: '#64748b', width: '36px', height: '36px', borderRadius: '8px', display: 'flex', alignItems: 'center', justifyContent: 'center', border: '1px solid #e2e8f0' }}>
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="12" cy="12" r="10" />
              <polyline points="12 6 12 12 16 14" />
            </svg>
          </div>
          <div>
            <h2 style={{ margin: 0, fontSize: '1.1rem', fontWeight: 600 }}>Recent activity &amp; audit history</h2>
            <p style={{ margin: 0, fontSize: '0.85rem', color: '#64748b' }}>Audit trail of project updates and modifications</p>
          </div>
        </div>
        <button className="button" onClick={query.refetch} disabled={query.isFetching}>
          Refresh history
        </button>
      </div>
      <p className="work-empty" style={{ margin: '0 0 12px', fontSize: '13px' }}>
        Recent changes may take a moment to appear.{' '}
        <Link to={`/audit?EntityType=PROJECT&EntityId=${id}`}>View full audit trail</Link>
      </p>
      <QueryState query={query}>
        <AuditTable items={query.data?.items || []} />
      </QueryState>
    </section>
  );
}

export default function ProjectOverview() {
  const { projectId } = useParams();
  const { can, session } = useAuth();
  const query = useProjectQuery(projectId);

  return (
    <QueryState query={query}>
      {query.data && (
        <div className="project-overview-page">
          <PageHeader
            eyebrow={`Account · ${query.data.accountName || query.data.name}`}
            title={query.data.projectName || query.data.key}
            description={query.data.description || 'Project overview and workspace configuration.'}
          >
            <Link className="button" to="/projects">
              All projects
            </Link>
          </PageHeader>

          {/* Project Meta Bar */}
          <div className="project-overview-meta">
            <Badge value={query.data.status} />
            <Badge value={query.data.projectType} />
            {query.data.parentProjectId != null ? (
              <Link to={`/projects/${query.data.parentProjectId}`}>
                Parent project #{query.data.parentProjectId}
              </Link>
            ) : (
              <span>Top-level project</span>
            )}
          </div>

          {/* Quick Stats & KPI Grid */}
          <div className="project-overview-kpi-grid">
            <div className="project-kpi-card">
              <div className="kpi-icon" style={{ background: '#eff6ff', color: '#2563eb' }}>
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                  <polyline points="14 2 14 8 20 8" />
                  <line x1="16" y1="13" x2="8" y2="13" />
                  <line x1="16" y1="17" x2="8" y2="17" />
                  <polyline points="10 9 9 9 8 9" />
                </svg>
              </div>
              <div className="kpi-info">
                <span className="kpi-label">Project Type</span>
                <span className="kpi-value">{query.data.projectType || 'Standard'}</span>
              </div>
            </div>

            <div className="project-kpi-card">
              <div className="kpi-icon" style={{ background: '#f0fdf4', color: '#16a34a' }}>
                <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                  <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14" />
                  <polyline points="22 4 12 14.01 9 11.01" />
                </svg>
              </div>
              <div className="kpi-info">
                <span className="kpi-label">Lifecycle Status</span>
                <span className="kpi-value">{query.data.status || 'Active'}</span>
              </div>
            </div>
          </div>

          {can('ISSUE_VIEW') && <ProjectIssues id={projectId} />}
          {can('PROJECT_VIEW') && <ProjectWorkflow key={`workflow:${projectId}`} id={projectId} />}
          {session.user.role === 'ADMIN' && (
            <ProjectMembers project={query.data} />
          )}
          {can('AUDIT_VIEW') && <History id={projectId} />}
        </div>
      )}
    </QueryState>
  );
}
