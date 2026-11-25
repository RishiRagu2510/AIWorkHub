import { useState } from 'react';
import { useAuth } from '../auth/context';
import {
  useProjectCatalogQuery,
  useCreateCatalogMutation,
  useUpdateCatalogMutation,
} from '../../services/api/planning';
import { QueryState, ErrorState, Field } from '../../components/common';
function Catalog({ projectId, kind, title }) {
  const { can, session } = useAuth();
  const query = useProjectCatalogQuery({ projectId, kind });
  const [create, state] = useCreateCatalogMutation();
  const [update, editing] = useUpdateCatalogMutation();
  const [name, setName] = useState('');
  async function submit(e) {
    e.preventDefault();
    try {
      await create({ projectId, kind, body: { name } }).unwrap();
      setName('');
    } catch {
      /* Keep input. */
    }
  }
  return (
    <section className="panel collaboration-panel">
      <h2>{title}</h2>
      <QueryState query={query}>
        {query.data?.length ? (
          <ul>
            {query.data.map((x) => (
              <li key={`${x.id}:${x.version}`}>
                {session.user.role === 'ADMIN' && can('PROJECT_MANAGE') && (
                  <details>
                    <summary>Edit {x.name}</summary>
                    <form
                      onSubmit={async (e) => {
                        e.preventDefault();
                        const data = new FormData(e.currentTarget);
                        try {
                          await update({
                            projectId,
                            kind,
                            id: x.id,
                            version: x.version,
                            body: {
                              name: data.get('name'),
                              description: data.get('description'),
                              ...(kind === 'versions'
                                ? { status: data.get('status'), releaseDate: x.releaseDate }
                                : {}),
                            },
                          }).unwrap();
                        } catch {
                          /* Preserve values. */
                        }
                      }}
                    >
                      <Field
                        name="name"
                        label="Name"
                        required
                        maxLength={100}
                        defaultValue={x.name}
                      />
                      <Field
                        name="description"
                        label="Description"
                        maxLength={4000}
                        defaultValue={x.description || ''}
                      />
                      {kind === 'versions' && (
                        <label>
                          Status
                          <select name="status" defaultValue={x.status}>
                            {['UNRELEASED', 'RELEASED', 'ARCHIVED'].map((status) => (
                              <option key={status}>{status}</option>
                            ))}
                          </select>
                        </label>
                      )}
                      <button disabled={editing.isLoading}>Save</button>
                    </form>
                  </details>
                )}

                {x.name}
                {x.status ? ` · ${x.status}` : ''}
              </li>
            ))}
          </ul>
        ) : (
          <p>No {title.toLowerCase()} yet.</p>
        )}
      </QueryState>
      {session.user.role === 'ADMIN' && can('PROJECT_MANAGE') && (
        <form onSubmit={submit} className="actions">
          <Field
            label={`New ${kind === 'versions' ? 'version' : 'component'}`}
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={100}
            required
          />
          <button disabled={state.isLoading}>Add</button>
        </form>
      )}
      <ErrorState error={state.error || editing.error} />
    </section>
  );
}
export default function ProjectCatalog({ projectId }) {
  return (
    <div className="form-grid">
      <Catalog projectId={projectId} kind="components" title="Components" />
      <Catalog projectId={projectId} kind="versions" title="Versions" />
    </div>
  );
}
