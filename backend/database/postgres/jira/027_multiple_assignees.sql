CREATE TABLE IF NOT EXISTS issue_assignees (
    tenant_id BIGINT NOT NULL,
    project_id BIGINT NOT NULL,
    issue_id BIGINT NOT NULL,
    user_id BIGINT NOT NULL,
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT pk_issue_assignees PRIMARY KEY (issue_id, user_id),
    CONSTRAINT fk_issue_assignees_issue FOREIGN KEY (issue_id) REFERENCES issues(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_issue_assignees_tenant_user ON issue_assignees(tenant_id, user_id);


INSERT INTO issue_assignees (tenant_id, project_id, issue_id, user_id, assigned_at)
SELECT tenant_id, project_id, id, assignee_user_id, updated_at
FROM issues
WHERE assignee_user_id IS NOT NULL AND assignee_user_id > 0
ON CONFLICT (issue_id, user_id) DO NOTHING;
