BEGIN;
ALTER TABLE projects DROP CONSTRAINT IF EXISTS ck_projects_type;
ALTER TABLE projects ADD CONSTRAINT ck_projects_type
 CHECK (project_type IN ('SOFTWARE', 'SERVICE', 'BUSINESS', 'BANKING'));
COMMIT;
