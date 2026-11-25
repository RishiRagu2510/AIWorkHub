BEGIN;
DROP INDEX IF EXISTS uq_career_profiles_code;
CREATE UNIQUE INDEX IF NOT EXISTS uq_career_profiles_code_name ON career_profiles(code, name);
CREATE INDEX IF NOT EXISTS "IX_career_profiles_code" ON career_profiles(code);
COMMIT;
