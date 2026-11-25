CREATE TABLE IF NOT EXISTS issue_file_objects (
 id uuid PRIMARY KEY, tenant_id bigint NOT NULL, issue_id bigint NOT NULL,
 author_user_id bigint NOT NULL, name varchar(255) NOT NULL, size bigint NOT NULL CHECK (size > 0 AND size <= 10485760), created_at timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_issue_file_objects_scope ON issue_file_objects(tenant_id, issue_id);
CREATE TABLE IF NOT EXISTS user_workspace_preferences (
 tenant_id bigint NOT NULL, user_id bigint NOT NULL, key varchar(160) NOT NULL, value jsonb NOT NULL,
 PRIMARY KEY(tenant_id, user_id, key)
);
