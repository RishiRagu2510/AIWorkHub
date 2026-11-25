// Date-only calendar arithmetic; promotion dates belong to the new position.
export function todayDate() {
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}
function parse(value) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value || '')) return null;
  const d = new Date(`${value}T00:00:00Z`);
  return Number.isFinite(d.valueOf()) && d.toISOString().slice(0, 10) === value ? d : null;
}
function addMonths(date, months) {
  const target = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + months, 1));
  const last = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate();
  target.setUTCDate(Math.min(date.getUTCDate(), last));
  return target;
}
export function periodLength(start, end) {
  const a = parse(start), b = parse(end);
  if (!a || !b || b < a) return 'Not recorded';
  let months = (b.getUTCFullYear() - a.getUTCFullYear()) * 12 + b.getUTCMonth() - a.getUTCMonth();
  if (addMonths(a, months) > b) months--;
  const days = Math.round((b - addMonths(a, months)) / 86400000);
  return `${Math.floor(months / 12)} year(s), ${months % 12} month(s), ${days} day(s)`;
}
export function careerPeriods(entries, employee, today = todayDate()) {
  const until = employee.endDate && employee.endDate < today ? employee.endDate : today;
  const ordered = [...(entries || [])].sort((a, b) => b.id - a.id);
  return ordered.map((entry, index) => {
    const next = index > 0 ? ordered[index - 1].effectiveDate : null;
    const end = next && next < until ? next : until;
    return { ...entry, periodEnd: end, current: index === 0 && (!employee.endDate || employee.endDate > today),
      duration: periodLength(entry.effectiveDate, end) };
  });
}
export function careerTenure(entries, employee, today = todayDate()) {
  const until = employee.endDate && employee.endDate < today ? employee.endDate : today;
  const rows = [...(entries || [])].sort((a, b) => b.id - a.id);
  function since(keys) {
    if (!rows.length) return employee.joiningDate ? periodLength(employee.joiningDate, until) : 'Not recorded';
    let oldest = rows[0];
    for (const row of rows.slice(1)) {
      if (keys.some(key => (row.details?.[key] || '') !== (rows[0].details?.[key] || ''))) break;
      oldest = row;
    }
    const startDate = oldest.effectiveDate || employee.joiningDate;
    return startDate ? periodLength(startDate, until) : 'Not recorded';
  }
  return { service: periodLength(employee.joiningDate, until),
    position: since(['grade', 'designation', 'jobTitle']) };
}
