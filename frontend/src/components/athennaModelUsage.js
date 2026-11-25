// How many messages this user sent with each model provider, kept on this device only. The providers do not tell
// the browser their real quota, so "percent left" is measured against the allowance below (a 5 hour and a weekly
// allowance per provider). Set VITE_ATHENNA_USAGE_LIMITS, e.g. {"Google":{"fiveHour":80,"week":800}}, to change it.

const WINDOW_DAYS = 7;
const FIVE_HOURS = 5 * 60 * 60 * 1000;
const WEEK = WINDOW_DAYS * 24 * 60 * 60 * 1000;
const MAX_ENTRIES = 2000;

const key = (userId) => `athenna_model_usage_${userId ?? 'anonymous'}`;

function defaultStorage() {
  try {
    if (typeof window !== 'undefined' && window.localStorage && typeof window.localStorage.getItem === 'function') {
      return window.localStorage;
    }
  } catch {
    // Blocked storage just means no usage is shown.
  }
  return null;
}

function read(userId, storage) {
  try {
    const parsed = JSON.parse(storage?.getItem(key(userId)) || '[]');
    return Array.isArray(parsed) ? parsed.filter((e) => e && typeof e.t === 'number' && typeof e.p === 'string') : [];
  } catch {
    return [];
  }
}

/** Notes that one message was sent with a provider's model. Older than a week is dropped. */
export function recordModelUsage(userId, provider, now = Date.now(), storage = defaultStorage()) {
  if (!storage || !provider) return;
  const kept = read(userId, storage).filter((e) => now - e.t <= WEEK);
  kept.push({ t: now, p: provider });
  try {
    storage.setItem(key(userId), JSON.stringify(kept.slice(-MAX_ENTRIES)));
  } catch {
    // Storage full: usage is a nicety, so it is simply not recorded.
  }
}

/** { Google: { last5h, week }, OpenAI: { last5h, week } } for the providers that have been used. */
export function summarizeModelUsage(userId, now = Date.now(), storage = defaultStorage()) {
  const totals = {};
  read(userId, storage).forEach((e) => {
    const age = now - e.t;
    if (age < 0 || age > WEEK) return;
    const row = (totals[e.p] ||= { last5h: 0, week: 0 });
    row.week += 1;
    if (age <= FIVE_HOURS) row.last5h += 1;
  });
  return totals;
}

export const DEFAULT_LIMITS = {
  Google: { fiveHour: 60, week: 600 },
  OpenAI: { fiveHour: 40, week: 400 },
  Groq: { fiveHour: 60, week: 600 },
};

function configuredLimits() {
  try {
    const raw = import.meta.env?.VITE_ATHENNA_USAGE_LIMITS;
    const parsed = raw ? JSON.parse(raw) : {};
    const merged = { ...DEFAULT_LIMITS };
    Object.keys(parsed || {}).forEach((provider) => {
      const { fiveHour, week } = parsed[provider] || {};
      if (fiveHour > 0 && week > 0) merged[provider] = { fiveHour, week };
    });
    return merged;
  } catch {
    return DEFAULT_LIMITS;
  }
}

function windowStatus(times, now, span, limit) {
  const inside = times.filter((t) => now - t >= 0 && now - t <= span);
  const used = inside.length;
  const left = Math.max(0, Math.min(100, Math.round(((limit - used) / limit) * 100)));
  // The window slides, so capacity starts coming back when the oldest message in it ages out.
  const resetsInMs = inside.length ? Math.max(0, Math.min(...inside) + span - now) : null;
  return { used, limit, left, resetsInMs };
}

/** { Google: { fiveHour: { used, limit, left, resetsInMs }, week: {...} }, OpenAI: {...} } for every provider listed. */
export function usageStatus(userId, now = Date.now(), storage = defaultStorage(), limits = configuredLimits()) {
  const entries = read(userId, storage);
  const status = {};
  Object.keys(limits).forEach((provider) => {
    const times = entries.filter((e) => e.p === provider).map((e) => e.t);
    status[provider] = {
      fiveHour: windowStatus(times, now, FIVE_HOURS, limits[provider].fiveHour),
      week: windowStatus(times, now, WEEK, limits[provider].week),
    };
  });
  return status;
}

/** "2d 20h", "1h 49m", "12m" for the time until a window frees up. */
export function formatReset(ms) {
  const minutes = Math.max(1, Math.ceil(ms / 60000));
  const days = Math.floor(minutes / 1440);
  const hours = Math.floor((minutes % 1440) / 60);
  if (days) return `${days}d ${hours}h`;
  if (hours) return `${hours}h ${minutes % 60}m`;
  return `${minutes}m`;
}
