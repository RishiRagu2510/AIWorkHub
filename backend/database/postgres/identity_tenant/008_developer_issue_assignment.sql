BEGIN;
INSERT INTO role_permissions(role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.code = 'DEVELOPER' AND p.code = 'ISSUE_ASSIGN'
ON CONFLICT DO NOTHING;
COMMIT;
