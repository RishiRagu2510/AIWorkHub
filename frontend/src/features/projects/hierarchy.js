// Only order resources already returned to this user; never fetch hidden ancestors.
export function projectHierarchy(projects) {
  const ids = new Set(projects.map((project) => String(project.id)));
  const children = new Map();
  for (const project of projects) {
    const parent = project.parentProjectId == null ? null : String(project.parentProjectId);
    const group = parent && ids.has(parent) ? parent : null;
    if (!children.has(group)) children.set(group, []);
    children.get(group).push(project);
  }
  const rows = [];
  const visited = new Set();
  function visit(project, depth) {
    const id = String(project.id);
    if (visited.has(id)) return;
    visited.add(id);
    rows.push({ ...project, hierarchyDepth: depth });
    for (const child of children.get(id) || []) visit(child, depth + 1);
  }
  for (const root of children.get(null) || []) visit(root, 0);
  // Defensively retain records even if the response contains a cycle.
  for (const project of projects) visit(project, 0);
  return rows;
}

export function visibleHierarchy(projects, collapsed) {
  let hiddenBelow = null;
  return projectHierarchy(projects).filter((project) => {
    if (hiddenBelow != null && project.hierarchyDepth > hiddenBelow) return false;
    hiddenBelow = collapsed.has(String(project.id)) ? project.hierarchyDepth : null;
    return true;
  });
}
