import { ATHENNA } from './features';

// What each portal and optional feature is called and does, for the screens that talk about the company's plan.
export const portalCatalog = [
  { id: 'workspace', name: 'GridHub', to: '/dashboard', what: 'Projects, issues and sprints' },
  { id: 'employee', name: 'Persona', to: '/profile', what: 'Your profile, leave and career' },
  { id: 'timesheet', name: 'Chronos', to: '/timesheets/my', what: 'Timesheets and time approval' },
  { id: 'hub', name: 'AIWorkHub', to: '/home', what: 'Your central home, with the Athenna AI chat' },
];
export const featureCatalog = {
  [ATHENNA]: { name: 'Athenna AI chat', what: 'Ask questions, create issues and log work from chat. Comes with AIWorkHub.' },
};

/**
 * The company's plan as a list a person can read: every portal, and the optional tools, each marked included or not.
 * A tool that comes with a portal (the AI chat comes with AIWorkHub) is left out while that portal is missing, as on the Plan page:
 * the portal's own line says what it brings.
 */
export function planItems(data) {
  const portalIncluded = (id) => data.portals.find((x) => x.id === id)?.included ?? true;
  const portals = portalCatalog.map((p) => ({ ...p, included: portalIncluded(p.id) }));
  const tools = Object.entries(featureCatalog)
    .filter(([id]) => {
      const requires = data.features.find((x) => x.id === id)?.requires;
      return !requires || portalIncluded(requires);
    })
    .map(([id, f]) => ({ id, ...f, included: data.features.find((x) => x.id === id)?.included ?? true }));
  return { portals, tools };
}
