BEGIN;
CREATE TABLE IF NOT EXISTS timesheet_periods (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, tenant_id bigint NOT NULL, user_id bigint NOT NULL,
 period_start date NOT NULL, period_end date NOT NULL,
 status varchar(20) NOT NULL DEFAULT 'DRAFT' CHECK(status IN ('DRAFT','SUBMITTED','APPROVED','REJECTED')),
 submitted_at timestamptz, approver_user_id bigint, approved_by_user_id bigint, approved_at timestamptz,
 rejection_reason varchar(2000), created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 UNIQUE(tenant_id,user_id,period_start), UNIQUE(tenant_id,user_id,id),
 CHECK(period_end=period_start+6 AND extract(isodow FROM period_start)=1)
);
CREATE INDEX IF NOT EXISTS ix_timesheet_periods_review ON timesheet_periods(tenant_id,status,period_start);
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conrelid='issues'::regclass AND conname='uq_issues_worklog_scope') THEN
 ALTER TABLE issues ADD CONSTRAINT uq_issues_worklog_scope UNIQUE(tenant_id,project_id,id);
 END IF;
END $$;
CREATE TABLE IF NOT EXISTS work_logs (
 id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY, tenant_id bigint NOT NULL, user_id bigint NOT NULL,
 issue_id bigint NOT NULL, project_id bigint NOT NULL, period_id bigint NOT NULL, work_date date NOT NULL,
 duration_minutes integer NOT NULL CHECK(duration_minutes BETWEEN 1 AND 1440), description varchar(4000), billable boolean NOT NULL DEFAULT false,
 created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 FOREIGN KEY(tenant_id,project_id,issue_id) REFERENCES issues(tenant_id,project_id,id) ON DELETE RESTRICT,
 FOREIGN KEY(tenant_id,project_id) REFERENCES projects(tenant_id,id) ON DELETE RESTRICT,
 FOREIGN KEY(tenant_id,user_id,period_id) REFERENCES timesheet_periods(tenant_id,user_id,id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_work_logs_user_date ON work_logs(tenant_id,user_id,work_date);
CREATE INDEX IF NOT EXISTS ix_work_logs_project_date ON work_logs(tenant_id,project_id,work_date);
CREATE INDEX IF NOT EXISTS ix_work_logs_issue ON work_logs(tenant_id,issue_id);
CREATE INDEX IF NOT EXISTS ix_work_logs_period ON work_logs(period_id);
ALTER TABLE notifications ADD COLUMN IF NOT EXISTS timesheet_period_id bigint REFERENCES timesheet_periods(id) ON DELETE RESTRICT;
ALTER TABLE notifications ADD COLUMN IF NOT EXISTS reason varchar(2000);
ALTER TABLE request_replays ADD COLUMN IF NOT EXISTS resource_type text;
ALTER TABLE request_replays ADD COLUMN IF NOT EXISTS resource_id bigint;
COMMIT;
