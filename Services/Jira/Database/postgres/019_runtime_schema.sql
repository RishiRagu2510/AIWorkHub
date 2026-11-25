
BEGIN;
ALTER TABLE issues ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT false;
ALTER TABLE issues ADD COLUMN IF NOT EXISTS deleted_at timestamptz;
ALTER TABLE issues ADD COLUMN IF NOT EXISTS deleted_by_user_id bigint;
CREATE INDEX IF NOT EXISTS ix_issues_tenant_deleted ON issues(tenant_id, is_deleted);

CREATE TABLE IF NOT EXISTS outbox_messages (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
 event_id uuid NOT NULL,
 event_type varchar(100) NOT NULL,
 topic varchar(200) NOT NULL,
 payload jsonb NOT NULL,
 occurred_at timestamptz NOT NULL,
 processed_at timestamptz,
 attempt_count integer NOT NULL DEFAULT 0,
 last_error text,
 next_attempt_at timestamptz,
 locked_until timestamptz,
 locked_by varchar(100)
);
ALTER TABLE outbox_messages ADD COLUMN IF NOT EXISTS next_attempt_at timestamptz;
ALTER TABLE outbox_messages ADD COLUMN IF NOT EXISTS locked_until timestamptz;
ALTER TABLE outbox_messages ADD COLUMN IF NOT EXISTS locked_by varchar(100);
CREATE UNIQUE INDEX IF NOT EXISTS uq_outbox_messages_event_id ON outbox_messages(event_id);
CREATE INDEX IF NOT EXISTS idx_outbox_messages_pending_claim
 ON outbox_messages(processed_at, next_attempt_at, locked_until, id);
CREATE INDEX IF NOT EXISTS idx_outbox_messages_cleanup
 ON outbox_messages(processed_at, id) WHERE processed_at IS NOT NULL;

ALTER TABLE project_members DROP CONSTRAINT IF EXISTS ck_project_members_role;
ALTER TABLE project_members ADD CONSTRAINT ck_project_members_role
 CHECK (role IN ('ADMIN', 'LEAD', 'MEMBER', 'VIEWER', 'DEVELOPER'));
COMMIT;
