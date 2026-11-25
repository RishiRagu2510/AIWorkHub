
\set ON_ERROR_STOP on
\if :{?google_client_id}
\else
\echo Supply the configured Google OAuth client ID as google_client_id.
\quit 3
\endif
BEGIN;
INSERT INTO tenants(name, slug, email_domain, status)
SELECT 'Grid Dynamics', 'grid-dynamics', 'griddynamics.com', 'ACTIVE'
WHERE NOT EXISTS (SELECT 1 FROM tenants WHERE lower(email_domain)='griddynamics.com');
DO $$ BEGIN
 IF NOT EXISTS (SELECT 1 FROM tenants WHERE lower(email_domain)='griddynamics.com' AND status='ACTIVE') THEN
  RAISE EXCEPTION 'The Grid Dynamics tenant is not active';
 END IF;
 IF EXISTS (SELECT 1 FROM users u JOIN tenants t ON t.id=u.tenant_id
            WHERE lower(u.email)='rragupathi@griddynamics.com'
              AND lower(t.email_domain) IS DISTINCT FROM 'griddynamics.com') THEN
  RAISE EXCEPTION 'Administrator email already belongs to another organization';
 END IF;
END $$;
INSERT INTO tenant_settings(tenant_id)
SELECT id FROM tenants WHERE lower(email_domain)='griddynamics.com'
ON CONFLICT (tenant_id) DO NOTHING;
INSERT INTO roles(tenant_id,code,name,description)
SELECT t.id, r.code,r.name,r.description FROM tenants t CROSS JOIN (VALUES
 ('ADMIN','Administrator','Organization administration'),
 ('DEVELOPER','Developer','Assigned project and issue access')
) r(code,name,description) WHERE lower(t.email_domain)='griddynamics.com'
ON CONFLICT (tenant_id,code) DO NOTHING;
INSERT INTO role_permissions(role_id,permission_id)
SELECT r.id,p.id FROM roles r JOIN tenants t ON t.id=r.tenant_id CROSS JOIN permissions p
WHERE lower(t.email_domain)='griddynamics.com' AND (r.code='ADMIN' OR
 (r.code='DEVELOPER' AND p.code IN ('USER_VIEW','TEAM_VIEW','PROJECT_VIEW','ISSUE_VIEW',
 'ISSUE_CREATE','ISSUE_UPDATE','ISSUE_ASSIGN','ISSUE_TRANSITION','ISSUE_COMMENT_CREATE',
 'WORKLOG_VIEW','WORKLOG_CREATE','WORKLOG_UPDATE','WORKLOG_DELETE','TIMESHEET_VIEW','EMPLOYEE_VIEW','EMPLOYEE_UPDATE_SELF')))
ON CONFLICT DO NOTHING;
INSERT INTO identity_providers(tenant_id,provider_name,provider_type,client_id,authority,enabled)
SELECT t.id,'Google','GOOGLE',:'google_client_id','https://accounts.google.com',true
FROM tenants t WHERE lower(t.email_domain)='griddynamics.com'
AND NOT EXISTS (SELECT 1 FROM identity_providers p WHERE p.tenant_id=t.id AND p.provider_type='GOOGLE');
INSERT INTO users(tenant_id,email,display_name,status,email_verified)
SELECT id,'rragupathi@griddynamics.com','rragupathi@griddynamics.com','ACTIVE',false
FROM tenants t WHERE lower(email_domain)='griddynamics.com'
AND NOT EXISTS (SELECT 1 FROM users u WHERE u.tenant_id=t.id AND lower(u.email)='rragupathi@griddynamics.com');
INSERT INTO user_roles(tenant_id,user_id,role_id)
SELECT t.id,u.id,r.id FROM tenants t JOIN users u ON u.tenant_id=t.id JOIN roles r ON r.tenant_id=t.id
WHERE lower(t.email_domain)='griddynamics.com' AND lower(u.email)='rragupathi@griddynamics.com' AND r.code='ADMIN'
ON CONFLICT DO NOTHING;

COMMIT;
