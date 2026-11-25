BEGIN;
ALTER TABLE work_logs ADD COLUMN IF NOT EXISTS start_time time;
ALTER TABLE work_logs ADD COLUMN IF NOT EXISTS end_time time;
ALTER TABLE work_logs DROP CONSTRAINT IF EXISTS ck_work_log_times;
ALTER TABLE work_logs ADD CONSTRAINT ck_work_log_times CHECK ((start_time IS NULL AND end_time IS NULL) OR (start_time IS NOT NULL AND end_time IS NOT NULL AND end_time > start_time AND extract(second FROM start_time)=0 AND extract(second FROM end_time)=0 AND duration_minutes=extract(epoch FROM (end_time-start_time))/60));
ALTER TABLE issues ADD COLUMN IF NOT EXISTS original_estimate_minutes integer NOT NULL DEFAULT 0 CHECK(original_estimate_minutes>=0);
ALTER TABLE issues ADD COLUMN IF NOT EXISTS remaining_estimate_minutes integer NOT NULL DEFAULT 0 CHECK(remaining_estimate_minutes>=0);
ALTER TABLE issues ADD COLUMN IF NOT EXISTS logged_time_minutes bigint NOT NULL DEFAULT 0 CHECK(logged_time_minutes>=0);
ALTER TABLE issues ADD COLUMN IF NOT EXISTS backlog_order bigint NOT NULL DEFAULT 0;
UPDATE issues i SET logged_time_minutes=coalesce((SELECT sum(duration_minutes) FROM work_logs w WHERE w.tenant_id=i.tenant_id AND w.issue_id=i.id),0);
CREATE UNIQUE INDEX IF NOT EXISTS uq_releases_scope ON releases(tenant_id,project_id,id);
CREATE UNIQUE INDEX IF NOT EXISTS uq_issues_label_scope ON issues(tenant_id,id);
CREATE TABLE IF NOT EXISTS issue_releases (
 tenant_id bigint NOT NULL, project_id bigint NOT NULL, issue_id bigint NOT NULL, release_id bigint NOT NULL, kind varchar(10) NOT NULL CHECK(kind IN ('FIX','AFFECTS')),
 PRIMARY KEY(issue_id,release_id,kind),
 FOREIGN KEY(tenant_id,project_id,issue_id) REFERENCES issues(tenant_id,project_id,id) ON DELETE CASCADE,
 FOREIGN KEY(tenant_id,project_id,release_id) REFERENCES releases(tenant_id,project_id,id) ON DELETE RESTRICT
);
CREATE INDEX IF NOT EXISTS ix_issue_releases_filter ON issue_releases(tenant_id,release_id,kind,issue_id);
CREATE INDEX IF NOT EXISTS ix_labels_tenant_label ON issue_labels(tenant_id,label,issue_id);
ALTER TABLE sprints ADD COLUMN IF NOT EXISTS board_id bigint REFERENCES boards(id) ON DELETE RESTRICT;
CREATE INDEX IF NOT EXISTS ix_sprint_board ON sprints(tenant_id,board_id,status);
CREATE INDEX IF NOT EXISTS ix_backlog_order ON issues(tenant_id,project_id,backlog_order,id);
DO $$ BEGIN
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_issue_labels_tenant_issue') THEN ALTER TABLE issue_labels ADD CONSTRAINT fk_issue_labels_tenant_issue FOREIGN KEY(tenant_id,issue_id) REFERENCES issues(tenant_id,id) ON DELETE CASCADE; END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_components_tenant_project') THEN ALTER TABLE components ADD CONSTRAINT fk_components_tenant_project FOREIGN KEY(tenant_id,project_id) REFERENCES projects(tenant_id,id); END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_releases_tenant_project') THEN ALTER TABLE releases ADD CONSTRAINT fk_releases_tenant_project FOREIGN KEY(tenant_id,project_id) REFERENCES projects(tenant_id,id); END IF;
 IF NOT EXISTS(SELECT 1 FROM pg_constraint WHERE conname='fk_sprints_tenant_project') THEN ALTER TABLE sprints ADD CONSTRAINT fk_sprints_tenant_project FOREIGN KEY(tenant_id,project_id) REFERENCES projects(tenant_id,id); END IF;
END $$;
COMMIT;
