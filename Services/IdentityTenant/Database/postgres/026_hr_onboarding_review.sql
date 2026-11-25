
INSERT INTO role_permissions(role_id,permission_id,created_at)
SELECT r.id,p.id,CURRENT_TIMESTAMP FROM roles r CROSS JOIN permissions p
WHERE r.code='HR' AND p.code IN ('ISSUE_VIEW','PROJECT_VIEW')
ON CONFLICT DO NOTHING;
