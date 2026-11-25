import { request } from '../services/api/client.js';
let active = null;
const allowed = /^(theme|preferences|filters:[a-zA-Z0-9_-]+|issue-draft:[a-zA-Z0-9:_-]+)$/;
export function stopPreferenceSync() {
  if (active) {
    active.stopped = true;
    active.abort.abort();
  }
  active = null;
}
export async function hydratePreferences(user) {
  stopPreferenceSync();
  const state = {
    user,
    versions: new Map(),
    pending: new Map(),
    stopped: false,
    ready: false,
    abort: new AbortController(),
  };
  active = state;
  try {
    const items = await request('/api/preferences', {
      signal: AbortSignal.any([state.abort.signal, AbortSignal.timeout(5000)]),
    });
    if (state.stopped || !Array.isArray(items)) return;
    for (const item of items) {
      if (!allowed.test(item.key) || !Number.isInteger(item.version) || item.version <= 0) continue;
      const key = `GRIDHUB:${item.key}:${user.tenantId}:${user.id}`;
      state.versions.set(
        item.key,
        localStorage.getItem(`${key}:unsynced`)
          ? Number(localStorage.getItem(`${key}:version`) || 0)
          : item.version,
      );
      if (!localStorage.getItem(`${key}:unsynced`))
        localStorage.setItem(`${key}:version`, String(item.version));
      // Unsynced local changes win locally, but never overwrite remote values automatically.
      if (localStorage.getItem(`${key}:unsynced`)) continue;
      if (item.value == null) localStorage.removeItem(key);
      else
        localStorage.setItem(
          key,
          item.key === 'theme' ? String(item.value) : JSON.stringify(item.value),
        );
    }
    state.ready = true;
  } catch {
    /* Local preferences remain usable when offline. */
  }
}
export function syncPreference(key, value) {
  try {
    localStorage.setItem(`${key}:unsynced`, 'true');
  } catch {
    /* Optional cache. */
  }
  const state = active;
  const suffix = state && `:${state.user.tenantId}:${state.user.id}`;
  if (!state?.ready || state.stopped || !key.startsWith('GRIDHUB:') || !key.endsWith(suffix))
    return;
  const name = key.slice('GRIDHUB:'.length, -suffix.length);
  if (!allowed.test(name)) return;
  state.pending.set(name, value);
  if (!state.writing) void flush(state);
}
async function flush(state) {
  state.writing = true;
  while (!state.stopped && state.pending.size) {
    const [key, value] = state.pending.entries().next().value;
    state.pending.delete(key);
    try {
      const result = await request(`/api/preferences/${encodeURIComponent(key)}`, {
        method: 'PUT',
        body: { value, version: state.versions.get(key) || 0 },
        signal: state.abort.signal,
      });
      if (state.stopped) break;
      state.versions.set(key, result.version);
      localStorage.setItem(
        `GRIDHUB:${key}:${state.user.tenantId}:${state.user.id}:version`,
        String(result.version),
      );
      if (!state.pending.has(key))
        localStorage.removeItem(
          `GRIDHUB:${key}:${state.user.tenantId}:${state.user.id}:unsynced`,
        );
      window.dispatchEvent(
        new CustomEvent('preference-sync', { detail: 'Preferences saved to your account.' }),
      );
    } catch (error) {
      state.ready = false;
      state.pending.clear();
      if (!state.stopped)
        window.dispatchEvent(
          new CustomEvent('preference-sync', {
            detail:
              error.status === 412
                ? 'Preferences changed on another device. Local changes are preserved. Open Review account sync in Settings to compare and resolve changes.'
                : 'Preferences are saved on this browser. Account sync is unavailable.',
          }),
        );
      break;
    }
  }
  state.writing = false;
}

export async function reviewPreferences(user) {
  const items = await request('/api/preferences');
  const suffix = `:${user.tenantId}:${user.id}`;
  const records = new Map(
    items.filter((item) => allowed.test(item.key)).map((item) => [item.key, item]),
  );
  for (let i = 0; i < localStorage.length; i++) {
    const storageKey = localStorage.key(i);
    if (storageKey?.startsWith('GRIDHUB:') && storageKey.endsWith(suffix)) {
      const key = storageKey.slice('GRIDHUB:'.length, -suffix.length);
      if (allowed.test(key) && !records.has(key))
        records.set(key, { key, value: null, version: 0 });
    }
  }
  return [...records.values()].map((item) => {
    const raw = localStorage.getItem(`GRIDHUB:${item.key}${suffix}`);
    let local = raw;
    if (item.key !== 'theme') {
      try {
        local = JSON.parse(raw);
      } catch {
        local = null;
      }
    }
    return { ...item, local };
  });
}
export async function resolvePreference(user, item, useLocal) {
  const state = active;
  if (
    !state ||
    state.stopped ||
    String(state.user.id) !== String(user.id) ||
    String(state.user.tenantId) !== String(user.tenantId)
  )
    throw new Error('Sign in to sync preferences.');
  const record = useLocal
    ? await request(`/api/preferences/${encodeURIComponent(item.key)}`, {
        method: 'PUT',
        body: { value: item.local, version: item.version },
        signal: state.abort.signal,
      })
    : item;
  if (state.stopped) return;
  const key = `GRIDHUB:${item.key}:${user.tenantId}:${user.id}`;
  if (record.value == null) localStorage.removeItem(key);
  else
    localStorage.setItem(
      key,
      item.key === 'theme' ? String(record.value) : JSON.stringify(record.value),
    );
  localStorage.setItem(`${key}:version`, String(record.version));
  localStorage.removeItem(`${key}:unsynced`);
  state.versions.set(item.key, record.version);
  state.ready = true;
}
