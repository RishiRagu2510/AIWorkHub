
INSERT INTO tenants (
    name,
    slug,
    status
)
VALUES (
           'AIWorkHub Development',
           'aiworkhub-dev',
           'ACTIVE'
       )
    ON CONFLICT (slug) DO NOTHING;

INSERT INTO tenant_settings (
    tenant_id,
    timezone,
    locale,
    settings
)
SELECT
    id,
    'UTC',
    'en-US',
    '{
        "environment": "development",
        "aiEnabled": true
    }'::jsonb
FROM tenants
WHERE slug = 'aiworkhub-dev'
    ON CONFLICT (tenant_id) DO NOTHING;


\ir ../identity_tenant/002_seed.sql
INSERT INTO roles (
    tenant_id,
    code,
    name,
    description
)
SELECT
    t.id,
    r.code,
    r.name,
    r.description
FROM tenants t
         CROSS JOIN (
    VALUES
        (
            'ADMIN',
            'Administrator',
            'Full administrative access'
        ),
        (
            'PROJECT_MANAGER',
            'Project Manager',
            'Project and issue management access'
        ),
        (
            'DEVELOPER',
            'Developer',
            'Development and issue execution access'
        ),
        (
            'SERVICE_AGENT',
            'Service Agent',
            'Service management and incident handling access'
        ),
        (
            'USER',
            'User',
            'Standard end-user access'
        )
) AS r(code, name, description)
WHERE t.slug = 'aiworkhub-dev'
    ON CONFLICT (tenant_id, code) DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT
    r.id,
    p.id
FROM roles r
         CROSS JOIN permissions p
         JOIN tenants t
              ON t.id = r.tenant_id
WHERE t.slug = 'aiworkhub-dev'
  AND r.code = 'ADMIN'
    ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT
    r.id,
    p.id
FROM roles r
         JOIN tenants t
              ON t.id = r.tenant_id
         JOIN permissions p
              ON p.code IN (
                            'USER_VIEW',
                            'TEAM_VIEW',
                            'TEAM_CREATE',
                            'TEAM_UPDATE',
                            'PROJECT_VIEW',
                            'PROJECT_CREATE',
                            'PROJECT_UPDATE',
                            'PROJECT_DELETE',
                            'PROJECT_MANAGE',
                            'ISSUE_VIEW',
                            'ISSUE_CREATE',
                            'ISSUE_UPDATE',
                            'ISSUE_ASSIGN',
                            'ISSUE_TRANSITION',
                            'DOCUMENT_VIEW',
                            'DOCUMENT_CREATE',
                            'DOCUMENT_UPDATE',
                            'SERVICE_REQUEST_VIEW',
                            'SERVICE_REQUEST_CREATE',
                            'SERVICE_REQUEST_UPDATE',
                            'INCIDENT_VIEW',
                            'AUTOMATION_VIEW'
                  )
WHERE t.slug = 'aiworkhub-dev'
  AND r.code = 'PROJECT_MANAGER'
    ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT
    r.id,
    p.id
FROM roles r
         JOIN tenants t
              ON t.id = r.tenant_id
         JOIN permissions p
              ON p.code IN (
                            'USER_VIEW',
                            'TEAM_VIEW',

                            'PROJECT_VIEW',

                            'ISSUE_VIEW',
                            'ISSUE_CREATE',
                            'ISSUE_UPDATE',
                            'ISSUE_ASSIGN',
                            'ISSUE_TRANSITION',

                            'GITHUB_READ',
                            'GITHUB_WRITE',
                            'REPOSITORY_MANAGE',

                            'PR_VIEW',
                            'PR_CREATE',
                            'PR_UPDATE',

                            'DOCUMENT_VIEW',
                            'DOCUMENT_CREATE',
                            'DOCUMENT_UPDATE',

                            'SERVICE_REQUEST_VIEW',
                            'SERVICE_REQUEST_CREATE',
                            'SERVICE_REQUEST_UPDATE',

                            'AI_USE',
                            'AI_EXECUTE',

                            'BUG_FIX_VIEW',
                            'BUG_FIX_EXECUTE',

                            'RAG_VIEW'
                  )
WHERE t.slug = 'aiworkhub-dev'
  AND r.code = 'DEVELOPER'
    ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT
    r.id,
    p.id
FROM roles r
         JOIN tenants t
              ON t.id = r.tenant_id
         JOIN permissions p
              ON p.code IN (
                            'USER_VIEW',
                            'TEAM_VIEW',

                            'PROJECT_VIEW',

                            'ISSUE_VIEW',
                            'ISSUE_CREATE',
                            'ISSUE_UPDATE',
                            'ISSUE_ASSIGN',
                            'ISSUE_TRANSITION',

                            'DOCUMENT_VIEW',
                            'DOCUMENT_CREATE',
                            'DOCUMENT_UPDATE',

                            'SERVICE_REQUEST_VIEW',
                            'SERVICE_REQUEST_CREATE',
                            'SERVICE_REQUEST_UPDATE',

                            'INCIDENT_VIEW',
                            'INCIDENT_CREATE',
                            'INCIDENT_UPDATE',
                            'INCIDENT_RESOLVE',

                            'CHANGE_MANAGE',

                            'RAG_VIEW'
                  )
WHERE t.slug = 'aiworkhub-dev'
  AND r.code = 'SERVICE_AGENT'
    ON CONFLICT DO NOTHING;

INSERT INTO role_permissions (role_id, permission_id)
SELECT
    r.id,
    p.id
FROM roles r
         JOIN tenants t
              ON t.id = r.tenant_id
         JOIN permissions p
              ON p.code IN (
                            'USER_VIEW',

                            'PROJECT_VIEW',

                            'ISSUE_VIEW',
                            'ISSUE_CREATE',
                            'ISSUE_UPDATE',

                            'DOCUMENT_VIEW',

                            'SERVICE_REQUEST_VIEW',
                            'SERVICE_REQUEST_CREATE',

                            'INCIDENT_VIEW',

                            'AI_USE',

                            'RAG_VIEW'
                  )
WHERE t.slug = 'aiworkhub-dev'
  AND r.code = 'USER'
    ON CONFLICT DO NOTHING;

