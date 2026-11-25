function snapshot(value) {
  if (value == null || value === '') return {};
  try {
    const parsed = typeof value === 'string' ? JSON.parse(value) : value;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : null;
  } catch {
    return null;
  }
}
const labels = {
  name: 'Name',
  key: 'Project',
  accountid: 'Account ID',
  accountname: 'Account',
  projectname: 'Project',
  description: 'Description',
  status: 'Status',
  projecttype: 'Project type',
  parentprojectid: 'Parent project ID',
  projectleaduserid: 'Project lead user ID',
  userid: 'Member user ID',
  role: 'Membership role',
};
const fieldLabel = (key) =>
  labels[key.toLowerCase()] ||
  key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/^./, (c) => c.toUpperCase());
function display(value) {
  if (value === undefined) return 'Not recorded';
  if (value === null) return 'None';
  if (value === '') return '(empty)';
  return typeof value === 'object' ? JSON.stringify(value) : String(value);
}
export default function AuditChanges({ oldValues, newValues }) {
  const before = snapshot(oldValues);
  const after = snapshot(newValues);
  if (!before || !after)
    return <p>Change summary unavailable. Inspect the recorded values below.</p>;
  const keys = [...new Set([...Object.keys(before), ...Object.keys(after)])].filter(
    (key) =>
      key.toLowerCase() !== 'version' && JSON.stringify(before[key]) !== JSON.stringify(after[key]),
  );
  return (
    <section className="audit-changes" aria-label="Change summary">
      <h3>Changes</h3>
      {keys.length ? (
        <dl>
          {keys.map((key) => (
            <div key={key}>
              <dt>{fieldLabel(key)}</dt>
              <dd>
                <span>
                  <small>Before</small>
                  {display(before[key])}
                </span>
                <span>
                  <small>After</small>
                  {display(after[key])}
                </span>
              </dd>
            </div>
          ))}
        </dl>
      ) : (
        <p>No field changes recorded.</p>
      )}
    </section>
  );
}
