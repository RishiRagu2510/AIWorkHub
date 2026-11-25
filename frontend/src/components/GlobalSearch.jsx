import { useState, useEffect, useRef } from 'react';
import { useNavigate, useLocation } from 'react-router';
import { useSearchIssuesQuery, useProjectsQuery } from '../services/api/api';
import { useEmployeesQuery, useEmployeeReferencesQuery } from '../services/api/employees';
import { activePortal, portalKind } from '../utils/portals';
import { useAuth } from '../features/auth/context';
import { Icon } from './Icon';
import { Badge } from './common';
import EmployeePhoto from '../features/employees/EmployeePhoto';

export default function GlobalSearch({ open, onClose }) {
  const { can } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [query, setQuery] = useState('');
  const [activeIndex, setActiveIndex] = useState(0);
  const inputRef = useRef(null);

  const isPersona = activePortal === 'employee' || portalKind(location.pathname) === 'employee';

  const issuesQuery = useSearchIssuesQuery(
    { Text: query, Page: 1, PageSize: 6 },
    { skip: !open || !query.trim() || !can('ISSUE_VIEW') || isPersona },
  );

  const projectsQuery = useProjectsQuery(undefined, {
    skip: !open || !can('PROJECT_VIEW') || isPersona,
  });

  const employeesQuery = useEmployeesQuery(
    { search: query.trim(), page: 1, pageSize: 8 },
    { skip: !open || !query.trim() || !can('EMPLOYEE_VIEW') || !isPersona },
  );

  const referencesQuery = useEmployeeReferencesQuery(undefined, {
    skip: !open || !can('EMPLOYEE_VIEW') || !isPersona,
  });

  const refName = (id) => referencesQuery.data?.find((r) => r.id === id)?.name || '';

  const matchingProjects = isPersona
    ? []
    : (projectsQuery.data || [])
        .filter((p) => {
          if (!query.trim()) return false;
          const q = query.toLowerCase();
          return (
            p.projectName?.toLowerCase().includes(q) ||
            p.key?.toLowerCase().includes(q) ||
            p.description?.toLowerCase().includes(q)
          );
        })
        .slice(0, 4);

  const issueItems = isPersona ? [] : (issuesQuery.data?.items || []);
  const employeeItems = isPersona && can('EMPLOYEE_VIEW') ? (employeesQuery.data?.items || []) : [];

  const allResults = isPersona
    ? employeeItems.map((e) => ({ type: 'employee', item: e }))
    : [
        ...matchingProjects.map((p) => ({ type: 'project', item: p })),
        ...issueItems.map((i) => ({ type: 'issue', item: i })),
      ];

  useEffect(() => {
    if (open) {
      setQuery('');
      setActiveIndex(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [open]);

  useEffect(() => {
    setActiveIndex(0);
  }, [query]);

  function handleSelect(result) {
    if (!result) return;
    onClose();
    if (result.type === 'employee') {
      navigate(`/employees/${result.item.id}`);
    } else if (result.type === 'project') {
      navigate(`/projects/${result.item.id}`);
    } else {
      navigate(`/issues/${result.item.id}`);
    }
  }

  function handleKeyDown(e) {
    if (e.key === 'Escape') {
      e.preventDefault();
      onClose();
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      setActiveIndex((prev) => (prev < allResults.length - 1 ? prev + 1 : 0));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIndex((prev) => (prev > 0 ? prev - 1 : allResults.length - 1));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      if (allResults[activeIndex]) {
        handleSelect(allResults[activeIndex]);
      } else if (query.trim()) {
        onClose();
        if (isPersona) {
          navigate(can('EMPLOYEE_CREATE') ? `/employees/admin?search=${encodeURIComponent(query.trim())}` : `/employees?search=${encodeURIComponent(query.trim())}`);
        } else {
          navigate(`/issues?Text=${encodeURIComponent(query.trim())}`);
        }
      }
    }
  }

  if (!open) return null;

  const isSearching = isPersona ? employeesQuery.isFetching : issuesQuery.isFetching;

  return (
    <div className="search-palette-overlay" onClick={onClose} role="dialog" aria-modal="true" aria-label="Global search">
      <div className="search-palette-modal" onClick={(e) => e.stopPropagation()}>
        <div className="search-palette-header">
          <Icon name="search" />
          <input
            ref={inputRef}
            type="search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={
              isPersona
                ? 'Search employees by name, code, or department…'
                : 'Search issues, projects, or type to filter…'
            }
            aria-label="Search"
          />
          <kbd className="search-palette-esc" onClick={onClose}>ESC</kbd>
        </div>

        <div className="search-palette-body">
          {!query.trim() && (
            <div className="search-palette-hints">
              <span className="eyebrow">QUICK ACTIONS</span>
              <div className="search-quick-links">
                {isPersona ? (
                  <>
                    {can('EMPLOYEE_VIEW') && (
                      <button onClick={() => { onClose(); navigate('/profile'); }}>
                        <Icon name="user" /> My profile
                      </button>
                    )}
                    {can('EMPLOYEE_VIEW') && (
                      <button onClick={() => { onClose(); navigate('/employees/org-chart'); }}>
                        <Icon name="user" /> Organization chart
                      </button>
                    )}
                    {can('EMPLOYEE_VIEW') && (
                      <button onClick={() => { onClose(); navigate('/employees/leave'); }}>
                        <Icon name="clock" /> Leave / WFH
                      </button>
                    )}
                    {can('EMPLOYEE_CREATE') ? (
                      <button onClick={() => { onClose(); navigate('/employees/admin'); }}>
                        <Icon name="user" /> Employee management
                      </button>
                    ) : can('EMPLOYEE_VIEW') ? (
                      <button onClick={() => { onClose(); navigate('/employees'); }}>
                        <Icon name="user" /> Employee directory
                      </button>
                    ) : null}
                  </>
                ) : (
                  <>
                    {can('ISSUE_CREATE') && (
                      <button onClick={() => { onClose(); navigate('/issues/create'); }}>
                        <Icon name="plus" /> Create new issue
                      </button>
                    )}
                    {can('ISSUE_VIEW') && (
                      <button onClick={() => { onClose(); navigate('/issues'); }}>
                        <Icon name="issues" /> Browse all issues
                      </button>
                    )}
                    {can('PROJECT_VIEW') && (
                      <button onClick={() => { onClose(); navigate('/projects'); }}>
                        <Icon name="folder" /> Browse projects
                      </button>
                    )}
                    {can('TIMESHEET_VIEW') && (
                      <button onClick={() => { onClose(); navigate('/timesheets/my'); }}>
                        <Icon name="clock" /> My timesheet
                      </button>
                    )}
                  </>
                )}
              </div>
            </div>
          )}

          {query.trim() && (
            <>
              {isPersona && employeeItems.length > 0 && (
                <div className="search-group">
                  <div className="search-group-title">EMPLOYEES</div>
                  {employeeItems.map((emp, idx) => {
                    const isSelected = activeIndex === idx;
                    const empName = `${emp.preferredName || emp.firstName} ${emp.lastName}`;
                    const initials = `${emp.firstName?.[0] || ''}${emp.lastName?.[0] || ''}`.toUpperCase();
                    const dept = refName(emp.departmentId);
                    const grade = refName(emp.gradeId);
                    const metaParts = [emp.workEmail, dept, grade].filter(Boolean);

                    return (
                      <div
                        key={`e-${emp.id}`}
                        className={`search-result-item${isSelected ? ' is-active' : ''}`}
                        onClick={() => handleSelect({ type: 'employee', item: emp })}
                        onMouseEnter={() => setActiveIndex(idx)}
                      >
                        <EmployeePhoto
                          src={emp.profilePhotoUrl}
                          alt={empName}
                          initials={initials || 'E'}
                          className="search-item-avatar"
                        />
                        <div className="search-item-content">
                          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                            <span className="search-item-title">{empName}</span>
                            {emp.employeeCode && (
                              <span className="search-item-code">#{emp.employeeCode}</span>
                            )}
                          </div>
                          {metaParts.length > 0 && (
                            <span className="search-item-subtitle">{metaParts.join(' · ')}</span>
                          )}
                        </div>
                        {emp.employmentStatus && <Badge value={emp.employmentStatus} />}
                      </div>
                    );
                  })}
                </div>
              )}

              {!isPersona && (
                <>
                  {matchingProjects.length > 0 && (
                    <div className="search-group">
                      <div className="search-group-title">PROJECTS</div>
                      {matchingProjects.map((p, idx) => {
                        const isSelected = activeIndex === idx;
                        return (
                          <div
                            key={`p-${p.id}`}
                            className={`search-result-item${isSelected ? ' is-active' : ''}`}
                            onClick={() => handleSelect({ type: 'project', item: p })}
                            onMouseEnter={() => setActiveIndex(idx)}
                          >
                            <span className="search-item-icon">
                              <Icon name="folder" />
                            </span>
                            <div className="search-item-content">
                              <span className="search-item-title">{p.projectName || p.key}</span>
                              <span className="search-item-subtitle">{p.accountName || p.name}</span>
                            </div>
                            <Badge value={p.projectType} />
                          </div>
                        );
                      })}
                    </div>
                  )}

                  {issueItems.length > 0 && (
                    <div className="search-group">
                      <div className="search-group-title">ISSUES</div>
                      {issueItems.map((issue, idx) => {
                        const overallIndex = matchingProjects.length + idx;
                        const isSelected = activeIndex === overallIndex;
                        return (
                          <div
                            key={`i-${issue.id}`}
                            className={`search-result-item${isSelected ? ' is-active' : ''}`}
                            onClick={() => handleSelect({ type: 'issue', item: issue })}
                            onMouseEnter={() => setActiveIndex(overallIndex)}
                          >
                            <span className="search-item-key">{issue.issueKey}</span>
                            <div className="search-item-content">
                              <span className="search-item-title">{issue.summary}</span>
                            </div>
                            <Badge value={issue.statusCode} />
                            <Badge value={issue.priority} />
                          </div>
                        );
                      })}
                    </div>
                  )}
                </>
              )}

              {query.trim() && allResults.length === 0 && !isSearching && (
                <div className="search-empty-results">
                  <p>No results found for &ldquo;{query}&rdquo;</p>
                  {isPersona ? (
                    can('EMPLOYEE_CREATE') && (
                      <button
                        className="button"
                        onClick={() => {
                          onClose();
                          navigate(`/employees/admin?search=${encodeURIComponent(query.trim())}`);
                        }}
                      >
                        Search in Employee Management →
                      </button>
                    )
                  ) : (
                    <button
                      className="button"
                      onClick={() => {
                        onClose();
                        navigate(`/issues?Text=${encodeURIComponent(query.trim())}`);
                      }}
                    >
                      Search in issues page →
                    </button>
                  )}
                </div>
              )}

              {isSearching && (
                <div className="search-loading-hint">Searching…</div>
              )}
            </>
          )}
        </div>

        <div className="search-palette-footer">
          <span><kbd>↑</kbd> <kbd>↓</kbd> navigate</span>
          <span><kbd>↵</kbd> select</span>
          <span><kbd>ESC</kbd> dismiss</span>
        </div>
      </div>
    </div>
  );
}
