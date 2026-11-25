
CREATE TABLE IF NOT EXISTS notifications (
    id uuid PRIMARY KEY,
    event_id uuid NOT NULL,
    tenant_id bigint NOT NULL,
    recipient_user_id bigint NOT NULL,
    actor_user_id bigint NOT NULL,
    issue_id bigint NOT NULL,
    kind varchar(40) NOT NULL,
    created_at timestamptz NOT NULL,
    read_at timestamptz NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_notifications_event_recipient ON notifications (tenant_id, event_id, recipient_user_id);
CREATE INDEX IF NOT EXISTS ix_notifications_inbox ON notifications (tenant_id, recipient_user_id, created_at);
