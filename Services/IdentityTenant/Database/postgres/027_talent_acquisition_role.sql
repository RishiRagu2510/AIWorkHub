
INSERT INTO roles(tenant_id,code,name,created_at,updated_at)
SELECT id,'TALENT_ACQUISITION','Talent Acquisition',CURRENT_TIMESTAMP,CURRENT_TIMESTAMP FROM tenants
ON CONFLICT(tenant_id,code) DO NOTHING;
INSERT INTO role_permissions(role_id,permission_id,created_at)
SELECT r.id,p.id,CURRENT_TIMESTAMP FROM roles r CROSS JOIN permissions p
WHERE r.code='TALENT_ACQUISITION' AND p.code IN ('ISSUE_CREATE','ISSUE_VIEW','PROJECT_VIEW')
ON CONFLICT DO NOTHING;
