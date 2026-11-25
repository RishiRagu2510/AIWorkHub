import { useState } from 'react';
import { useProjectCatalogQuery } from '../../services/api/planning';
import { Field, ErrorState } from '../../components/common';
function VersionChoice({ name, title, options, initial }) {
  const [ids, setIds] = useState(
    Array.isArray(initial)
      ? initial.map(String)
      : String(initial || '')
          .split(',')
          .filter(Boolean),
  );
  return (
    <Field label={title}>
      <select
        multiple
        aria-label={title}
        value={ids}
        onChange={(e) => setIds(Array.from(e.target.selectedOptions, (o) => o.value))}
      >
        {options.map((v) => (
          <option key={v.id} value={v.id}>
            {v.name}
          </option>
        ))}
      </select>
      <input type="hidden" name={name} value={ids.join(',')} />
      <small>Choose versions; clear the selection to remove them.</small>
    </Field>
  );
}
export default function IssuePlanningFields({ projectId, initial = {} }) {
  const versions = useProjectCatalogQuery({ projectId, kind: 'versions' }, { skip: !projectId });
  return (
    <fieldset className="planning-fields">
      <legend>Labels, versions & estimates</legend>
      <ErrorState error={versions.error} retry={versions.refetch} />
      <div className="form-grid">
        <Field
          label="Labels (comma separated)"
          name="labels"
          defaultValue={
            Array.isArray(initial.labels) ? initial.labels.join(', ') : initial.labels || ''
          }
          maxLength={3000}
        />
        <Field
          label="Original estimate (minutes)"
          name="originalEstimateMinutes"
          type="number"
          min="0"
          max="2147483647"
          step="1"
          defaultValue={initial.originalEstimateMinutes ?? 0}
        />
        <Field
          label="Remaining estimate (minutes)"
          name="remainingEstimateMinutes"
          type="number"
          min="0"
          max="2147483647"
          step="1"
          defaultValue={initial.remainingEstimateMinutes ?? 0}
        />
        {versions.data && (
          <>
            <VersionChoice
              name="fixVersionIds"
              title="Fix versions"
              options={versions.data}
              initial={initial.fixVersionIds}
            />
            <VersionChoice
              name="affectsVersionIds"
              title="Affects versions"
              options={versions.data}
              initial={initial.affectsVersionIds}
            />
          </>
        )}
      </div>
    </fieldset>
  );
}
