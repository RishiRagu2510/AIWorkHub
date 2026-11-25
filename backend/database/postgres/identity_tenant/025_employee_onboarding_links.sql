CREATE TABLE IF NOT EXISTS employee_onboarding_links (
 tenant_id bigint NOT NULL REFERENCES tenants(id), issue_id bigint NOT NULL,
 employee_id bigint NOT NULL, created_at timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
 PRIMARY KEY(tenant_id,issue_id), UNIQUE(tenant_id,employee_id),
 FOREIGN KEY(tenant_id,employee_id) REFERENCES employees(tenant_id,id)
);
