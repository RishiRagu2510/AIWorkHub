
BEGIN;
ALTER TABLE accounts ADD COLUMN IF NOT EXISTS status varchar(30) NOT NULL DEFAULT 'ACTIVE';
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='accounts'::regclass AND conname='ck_accounts_status') THEN
  ALTER TABLE accounts ADD CONSTRAINT ck_accounts_status CHECK(status IN ('ACTIVE','ARCHIVED'));
 END IF;
END $$;
ALTER TABLE projects ALTER COLUMN account_id SET NOT NULL;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS display_name varchar(1000);
UPDATE projects SET display_name=key WHERE display_name IS NULL;
ALTER TABLE projects ALTER COLUMN display_name SET NOT NULL;


CREATE OR REPLACE FUNCTION check_project_account_tree() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW.parent_project_id IS NOT NULL AND NOT EXISTS (
  SELECT 1 FROM projects p WHERE p.id=NEW.parent_project_id AND p.tenant_id=NEW.tenant_id AND p.account_id=NEW.account_id
 ) THEN RAISE EXCEPTION 'Subprojects must share their parent account'; END IF;
 IF EXISTS(SELECT 1 FROM projects c WHERE c.parent_project_id=NEW.id AND c.account_id<>NEW.account_id)
 THEN RAISE EXCEPTION 'Child projects must share the account'; END IF;
 RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS projects_account_tree ON projects;
CREATE TRIGGER projects_account_tree BEFORE INSERT OR UPDATE OF account_id,parent_project_id ON projects
 FOR EACH ROW EXECUTE FUNCTION check_project_account_tree();
COMMIT;
