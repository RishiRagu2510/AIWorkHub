import AuditChanges from './AuditChanges';
import UserIdentity from '../../components/UserIdentity';
import { useEffect, useRef, useState } from 'react';
import { Badge, DataTable, JsonViewer } from '../../components/common';
import { Icon } from '../../components/Icon';
import { dateTime } from '../../utils/format';
function AuditInspector({ event, close }) {
  const ref = useRef(null);
  const [copied, setCopied] = useState('');
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement;
    dialog.showModal();
    return () => {
      dialog.close();
      previous?.focus();
    };
  }, []);
  async function copy() {
    try {
      await navigator.clipboard.writeText(JSON.stringify(event, null, 2));
      setCopied('JSON copied.');
    } catch {
      setCopied('Unable to copy. Select the JSON text to copy it manually.');
    }
  }
  return (
    <dialog
      ref={ref}
      className="audit-drawer"
      aria-labelledby="audit-inspector-title"
      onCancel={(e) => {
        e.preventDefault();
        close();
      }}
    >
      <header className="section-header">
        <div>
          <div className="eyebrow">Event #{event.id}</div>
          <h2 id="audit-inspector-title">Audit event inspector</h2>
        </div>
        <button className="icon-button" autoFocus aria-label="Close inspector" onClick={close}>
          <Icon name="close" />
        </button>
      </header>
      <div className="drawer-body">
        <div className="audit-inspector-banner">
          <Badge value={event.action} />
          <p className="event-id">Event ID: {event.eventId || '—'}</p>
        </div>
        <dl className="event-metadata">
          <div>
            <dt>Entity</dt>
            <dd>
              {event.entityType} #{event.entityId}
            </dd>
          </div>
          <div>
            <dt>Actor</dt>
            <dd>{event.actorUserId ? <UserIdentity id={event.actorUserId} /> : '—'}</dd>
          </div>
          <div>
            <dt>Source</dt>
            <dd>{event.source || '—'}</dd>
          </div>
          <div>
            <dt>Timestamp</dt>
            <dd>{event.occurredAt}</dd>
          </div>
        </dl>
        <AuditChanges oldValues={event.oldValues} newValues={event.newValues} />
        <div className="json-comparison">
          <section>
            <h3>Old values</h3>
            <JsonViewer value={event.oldValues} />
          </section>
          <section>
            <h3>New values</h3>
            <JsonViewer value={event.newValues} />
          </section>
        </div>
        <section>
          <h3>Metadata</h3>
          <JsonViewer value={event.metadata} />
        </section>
        <p role="status">{copied}</p>
      </div>
      <footer className="form-footer">
        <button onClick={copy}>Copy JSON</button>
        <button className="primary" onClick={close}>
          Close inspector
        </button>
      </footer>
    </dialog>
  );
}
export function AuditTable({ items }) {
  const [selected, setSelected] = useState(null);
  return (
    <>
      <DataTable
        rows={items}
        empty="No audit events found"
        columns={[
          {
            key: 'id',
            title: 'Event ID',
            render: (a) => <span className="mono audit-id-badge">#{a.id}</span>,
          },
          { key: 'action', title: 'Action', render: (a) => <Badge value={a.action} /> },
          {
            key: 'entityType',
            title: 'Entity',
            render: (a) => (
              <span className="audit-entity-val">
                <span className="audit-entity-type">{a.entityType || '—'}</span>{' '}
                <span className="audit-entity-id">{a.entityId ?? ''}</span>
              </span>
            ),
          },
          {
            key: 'actorUserId',
            title: 'Actor',
            render: (a) => (a.actorUserId ? <UserIdentity id={a.actorUserId} /> : '—'),
          },
          {
            key: 'source',
            title: 'Source',
            render: (a) => <span className="audit-source-pill">{a.source || '—'}</span>,
          },
          {
            key: 'occurredAt',
            title: 'Occurred at',
            render: (a) => <span className="audit-date-cell">{dateTime(a.occurredAt)}</span>,
          },
          {
            key: 'details',
            title: 'Inspect',
            render: (a) => (
              <button className="inspect-button" onClick={() => setSelected(a)}>
                View details
                <Icon name="arrow" />
              </button>
            ),
          },
        ]}
      />
      {selected && <AuditInspector event={selected} close={() => setSelected(null)} />}
    </>
  );
}
