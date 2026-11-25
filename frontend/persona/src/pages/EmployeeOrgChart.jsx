import { useState, useMemo, useRef, useEffect } from 'react';
import EmployeePhoto from '../../../src/features/employees/EmployeePhoto';
import Link from '../../../src/components/PortalLink';
import {
  useEmployeeFullChartQuery,
  useEmployeeReferencesQuery,
  useEmployeeQuery,
} from '../../../src/services/api/employees';
import { PageHeader, QueryState } from '../../../src/components/common';
import { useAuth } from '../../../src/features/auth/context';
import '../../../src/styles/employees.scss';
import '../org-chart.scss';

const avatarPalette = [
  { bg: '#fef3c7', text: '#b45309' }, // Amber
  { bg: '#e0f2fe', text: '#0369a1' }, // Sky
  { bg: '#f3e8ff', text: '#7e22ce' }, // Purple
  { bg: '#dcfce7', text: '#15803d' }, // Green
  { bg: '#ffe4e6', text: '#e11d48' }, // Pink
  { bg: '#ccfbf1', text: '#0f766e' }, // Teal
  { bg: '#ffedd5', text: '#c2410c' }, // Orange
];

function getAvatarStyle(str) {
  let hash = 0;
  const name = str || 'User';
  for (let i = 0; i < name.length; i++) {
    hash = name.charCodeAt(i) + ((hash << 5) - hash);
  }
  const idx = Math.abs(hash) % avatarPalette.length;
  return avatarPalette[idx];
}

export default function EmployeeOrgChart() {
  const query = useEmployeeFullChartQuery(undefined, {
    refetchOnMountOrArgChange: true,
    refetchOnFocus: true,
  });
  const refs = useEmployeeReferencesQuery();
  const me = useEmployeeQuery('me');
  const [collapsed, setCollapsed] = useState({});
  const [photos, setPhotos] = useState(true);
  const [locations, setLocations] = useState(false);
  const [departments, setDepartments] = useState(false);
  const [showUnlinked, setShowUnlinked] = useState(false);
  const [searchTerm, setSearchTerm] = useState('');
  const [selectedEmployee, setSelectedEmployee] = useState(null);
  const [focusedManagerId, setFocusedManagerId] = useState(null);
  const [zoom, setZoom] = useState(1);
  const [isPanning, setIsPanning] = useState(false);

  const scrollRef = useRef(null);
  const treeContainerRef = useRef(null);
  const isDraggingRef = useRef(false);
  const hasDraggedRef = useRef(false);
  const dragStartRef = useRef({ x: 0, y: 0, scrollLeft: 0, scrollTop: 0 });

  const items = query.data || [];
  const ids = new Set(items.map((e) => e.id));
  const children = new Map();
  const order = { ADMIN: 0, MANAGER: 1, DEVELOPER: 2, OTHER: 3 };

  const sorted = [...items].sort(
    (a, b) =>
      (order[a.roleLevel] ?? 3) - (order[b.roleLevel] ?? 3) ||
      `${a.firstName} ${a.lastName}`.localeCompare(`${b.firstName} ${b.lastName}`),
  );

  for (const e of sorted) {
    if (!children.has(e.managerEmployeeId)) children.set(e.managerEmployeeId, []);
    children.get(e.managerEmployeeId).push(e);
  }

  const itemMap = useMemo(() => {
    const map = new Map();
    items.forEach((e) => map.set(e.id, e));
    return map;
  }, [items]);

  const auth = useAuth();

  // Resolve logged-in employee record by me.data.id or auth session userId
  const myEmployee = useMemo(() => {
    if (me.data?.id) {
      const found = items.find((e) => String(e.id) === String(me.data.id));
      if (found) return found;
    }
    const sessionUserId = auth?.session?.user?.id;
    if (sessionUserId) {
      const found = items.find((e) => String(e.userId) === String(sessionUserId));
      if (found) return found;
    }
    if (me.data?.workEmail) {
      const found = items.find((e) => e.workEmail?.toLowerCase() === me.data.workEmail?.toLowerCase());
      if (found) return found;
    }
    if (auth?.session?.user?.email) {
      const found = items.find((e) => e.workEmail?.toLowerCase() === auth.session.user.email?.toLowerCase());
      if (found) return found;
    }
    return me.data || null;
  }, [me.data, auth?.session?.user?.id, auth?.session?.user?.email, items]);

  // Focus on any employee's hierarchy (manager or subordinate/developer)
  function focusEmployee(empId) {
    if (!empId) {
      setFocusedManagerId(null);
      return;
    }
    setFocusedManagerId(empId);
    // Expand only the focused manager so direct subordinates are shown.
    // Collapse all other managers so lower levels only show subordinate counts.
    setCollapsed(() => {
      const next = {};
      items.forEach((e) => {
        next[e.id] = (e.id !== empId);
      });
      return next;
    });
    setSelectedEmployee(null);
  }

  // Focus on logged-in user's level (where "I" am: show my manager, me and my peers)
  function focusMyLevel() {
    const myRecord = myEmployee;
    if (!myRecord) return;
    if (myRecord.managerEmployeeId) {
      focusEmployee(myRecord.managerEmployeeId);
    } else {
      focusEmployee(myRecord.id);
    }
  }

  // Automatically focus on logged-in user's level when opening the chart (e.g. Rebecca sees her manager and herself)
  const initialFocusDoneRef = useRef(false);
  useEffect(() => {
    if (items.length > 0 && myEmployee && !initialFocusDoneRef.current) {
      initialFocusDoneRef.current = true;
      if (myEmployee.managerEmployeeId) {
        focusEmployee(myEmployee.managerEmployeeId);
      } else {
        focusEmployee(myEmployee.id);
      }
    }
  }, [items.length, myEmployee]);

  // Compute reporting chain breadcrumbs from top root down to focusedManager
  const reportingChain = useMemo(() => {
    if (!focusedManagerId || !itemMap.has(focusedManagerId)) return [];
    const chain = [];
    let curr = itemMap.get(focusedManagerId);
    const seen = new Set();
    while (curr && !seen.has(curr.id)) {
      seen.add(curr.id);
      chain.unshift(curr);
      curr = curr.managerEmployeeId ? itemMap.get(curr.managerEmployeeId) : null;
    }
    return chain;
  }, [focusedManagerId, itemMap]);

  // Only employees with direct reports or linked in reporting lines belong in the hierarchy tree.
  // When focused on an employee, show that employee at the top and their reports/chain below.
  const roots = useMemo(() => {
    if (focusedManagerId) {
      const focusedEmployee = items.find((e) => e.id === focusedManagerId);
      return focusedEmployee ? [focusedEmployee] : [];
    }
    return sorted.filter((e) => {
      const isRoot = !ids.has(e.managerEmployeeId);
      if (!isRoot) return false;
      const hasReports = (children.get(e.id) || []).length > 0;
      return hasReports;
    });
  }, [sorted, items, focusedManagerId, ids, children]);

  const reached = new Set();
  function visit(e) {
    if (reached.has(e.id)) return;
    reached.add(e.id);
    for (const child of children.get(e.id) || []) visit(child);
  }
  roots.forEach(visit);
  const unlinked = sorted.filter((e) => !reached.has(e.id));
  const reference = (id) => refs.data?.find((r) => r.id === id)?.name;

  // Matching logic
  const matches = useMemo(() => {
    if (!searchTerm.trim()) return new Set();
    const q = searchTerm.toLowerCase();
    const matchedSet = new Set();
    items.forEach((e) => {
      const name = `${e.firstName} ${e.lastName}`.toLowerCase();
      const job = (e.careerTitle || reference(e.jobTitleId) || '').toLowerCase();
      const dept = (reference(e.departmentId) || '').toLowerCase();
      if (name.includes(q) || job.includes(q) || dept.includes(q)) {
        matchedSet.add(e.id);
      }
    });
    return matchedSet;
  }, [items, searchTerm, refs.data]);

  function toggleCollapse(id) {
    setCollapsed((prev) => {
      const current = (id in prev)
        ? Boolean(prev[id])
        : (focusedManagerId ? focusedManagerId !== id : !roots.some((r) => r.id === id));
      return { ...prev, [id]: !current };
    });
  }

  function collapseAll() {
    const next = {};
    items.forEach((e) => {
      if ((children.get(e.id) || []).length > 0) next[e.id] = true;
    });
    setCollapsed(next);
  }

  function expandAll() {
    setCollapsed({});
  }

  // Re-collapse lower-level managers whenever returning to the full chart (or on initial load)
  // so only direct reports are shown and sub-managers only show subordinate counts
  useEffect(() => {
    if (items.length > 0 && !focusedManagerId) {
      setCollapsed(() => {
        const next = {};
        items.forEach((e) => {
          const hasReports = (children.get(e.id) || []).length > 0;
          const isTopRoot = sorted.some((r) => r.id === e.id && !ids.has(r.managerEmployeeId));
          // If they are not the top root manager, keep them collapsed so only subordinate count shows
          next[e.id] = hasReports && !isTopRoot;
        });
        return next;
      });
    } else if (items.length > 0 && focusedManagerId) {
      // Ensure ONLY the focused manager is uncollapsed; all sub-managers show subordinate counts
      setCollapsed(() => {
        const next = {};
        items.forEach((e) => {
          next[e.id] = (e.id !== focusedManagerId);
        });
        return next;
      });
    }
  }, [items.length, focusedManagerId]);

  function fitToScreen() {
    if (!scrollRef.current || !treeContainerRef.current) return;
    const viewportWidth = scrollRef.current.clientWidth - 48;
    const viewportHeight = scrollRef.current.clientHeight - 48;
    const treeEl = treeContainerRef.current.querySelector('.hierarchy-tree');
    if (!treeEl || viewportWidth <= 0 || viewportHeight <= 0) return;

    // Measure unscaled width and height of the tree
    const naturalWidth = treeEl.scrollWidth || treeEl.offsetWidth;
    const naturalHeight = treeEl.scrollHeight || treeEl.offsetHeight;
    if (naturalWidth <= 0 || naturalHeight <= 0) return;

    const scaleX = viewportWidth / naturalWidth;
    const scaleY = viewportHeight / naturalHeight;
    const bestScale = Math.min(scaleX, scaleY, 1.0);
    const targetScale = Math.max(0.4, Number(bestScale.toFixed(2)));
    setZoom(targetScale);
  }

  // Auto-fit on initial load, roots changes, focused manager changes, and window resize
  useEffect(() => {
    if (roots.length > 0) {
      const timer = setTimeout(fitToScreen, 120);
      window.addEventListener('resize', fitToScreen);
      return () => {
        clearTimeout(timer);
        window.removeEventListener('resize', fitToScreen);
      };
    }
  }, [roots.length, focusedManagerId, collapsed, showUnlinked]);

  function zoomIn() {
    setZoom((z) => Math.min(1.4, Number((z + 0.1).toFixed(1))));
  }

  function zoomOut() {
    setZoom((z) => Math.max(0.3, Number((z - 0.1).toFixed(1))));
  }

  function resetZoom() {
    setZoom(1);
    if (scrollRef.current) {
      scrollRef.current.scrollLeft = Math.max(0, (scrollRef.current.scrollWidth - scrollRef.current.clientWidth) / 2);
      scrollRef.current.scrollTop = 0;
    }
  }

  const handleMouseDown = (e) => {
    if (e.button !== 0) return;
    if (e.target.closest('button, a, input, select, details, label')) return;

    isDraggingRef.current = true;
    hasDraggedRef.current = false;
    dragStartRef.current = {
      x: e.clientX,
      y: e.clientY,
      scrollLeft: scrollRef.current?.scrollLeft || 0,
      scrollTop: scrollRef.current?.scrollTop || 0,
    };
    setIsPanning(true);
  };

  const handleMouseMove = (e) => {
    if (!isDraggingRef.current || !scrollRef.current) return;
    const dx = e.clientX - dragStartRef.current.x;
    const dy = e.clientY - dragStartRef.current.y;
    if (Math.abs(dx) > 4 || Math.abs(dy) > 4) {
      hasDraggedRef.current = true;
    }
    scrollRef.current.scrollLeft = dragStartRef.current.scrollLeft - dx;
    scrollRef.current.scrollTop = dragStartRef.current.scrollTop - dy;
  };

  const handleMouseUp = () => {
    isDraggingRef.current = false;
    setIsPanning(false);
  };

  function card(e, standalone = false) {
    const team = standalone ? [] : children.get(e.id) || [];
    const isMe = String(e.id) === String(myEmployee?.id) || String(e.id) === String(me.data?.id);
    const isMatch = matches.has(e.id);
    const isRootNode = roots.some((r) => r.id === e.id);
    const isCollapsed = (e.id in collapsed)
      ? Boolean(collapsed[e.id])
      : (focusedManagerId ? focusedManagerId !== e.id : !isRootNode);
    const avatarStyle = getAvatarStyle(`${e.firstName} ${e.lastName}`);
    const deptName = reference(e.departmentId);
    const subCount = team.length;
    const previewReports = team.slice(0, 3);
    const showManagerPill = isRootNode && e.managerEmployeeId && itemMap.get(e.managerEmployeeId);

    return (
      <article
        className={`people-node modern-tree-node${isMe ? ' is-you' : ''}${isMatch ? ' is-match' : ''}`}
        onClick={() => {
          if (hasDraggedRef.current) return;
          // If this card is the root manager at top and has a manager, clicking navigates up to their manager
          if (isRootNode && e.managerEmployeeId) {
            focusEmployee(e.managerEmployeeId);
            return;
          }
          // If this card has subordinates and is not the root manager, clicking focuses on their hierarchy
          if (team.length > 0 && !isRootNode) {
            focusEmployee(e.id);
            return;
          }
          // Otherwise open the employee details dialog
          setSelectedEmployee(e);
        }}
      >
        {isMe && <span className="people-you-label">YOU</span>}

        <div className="people-photo-wrap">
          {photos && e.profilePhotoUrl ? (
            <EmployeePhoto className="people-photo" src={e.profilePhotoUrl} alt="" />
          ) : (
            <span
              className="people-photo initials"
              style={{
                backgroundColor: avatarStyle.bg,
                color: avatarStyle.text,
              }}
            >
              {e.firstName?.[0]}
              {e.lastName?.[0]}
            </span>
          )}
        </div>

        <Link
          to={`/employees/${e.id}`}
          className="people-name-link"
          onClick={(ev) => ev.stopPropagation()}
        >
          {e.firstName} {e.lastName}
        </Link>

        <span className="people-job">
          {e.careerTitle || reference(e.jobTitleId) || 'Employee'}
        </span>

        {departments && deptName && (
          <span className="people-dept-badge">{deptName}</span>
        )}

        {locations && reference(e.locationId) && (
          <span className="people-location">
            <svg width="11" height="11" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
              <circle cx="12" cy="10" r="3" />
            </svg>
            {reference(e.locationId)}
          </span>
        )}

        {showManagerPill && (
          <button
            type="button"
            className="people-manager-pill"
            title={`Manager: ${itemMap.get(e.managerEmployeeId).firstName} ${itemMap.get(e.managerEmployeeId).lastName}. Click to view their hierarchy`}
            onClick={(ev) => {
              ev.stopPropagation();
              focusEmployee(e.managerEmployeeId);
            }}
            style={{
              marginTop: '6px',
              display: 'inline-flex',
              alignItems: 'center',
              gap: '4px',
              padding: '3px 10px',
              background: '#f8fafc',
              border: '1px solid #cbd5e1',
              borderRadius: '999px',
              fontSize: '11px',
              color: '#334155',
              fontWeight: 600,
              cursor: 'pointer',
              maxWidth: '92%',
              transition: 'all 0.15s ease',
            }}
          >
            <span style={{ color: '#94a3b8', fontSize: '10px' }}>Mgr:</span>
            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
              {itemMap.get(e.managerEmployeeId).firstName} {itemMap.get(e.managerEmployeeId).lastName}
            </span>
            <span style={{ color: '#2563eb', fontWeight: 700, fontSize: '10px' }}>↑</span>
          </button>
        )}

        {team.length > 0 && (
          <div className="node-subordinates-footer">
            <button
              className={`subordinates-pill ${isCollapsed ? 'is-collapsed' : ''}`}
              type="button"
              aria-label={`${subCount} ${subCount === 1 ? 'Subordinate' : 'Subordinates'}. Click to view direct reports`}
              title={isRootNode ? (isCollapsed ? 'Click to expand subordinates' : 'Click to collapse subordinates') : `Click to view ${e.firstName} and direct reports`}
              onClick={(ev) => {
                ev.stopPropagation();
                if (isRootNode) {
                  // Toggle collapse of root manager's direct reports
                  toggleCollapse(e.id);
                } else {
                  // Focus exclusively on this manager and their reports
                  focusEmployee(e.id);
                }
              }}
            >
              <div className="subordinates-avatar-stack">
                {previewReports.map((r, idx) => {
                  const rStyle = getAvatarStyle(`${r.firstName} ${r.lastName}`);
                  return (
                    <span
                      key={r.id}
                      className="stack-avatar"
                      style={{
                        zIndex: 3 - idx,
                        marginLeft: idx > 0 ? '-8px' : 0,
                      }}
                    >
                      {r.profilePhotoUrl ? (
                        <EmployeePhoto src={r.profilePhotoUrl} alt="" className="stack-img" />
                      ) : (
                        <span
                          className="stack-initials"
                          style={{
                            background: rStyle.bg,
                            color: rStyle.text,
                          }}
                        >
                          {r.firstName?.[0]}
                        </span>
                      )}
                    </span>
                  );
                })}
                {team.length > 2 && (
                  <span
                    className="stack-avatar stack-icon"
                    style={{ zIndex: 0, marginLeft: '-8px' }}
                  >
                    <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" strokeWidth="2.5">
                      <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
                      <circle cx="12" cy="7" r="4" />
                    </svg>
                  </span>
                )}
              </div>
              <span className="subordinates-text">
                {subCount} {subCount === 1 ? 'Subordinate' : 'Subordinates'}
              </span>
            </button>
          </div>
        )}
      </article>
    );
  }

  function branch(e) {
    const team = children.get(e.id) || [];
    const isCollapsed = (e.id in collapsed)
      ? Boolean(collapsed[e.id])
      : (focusedManagerId ? focusedManagerId !== e.id : !roots.some((r) => r.id === e.id));

    // If direct reports are many (> 6), split into two clean rows so they don't stretch into a tiny wide single row
    const shouldSplitRows = team.length > 6;
    const mid = shouldSplitRows ? Math.ceil(team.length / 2) : team.length;
    const row1 = shouldSplitRows ? team.slice(0, mid) : team;
    const row2 = shouldSplitRows ? team.slice(mid) : [];

    return (
      <li key={e.id}>
        {card(e)}
        {team.length > 0 && !isCollapsed && (
          <div className="subordinates-rows-container">
            <ul className="subordinates-row-1" aria-label={`Direct reports of ${e.firstName} ${e.lastName}`}>
              {row1.map(branch)}
            </ul>
            {row2.length > 0 && (
              <ul className="subordinates-row-2" aria-label={`Additional reports of ${e.firstName} ${e.lastName}`}>
                {row2.map(branch)}
              </ul>
            )}
          </div>
        )}
      </li>
    );
  }

  return (
    <div className="employee-page reference-people-page hierarchy-flow-page">
      <PageHeader
        title={focusedManagerId ? `Reporting line · ${roots[0]?.firstName || ''} ${roots[0]?.lastName || ''}` : "Organization hierarchy"}
        description={focusedManagerId ? `Showing reporting hierarchy for ${roots[0]?.firstName || 'this member'}.` : "Follow each manager’s reporting line from top to bottom."}
      >
        <button
          onClick={focusMyLevel}
          title="Jump directly to my level in the organization chart"
          style={{
            background: '#fffbeb',
            color: '#b45309',
            borderColor: '#fde68a',
            fontWeight: 600,
          }}
        >
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" style={{ marginRight: 6 }}>
            <circle cx="12" cy="7" r="4" />
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
          </svg>
          My level
        </button>
        {focusedManagerId && (
          <button
            onClick={() => setFocusedManagerId(null)}
            style={{
              background: '#f1f5f9',
              color: '#334155',
              borderColor: '#cbd5e1',
              fontWeight: 600,
            }}
          >
            ← View full org chart
          </button>
        )}
        <button onClick={expandAll}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ marginRight: 6 }}>
            <polyline points="15 3 21 3 21 9" />
            <polyline points="9 21 3 21 3 15" />
            <line x1="21" y1="3" x2="14" y2="10" />
            <line x1="3" y1="21" x2="10" y2="14" />
          </svg>
          Expand all
        </button>
        <button onClick={collapseAll}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ marginRight: 6 }}>
            <polyline points="4 14 10 14 10 20" />
            <polyline points="20 10 14 10 14 4" />
            <line x1="14" y1="10" x2="21" y2="3" />
            <line x1="3" y1="21" x2="10" y2="14" />
          </svg>
          Collapse all
        </button>
        <button onClick={() => window.print()}>
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ marginRight: 6 }}>
            <polyline points="6 9 6 2 18 2 18 9" />
            <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
            <rect x="6" y="14" width="12" height="8" />
          </svg>
          Print / Export
        </button>
        <Link className="button" to="/profile">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" style={{ marginRight: 6 }}>
            <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
            <circle cx="12" cy="7" r="4" />
          </svg>
          My profile
        </Link>
      </PageHeader>

      <section className="people-canvas" aria-label="Organization hierarchy">
        {/* Toolbar */}
        <div className="hierarchy-toolbar">
          <div className="search-container">
            <svg className="search-icon" width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
              <circle cx="11" cy="11" r="8" />
              <path d="M21 21l-4.35-4.35" />
            </svg>
            <input
              type="search"
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              placeholder="Filter by name or job title…"
            />
            {searchTerm && (
              <button
                type="button"
                className="clear-search-btn"
                onClick={() => setSearchTerm('')}
                aria-label="Clear search"
              >
                ✕
              </button>
            )}
          </div>

          <div className="stats-pill">
            <span>
              <strong>{items.length}</strong> employees ·{' '}
              <strong>
                {items.filter((e) => (children.get(e.id) || []).length > 0).length}
              </strong>{' '}
              reporting managers
            </span>
            {focusedManagerId && (
              <button
                type="button"
                onClick={() => setFocusedManagerId(null)}
                style={{
                  background: '#fef3c7',
                  color: '#b45309',
                  border: '1px solid #fde68a',
                  borderRadius: '6px',
                  padding: '2px 8px',
                  fontSize: '11.5px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  marginLeft: '8px',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '4px',
                }}
                title="Click to view all managers in the organization"
              >
                <span>Viewing single team</span>
                <span style={{ fontSize: '13px' }}>✕</span>
              </button>
            )}
            {searchTerm && matches.size > 0 && (
              <span style={{ color: '#b45309', fontWeight: 600, marginLeft: 6 }}>
                ({matches.size} matching)
              </span>
            )}
          </div>

          {/* Zoom Controls */}
          <div className="zoom-controls" title="Zoom Canvas">
            <button type="button" onClick={zoomOut} title="Zoom Out" disabled={zoom <= 0.3}>
              −
            </button>
            <span className="zoom-level-text">{Math.round(zoom * 100)}%</span>
            <button type="button" onClick={zoomIn} title="Zoom In" disabled={zoom >= 1.4}>
              +
            </button>
            <button type="button" onClick={fitToScreen} title="Fit to Screen" style={{ width: 'auto', padding: '0 8px', fontSize: '12px' }}>
              Fit to screen
            </button>
            <button type="button" onClick={resetZoom} title="Reset Zoom (100%)">
              ⟲
            </button>
          </div>

          {/* Display Preferences Dropdown */}
          <details className="people-customize">
            <summary>
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
                <circle cx="12" cy="12" r="3" />
                <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" />
              </svg>
              Chart display ▾
            </summary>
            <div>
              <label>
                <input
                  type="checkbox"
                  checked={photos}
                  onChange={(e) => setPhotos(e.target.checked)}
                />{' '}
                Show photos
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={departments}
                  onChange={(e) => setDepartments(e.target.checked)}
                />{' '}
                Show department
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={locations}
                  onChange={(e) => setLocations(e.target.checked)}
                />{' '}
                Show location
              </label>
              <label>
                <input
                  type="checkbox"
                  checked={showUnlinked}
                  onChange={(e) => setShowUnlinked(e.target.checked)}
                />{' '}
                Show unassigned ({unlinked.length})
              </label>
            </div>
          </details>
        </div>

        {/* Reporting chain breadcrumb bar */}
        {focusedManagerId && reportingChain.length > 0 && (
          <nav
            aria-label="Reporting hierarchy chain"
            className="reporting-chain-nav"
            style={{
              display: 'flex',
              alignItems: 'center',
              flexWrap: 'wrap',
              gap: '6px',
              padding: '10px 16px',
              margin: '0 0 16px 0',
              background: 'var(--theme-surface-subtle, #f8fafc)',
              border: '1px solid var(--theme-border, #e2e8f0)',
              borderRadius: '10px',
              fontSize: '13px',
              color: 'var(--theme-muted, #475569)',
            }}
          >
            <button
              type="button"
              onClick={() => setFocusedManagerId(null)}
              style={{
                background: 'none',
                border: 'none',
                padding: '2px 6px',
                color: '#2563eb',
                fontWeight: 600,
                cursor: 'pointer',
                borderRadius: '4px',
              }}
              title="View full organization chart"
            >
              Org Chart
            </button>
            <span style={{ color: 'var(--theme-muted, #94a3b8)' }}>›</span>
            {reportingChain.map((person, idx) => {
              const isLast = idx === reportingChain.length - 1;
              return (
                <span key={person.id} style={{ display: 'inline-flex', alignItems: 'center', gap: '6px' }}>
                  {isLast ? (
                    <span style={{ fontWeight: 700, color: 'var(--theme-text, #0f172a)' }}>
                      {person.firstName} {person.lastName}
                    </span>
                  ) : (
                    <button
                      type="button"
                      onClick={() => {
                        // Click to view this manager's hierarchy
                        focusEmployee(person.id);
                      }}
                      style={{
                        background: 'none',
                        border: 'none',
                        padding: '2px 6px',
                        color: '#2563eb',
                        fontWeight: 600,
                        cursor: 'pointer',
                        borderRadius: '4px',
                      }}
                      title={`View ${person.firstName}'s manager and team`}
                    >
                      {person.firstName} {person.lastName}
                    </button>
                  )}
                  {!isLast && <span style={{ color: 'var(--theme-muted, #94a3b8)' }}>›</span>}
                </span>
              );
            })}
            {itemMap.get(focusedManagerId)?.managerEmployeeId && (
              <button
                type="button"
                className="button-go-manager"
                onClick={() => {
                  const curr = itemMap.get(focusedManagerId);
                  if (curr?.managerEmployeeId) {
                    focusEmployee(curr.managerEmployeeId);
                  }
                }}
                style={{
                  marginLeft: 'auto',
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '4px',
                  background: 'var(--theme-surface, #ffffff)',
                  border: '1px solid var(--theme-border, #cbd5e1)',
                  borderRadius: '6px',
                  padding: '3px 8px',
                  fontSize: '11.5px',
                  fontWeight: 600,
                  color: 'var(--theme-text, #334155)',
                  cursor: 'pointer',
                }}
                title="View higher-level manager"
              >
                <span>↑ Go to Manager</span>
              </button>
            )}
          </nav>
        )}

        {/* Selected employee popover modal */}
        {selectedEmployee && (
          <div
            style={{
              position: 'fixed',
              inset: 0,
              background: 'rgba(15, 23, 42, 0.6)',
              backdropFilter: 'blur(3px)',
              zIndex: 999,
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
            }}
            onClick={() => setSelectedEmployee(null)}
          >
            <div
              className="panel"
              style={{
                width: '360px',
                padding: '24px',
                borderRadius: '16px',
                background: 'var(--theme-surface, #ffffff)',
                boxShadow: '0 20px 40px rgba(0,0,0,0.3)',
                border: '1px solid var(--theme-border, #e2e8f0)',
              }}
              onClick={(ev) => ev.stopPropagation()}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div style={{ display: 'flex', gap: '14px', alignItems: 'center' }}>
                  {selectedEmployee.profilePhotoUrl ? (
                    <EmployeePhoto
                      src={selectedEmployee.profilePhotoUrl}
                      alt=""
                      style={{ width: '52px', height: '52px', borderRadius: '50%', objectFit: 'cover' }}
                    />
                  ) : (
                    <span
                      style={{
                        width: '52px',
                        height: '52px',
                        fontSize: '18px',
                        fontWeight: '700',
                        display: 'grid',
                        placeItems: 'center',
                        background: getAvatarStyle(`${selectedEmployee.firstName} ${selectedEmployee.lastName}`).bg,
                        color: getAvatarStyle(`${selectedEmployee.firstName} ${selectedEmployee.lastName}`).text,
                        borderRadius: '50%',
                      }}
                    >
                      {selectedEmployee.firstName?.[0]}
                      {selectedEmployee.lastName?.[0]}
                    </span>
                  )}
                  <div>
                    <h3 style={{ margin: 0, fontSize: '17px', color: 'var(--theme-text, #0f172a)', fontWeight: 700 }}>
                      {selectedEmployee.firstName} {selectedEmployee.lastName}
                    </h3>
                    <span style={{ fontSize: '13px', color: 'var(--theme-muted, #64748b)' }}>
                      {selectedEmployee.careerTitle || reference(selectedEmployee.jobTitleId) || 'Employee'}
                    </span>
                  </div>
                </div>
                <button
                  onClick={() => setSelectedEmployee(null)}
                  style={{
                    background: 'var(--theme-surface-subtle, #f1f5f9)',
                    border: '1px solid var(--theme-border, transparent)',
                    borderRadius: '50%',
                    width: '28px',
                    height: '28px',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    cursor: 'pointer',
                    color: 'var(--theme-muted, #64748b)',
                  }}
                  aria-label="Close dialog"
                >
                  ✕
                </button>
              </div>

              <div
                style={{
                  margin: '18px 0',
                  display: 'grid',
                  gap: '10px',
                  fontSize: '13.5px',
                  background: 'var(--theme-surface-subtle, #f8fafc)',
                  padding: '14px',
                  borderRadius: '10px',
                  border: '1px solid var(--theme-border, #e2e8f0)',
                }}
              >
                {reference(selectedEmployee.departmentId) && (
                  <div>
                    <strong style={{ color: 'var(--theme-muted, #475569)' }}>Department:</strong>{' '}
                    <span style={{ color: 'var(--theme-text, #0f172a)', fontWeight: 500 }}>
                      {reference(selectedEmployee.departmentId)}
                    </span>
                  </div>
                )}
                <div>
                  <strong style={{ color: 'var(--theme-muted, #475569)' }}>Role Level:</strong>{' '}
                  <span style={{ color: 'var(--theme-text, #0f172a)', fontWeight: 500 }}>
                    {selectedEmployee.roleLevel}
                  </span>
                </div>
                {reference(selectedEmployee.locationId) && (
                  <div>
                    <strong style={{ color: 'var(--theme-muted, #475569)' }}>Location:</strong>{' '}
                    <span style={{ color: 'var(--theme-text, #0f172a)', fontWeight: 500 }}>
                      {reference(selectedEmployee.locationId)}
                    </span>
                  </div>
                )}
                {selectedEmployee.email && (
                  <div>
                    <strong style={{ color: 'var(--theme-muted, #475569)' }}>Email:</strong>{' '}
                    <a
                      href={`mailto:${selectedEmployee.email}`}
                      style={{ color: '#60a5fa', textDecoration: 'none' }}
                    >
                      {selectedEmployee.email}
                    </a>
                  </div>
                )}
                <div>
                  <strong style={{ color: 'var(--theme-muted, #475569)' }}>Direct Reports:</strong>{' '}
                  <span style={{ color: 'var(--theme-text, #0f172a)', fontWeight: 600 }}>
                    {(children.get(selectedEmployee.id) || []).length}
                  </span>
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginTop: '16px', gap: '8px', flexWrap: 'wrap' }}>
                <div style={{ display: 'flex', gap: '6px' }}>
                  {(children.get(selectedEmployee.id) || []).length > 0 && (
                    <button
                      type="button"
                      className="button"
                      onClick={() => focusEmployee(selectedEmployee.id)}
                      title="Focus on this employee's reporting team"
                      style={{ fontSize: '12.5px', padding: '6px 10px' }}
                    >
                      Show their team →
                    </button>
                  )}
                  {selectedEmployee.managerEmployeeId && (
                    <button
                      type="button"
                      className="button"
                      onClick={() => focusEmployee(selectedEmployee.managerEmployeeId)}
                      title="View their manager and peers"
                      style={{ fontSize: '12.5px', padding: '6px 10px' }}
                    >
                      Show manager's level ↑
                    </button>
                  )}
                </div>

                <Link className="button primary" to={`/employees/${selectedEmployee.id}`}>
                  View full profile →
                </Link>
              </div>
            </div>
          </div>
        )}

        <QueryState query={query}>
          {items.length > 0 ? (
            <div
              className={`hierarchy-scroll${isPanning ? ' is-panning' : ''}`}
              role="region"
              aria-label="Full reporting tree"
              tabIndex={0}
              ref={scrollRef}
              onMouseDown={handleMouseDown}
              onMouseMove={handleMouseMove}
              onMouseUp={handleMouseUp}
              onMouseLeave={handleMouseUp}
            >
              <div
                ref={treeContainerRef}
                style={{
                  minWidth: '100%',
                  width: 'max-content',
                  display: 'flex',
                  justifyContent: 'center',
                  alignItems: 'flex-start',
                  margin: '0 auto',
                  padding: '16px 24px 48px',
                  boxSizing: 'border-box',
                }}
              >
                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'center',
                    transform: `scale(${zoom})`,
                    transformOrigin: 'top center',
                    transition: isPanning ? 'none' : 'transform 0.2s cubic-bezier(0.16, 1, 0.3, 1)',
                  }}
                >
                  <ul className="hierarchy-tree">{roots.map(branch)}</ul>
                </div>
              </div>

              {showUnlinked && unlinked.length > 0 && (
                <div className="hierarchy-unresolved">
                  <h2>Unassigned employees ({unlinked.length})</h2>
                  <p>
                    The following employees currently have no manager or direct reports assigned.
                  </p>
                  <ul className="hierarchy-tree">
                    {unlinked.map((e) => (
                      <li key={e.id}>{card(e, true)}</li>
                    ))}
                  </ul>
                </div>
              )}
            </div>
          ) : (
            <p>No employees found.</p>
          )}
        </QueryState>
      </section>
    </div>
  );
}
