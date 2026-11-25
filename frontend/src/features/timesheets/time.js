// Display hours; preserve the API's integer-minute contract.
export function durationMinutes(text) {
  const value = String(text).trim();
  if (!/^(?:\d+(?:\.\d+)?|\.\d+)$/.test(value)) return null;
  const hours = Number(value);
  const minutes = Math.round(hours * 60);
  return Number.isFinite(hours) && hours > 0 && hours <= 24 && minutes >= 1 ? minutes : null;
}
export const durationLabel = (minutes) => String(Number((minutes / 60).toFixed(2)));
export const dateText = (date) =>
  `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
export function dateRange(date, mode = 'week') {
  const start = new Date(date + 'T12:00:00');
  const end = new Date(start);
  if (mode === 'week') {
    start.setDate(start.getDate() - ((start.getDay() + 6) % 7));
    end.setTime(start.getTime());
    end.setDate(end.getDate() + 6);
  }
  if (mode === 'month') {
    start.setDate(1);
    end.setMonth(end.getMonth() + 1, 0);
  }
  return { from: dateText(start), to: dateText(end) };
}
export function moveDate(date, mode, direction) {
  const d = new Date(date + 'T12:00:00');
  if (mode === 'month') {
    d.setDate(1);
    d.setMonth(d.getMonth() + direction);
  } else d.setDate(d.getDate() + direction * (mode === 'day' ? 1 : 7));
  return dateText(d);
}
