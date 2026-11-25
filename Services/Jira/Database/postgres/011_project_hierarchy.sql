
BEGIN;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS parent_project_id BIGINT;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'projects'::regclass AND conname = 'uq_projects_tenant_id_id') THEN
        ALTER TABLE projects ADD CONSTRAINT uq_projects_tenant_id_id UNIQUE (tenant_id, id);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'projects'::regclass AND conname = 'fk_projects_parent_same_tenant') THEN
        ALTER TABLE projects ADD CONSTRAINT fk_projects_parent_same_tenant
            FOREIGN KEY (tenant_id, parent_project_id) REFERENCES projects(tenant_id, id) ON DELETE RESTRICT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = 'projects'::regclass AND conname = 'ck_projects_not_self_parent') THEN
        ALTER TABLE projects ADD CONSTRAINT ck_projects_not_self_parent CHECK (parent_project_id IS NULL OR parent_project_id <> id);
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_projects_tenant_parent ON projects(tenant_id, parent_project_id);
COMMIT;
