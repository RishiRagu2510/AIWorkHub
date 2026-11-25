BEGIN;
ALTER TABLE audit_logs ADD COLUMN IF NOT EXISTS event_id uuid;
CREATE UNIQUE INDEX IF NOT EXISTS ux_audit_logs_event_id
 ON audit_logs(event_id) WHERE event_id IS NOT NULL;
COMMIT;
