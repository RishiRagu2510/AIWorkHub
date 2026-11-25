BEGIN;

LOCK TABLE roles, grades, career_profiles, career_levels IN SHARE ROW EXCLUSIVE MODE;
SELECT setval(pg_get_serial_sequence('roles','id'), GREATEST(COALESCE((SELECT MAX(id) FROM roles),0), nextval(pg_get_serial_sequence('roles','id'))));
SELECT setval(pg_get_serial_sequence('grades','id'), GREATEST(COALESCE((SELECT MAX(id) FROM grades),0), nextval(pg_get_serial_sequence('grades','id'))));
SELECT setval(pg_get_serial_sequence('career_profiles','id'), GREATEST(COALESCE((SELECT MAX(id) FROM career_profiles),0), nextval(pg_get_serial_sequence('career_profiles','id'))));
SELECT setval(pg_get_serial_sequence('career_levels','id'), GREATEST(COALESCE((SELECT MAX(id) FROM career_levels),0), nextval(pg_get_serial_sequence('career_levels','id'))));
COMMIT;
