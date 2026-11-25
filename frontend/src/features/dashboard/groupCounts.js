export function groupCounts(items, field) {
  const grouped = items.reduce((result, item) => {
    const name = item[field] || 'UNSPECIFIED';
    result[name] = (result[name] || 0) + 1;
    return result;
  }, {});
  return Object.entries(grouped).map(([name, count]) => ({ name, count }));
}
