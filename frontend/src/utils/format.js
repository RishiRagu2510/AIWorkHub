export const label = (value) =>
  value === 'TODO'
    ? 'To Do'
    : value
      ? String(value)
          .toLowerCase()
          .replaceAll('_', ' ')
          .replace(/\b\w/g, (c) => c.toUpperCase())
      : '—';
export const dateTime = (value) =>
  value === 'TODO'
    ? 'To Do'
    : value
      ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(
          new Date(value),
        )
      : '—';
export const userLabel = (id) => (id == null ? 'Unassigned' : `User #${id}`);
export function formatDisplayKey(key) {
  if (!key) return 'ISSUE';
  if (key.includes(' ') || key.length > 20) {
    const match = key.match(/^([A-Z0-9]+)\b.*-(\d+)$/i);
    if (match) return `${match[1]}-${match[2]}`.toUpperCase();
  }
  return key;
}
export function formValues(form) {
  return Object.fromEntries(new FormData(form));
}
