
INSERT INTO permissions (code, name, description)
VALUES
    ('USER_VIEW', 'View Users', 'View users'),
    ('USER_CREATE', 'Create Users', 'Create users'),
    ('USER_UPDATE', 'Update Users', 'Update users'),
    ('USER_DELETE', 'Delete Users', 'Delete users'),

    ('ROLE_VIEW', 'View Roles', 'View roles'),
    ('ROLE_MANAGE', 'Manage Roles', 'Create, update and delete roles'),

    ('PERMISSION_VIEW', 'View Permissions', 'View permissions'),
    ('PERMISSION_MANAGE', 'Manage Permissions', 'Manage permissions'),

    ('TEAM_VIEW', 'View Teams', 'View teams'),
    ('TEAM_CREATE', 'Create Teams', 'Create teams'),
    ('TEAM_UPDATE', 'Update Teams', 'Update teams'),
    ('TEAM_DELETE', 'Delete Teams', 'Delete teams'),

    ('PROJECT_VIEW', 'View Projects', 'View projects'),
    ('PROJECT_CREATE', 'Create Projects', 'Create projects'),
    ('PROJECT_UPDATE', 'Update Projects', 'Update projects'),
    ('PROJECT_DELETE', 'Delete Projects', 'Delete projects'),
    ('PROJECT_MANAGE', 'Manage Projects', 'Manage project configuration'),

    ('ISSUE_VIEW', 'View Issues', 'View issues'),
    ('ISSUE_CREATE', 'Create Issues', 'Create issues'),
    ('ISSUE_UPDATE', 'Update Issues', 'Update issues'),
    ('ISSUE_DELETE', 'Delete Issues', 'Delete issues'),
    ('ISSUE_ASSIGN', 'Assign Issues', 'Assign issues'),
    ('ISSUE_TRANSITION', 'Transition Issues', 'Move issues through workflows'),
    ('ISSUE_COMMENT_CREATE', 'Create Issue Comments', 'Create issue comments'),

    ('GITHUB_READ', 'Read GitHub', 'Read GitHub data'),
    ('GITHUB_UPDATE', 'Upate GitHub', 'Update GitHub data'),
    ('GITHUB_WRITE', 'Write GitHub', 'Perform allowed GitHub write operations'),

    ('REPOSITORY_MANAGE', 'Manage Repositories', 'Manage repository configuration'),

    ('PR_VIEW', 'View Pull Requests', 'View pull requests'),
    ('PR_CREATE', 'Create Pull Requests', 'Create pull requests'),
    ('PR_UPDATE', 'Update Pull Requests', 'Update pull requests'),
    ('PR_APPROVE', 'Approve Pull Requests', 'Approve pull requests'),

    ('DOCUMENT_VIEW', 'View Documents', 'View documentation'),
    ('DOCUMENT_CREATE', 'Create Documents', 'Create documentation'),
    ('DOCUMENT_UPDATE', 'Update Documents', 'Update documentation'),
    ('DOCUMENT_DELETE', 'Delete Documents', 'Delete documentation'),
    ('DOCUMENT_MANAGE', 'Manage Documentation', 'Manage documentation configuration'),

    ('SERVICE_REQUEST_VIEW', 'View Service Requests', 'View service requests'),
    ('SERVICE_REQUEST_CREATE', 'Create Service Requests', 'Create service requests'),
    ('SERVICE_REQUEST_UPDATE', 'Update Service Requests', 'Update service requests'),

    ('INCIDENT_VIEW', 'View Incidents', 'View incidents'),
    ('INCIDENT_CREATE', 'Create Incidents', 'Create incidents'),
    ('INCIDENT_UPDATE', 'Update Incidents', 'Update incidents'),
    ('INCIDENT_RESOLVE', 'Resolve Incidents', 'Resolve incidents'),

    ('CHANGE_MANAGE', 'Manage Changes', 'Manage changes'),

    ('AI_USE', 'Use AI', 'Execute allowed AI operations'),
    ('AI_EXECUTE', 'Execute AI Actions', 'Execute AI actions'),

    ('BUG_FIX_VIEW', 'View Bug Fix Jobs', 'View autonomous bug fix jobs'),
    ('BUG_FIX_EXECUTE', 'Execute Bug Fix', 'Execute autonomous bug fixes'),
    ('BUG_FIX_APPROVE', 'Approve Bug Fix', 'Approve autonomous bug fixes'),

    ('RAG_VIEW', 'View RAG', 'Use RAG capabilities'),
    ('RAG_MANAGE', 'Manage RAG', 'Manage RAG configuration'),

    ('AUTOMATION_VIEW', 'View Automations', 'View automation rules'),
    ('AUTOMATION_MANAGE', 'Manage Automations', 'Manage automation rules'),

    ('POLICY_VIEW', 'View Policies', 'View guardrail policies'),
    ('POLICY_MANAGE', 'Manage Policies', 'Manage guardrail policies'),

    ('AUDIT_VIEW', 'View Audit Logs', 'View audit records'),

    ('TENANT_VIEW', 'View Tenant', 'View tenant configuration'),
    ('TENANT_MANAGE', 'Manage Tenant', 'Manage tenant configuration')
    ON CONFLICT (code) DO NOTHING;

