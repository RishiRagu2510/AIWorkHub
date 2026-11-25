import { useAuth } from '../auth/context';
import { useEmployeeProjectsQuery } from '../../services/api/employees';
import { QueryState } from '../../components/common';
import Link from '../../components/PortalLink';

export default function Allocation({ employee }) {
  const { can } = useAuth();
  const allowed = can('PROJECT_VIEW');
  const projects = useEmployeeProjectsQuery(employee.userId, {
    skip: !allowed,
    refetchOnMountOrArgChange: true,
    refetchOnFocus: true,
    refetchOnReconnect: true,
  });
  const active = (projects.data || []).filter(project =>
    project.status === 0 || String(project.status).toUpperCase() === 'ACTIVE');
  return (
    <section className="profile-stat profile-allocation-card" aria-label="Current allocation">
      <div className="allocation-card-heading">
        <span className="stat-title">Current allocation</span>
        <div className="allocation-header-right">
          {allowed && !projects.isFetching && !projects.error && active.length > 0 ? (
            <span className="allocation-active-badge">Active</span>
          ) : null}
          <span className="stat-icon-wrapper" aria-hidden="true">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
              <rect x="2" y="7" width="20" height="14" rx="2" ry="2" />
              <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
            </svg>
          </span>
        </div>
      </div>
      {!allowed ? (
        <p className="allocation-restricted">Restricted</p>
      ) : (
        <QueryState query={projects}>
          <div className="stat-body">
            {active.length ? (
              <div className="profile-allocation-projects">
                {active.slice(0, 3).map(project => (
                  <Link key={project.id} title={project.projectName} to={`/projects/${project.id}`}>
                    <span className="project-bullet" aria-hidden="true" />
                    <span className="project-name-text">{project.projectName}</span>
                  </Link>
                ))}
                {active.length > 3 && (
                  <span className="allocation-more-tag">+{active.length - 3} more</span>
                )}
              </div>
            ) : (
              <div className="allocation-empty-state">
                <strong className="allocation-empty-title">No active projects</strong>
                <span className="allocation-empty-sub">Currently unassigned</span>
              </div>
            )}
          </div>
          <div className="allocation-card-footer">
            <span>Project membership</span>
            <span className="membership-count">{active.length} {active.length === 1 ? 'project' : 'projects'}</span>
          </div>
        </QueryState>
      )}
    </section>
  );
}
