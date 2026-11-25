






CREATE TABLE IF NOT EXISTS schema_data_cleanups (
    name       VARCHAR(100) PRIMARY KEY,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM schema_data_cleanups WHERE name = '024_cleanup_organization_setup_data') THEN
        RETURN;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM employees) THEN
        DELETE FROM career_level_skills;
        DELETE FROM career_levels;
        DELETE FROM grades;
        DELETE FROM career_profiles;
    END IF;

    INSERT INTO schema_data_cleanups (name) VALUES ('024_cleanup_organization_setup_data');
END
$$;
