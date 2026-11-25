INSERT INTO roles(tenant_id, code, name, created_at, updated_at)
SELECT id, 'HR', 'Human Resources', CURRENT_TIMESTAMP, CURRENT_TIMESTAMP FROM tenants
ON CONFLICT (tenant_id, code) DO NOTHING;



INSERT INTO role_permissions(role_id, permission_id, created_at)
SELECT r.id, p.id, CURRENT_TIMESTAMP FROM roles r CROSS JOIN permissions p
WHERE r.code = 'HR' AND p.code LIKE 'EMPLOYEE_%'
ON CONFLICT DO NOTHING;
