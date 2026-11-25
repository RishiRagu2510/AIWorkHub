// JWT claim names are a backend protocol contract, independent of display branding.
export function sessionFromToken(token, user, now = Date.now()) {
  try {
    const part = token.split('.')[1];
    const bytes = Uint8Array.from(atob(part.replace(/-/g, '+').replace(/_/g, '/')), (c) =>
      c.charCodeAt(0),
    );
    const claims = JSON.parse(new TextDecoder().decode(bytes));
    if (
      !Number.isFinite(claims.exp) ||
      claims.exp * 1000 <= now ||
      !claims['aiworkhub:tenant_id'] ||
      !claims['aiworkhub:user_id']
    )
      throw new Error();
    if (
      String(user.id) !== String(claims['aiworkhub:user_id']) ||
      String(user.tenantId) !== String(claims['aiworkhub:tenant_id'])
    )
      throw new Error();
    const value = claims['aiworkhub:permission'];
    const rawPortals = claims['aiworkhub:portal'] || claims['aiworkhub:portals'] || user?.allowedPortals;
    const allowedPortals = Array.isArray(rawPortals)
      ? rawPortals
      : typeof rawPortals === 'string'
      ? rawPortals.split(',').map((p) => p.trim())
      : null; // null means no portal restriction configured (allow all by default)
    const rawFeatures = claims['aiworkhub:feature'] || claims['aiworkhub:features'] || user?.allowedFeatures;
    const allowedFeatures = Array.isArray(rawFeatures)
      ? rawFeatures
      : typeof rawFeatures === 'string'
      ? rawFeatures.split(',').map((f) => f.trim())
      : null; // null means no plan restriction configured (every feature allowed)
    return {
      user: {
        ...user,
        allowedPortals,
        allowedFeatures,
      },
      expiresAt: claims.exp * 1000,
      permissions: Array.isArray(value) ? value : typeof value === 'string' ? [value] : [],
      allowedPortals,
      allowedFeatures,
    };
  } catch {
    throw new Error('Your session could not be verified. Please sign in again.');
  }
}
// Permissions that exist only for one portal (mirrors PortalLicense.PermissionOwners on the server).
// A company without that portal does not get them, so every `can(...)` check in every portal hides the
// feature without each screen having to ask about the licence as well.
const permissionOwners = [['TIMESHEET_', 'timesheet']];

export const owningPortal = (permission) =>
  permissionOwners.find(([prefix]) => String(permission).toUpperCase().startsWith(prefix))?.[1] ??
  null;

export const hasPermission = (session, permission) => {
  if (!session?.permissions?.includes(permission)) return false;
  const owner = owningPortal(permission);
  return owner === null || hasPortalAccess(session, owner);
};

export const hasPortalAccess = (session, portal) => {
  const allowed = session?.allowedPortals || session?.user?.allowedPortals;
  if (!allowed || !Array.isArray(allowed) || allowed.length === 0) {
    return true; // No portal restriction set; default to allow
  }
  // Normalize portal names: 'workspace' / 'gridhub', 'employee' / 'persona', 'timesheet' / 'chronos', 'hub' / 'aiworkhub'
  const aliases = {
    workspace: ['workspace', 'gridhub'],
    gridhub: ['workspace', 'gridhub'],
    employee: ['employee', 'persona'],
    persona: ['employee', 'persona'],
    timesheet: ['timesheet', 'chronos'],
    chronos: ['timesheet', 'chronos'],
    hub: ['hub', 'aiworkhub'],
    aiworkhub: ['hub', 'aiworkhub'],
  }[portal.toLowerCase()] || [portal.toLowerCase()];

  return allowed.some((p) => aliases.includes(String(p).toLowerCase()));
};

// Some features come with a portal (mirrors FeatureLicense.RequiredPortal on the server): the Athenna AI chat lives in AIWorkHub,
// so a company without AIWorkHub does not have it, whatever its feature list says.
export const featurePortal = (feature) => ({ athenna: 'hub' }[String(feature).trim().toLowerCase()] ?? null);

// Optional features are listed separately from portals (mirrors FeatureLicense on the server).
// No setting means every feature is allowed; the single entry "none" means no optional feature.
// A feature that comes with a portal also needs that portal.
export const hasFeatureAccess = (session, feature) => {
  const portal = featurePortal(feature);
  if (portal && !hasPortalAccess(session, portal)) return false;
  const allowed = session?.allowedFeatures || session?.user?.allowedFeatures;
  if (!Array.isArray(allowed)) return true;
  const listed = allowed.map((f) => String(f).trim().toLowerCase()).filter(Boolean);
  if (listed.length === 0) return true;
  return listed.includes(String(feature).trim().toLowerCase());
};
