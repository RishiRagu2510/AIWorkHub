CREATE TABLE IF NOT EXISTS employee_onboarding_requests (
 issue_id bigint PRIMARY KEY, tenant_id bigint NOT NULL,
 FOREIGN KEY(tenant_id,issue_id) REFERENCES issues(tenant_id,id) ON DELETE CASCADE,
 data jsonb NOT NULL, status text NOT NULL DEFAULT 'SUBMITTED' CHECK(status IN ('SUBMITTED','APPROVED','REJECTED','COMPLETED')),
 reviewed_by bigint, reviewed_at timestamptz, review_note text, employee_id bigint,
 revision integer NOT NULL DEFAULT 1,
 CHECK((status='COMPLETED')=(employee_id IS NOT NULL))
);
CREATE INDEX IF NOT EXISTS ix_employee_onboarding_tenant_status ON employee_onboarding_requests(tenant_id,status);
