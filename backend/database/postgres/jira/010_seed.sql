
\if :{?tenant_id}
\else
\echo Supply -v tenant_id=<existing-tenant-id> to seed demo data.
\quit 3
\endif
INSERT INTO projects (
    tenant_id,
    key,
    name,
    description,
    project_type,
    status
)
VALUES (
           :'tenant_id'::bigint,
           'AIW',
           'AIWorkHub',
           'AIWorkHub development project',
           'SOFTWARE',
           'ACTIVE'
       )
    ON CONFLICT (tenant_id, key) DO NOTHING;

INSERT INTO workflows (
    tenant_id,
    project_id,
    code,
    name,
    description,
    is_default,
    active
)
SELECT
    :'tenant_id'::bigint,
    p.id,
    'DEFAULT_SOFTWARE',
    'Default Software Workflow',
    'Default AIWorkHub software development workflow',
    TRUE,
    TRUE
FROM projects p
WHERE p.tenant_id = :'tenant_id'::bigint
  AND p.key = 'AIW'
    ON CONFLICT (tenant_id, code) DO NOTHING;

INSERT INTO workflow_statuses (
    tenant_id,
    workflow_id,
    code,
    name,
    category,
    position,
    is_initial,
    is_final
)
SELECT
    :'tenant_id'::bigint,
    w.id,
    s.code,
    s.name,
    s.category,
    s.position,
    s.is_initial,
    s.is_final
FROM workflows w
         CROSS JOIN (
    VALUES
        ('TODO', 'To Do', 'TODO', 1, TRUE, FALSE),
        ('IN_PROGRESS', 'In Progress', 'IN_PROGRESS', 2, FALSE, FALSE),
        ('CODE_REVIEW', 'Code Review', 'IN_PROGRESS', 3, FALSE, FALSE),
        ('TESTING', 'Testing', 'IN_PROGRESS', 4, FALSE, FALSE),
        ('DONE', 'Done', 'DONE', 5, FALSE, TRUE)
) AS s(code, name, category, position, is_initial, is_final)
WHERE w.tenant_id = :'tenant_id'::bigint
  AND w.code = 'DEFAULT_SOFTWARE'
    ON CONFLICT (workflow_id, code) DO NOTHING;

INSERT INTO workflow_transitions (
    tenant_id,
    workflow_id,
    from_status_id,
    to_status_id,
    code,
    name,
    requires_permission
)
SELECT
    :'tenant_id'::bigint,
    w.id,
    from_status.id,
    to_status.id,
    transition.code,
    transition.name,
    transition.requires_permission
FROM workflows w
         JOIN (
    VALUES
        (
            'TODO',
            'IN_PROGRESS',
            'START_PROGRESS',
            'Start Progress',
            'ISSUE_TRANSITION'
        ),
        (
            'IN_PROGRESS',
            'CODE_REVIEW',
            'SEND_TO_CODE_REVIEW',
            'Send to Code Review',
            'ISSUE_TRANSITION'
        ),
        (
            'CODE_REVIEW',
            'TESTING',
            'START_TESTING',
            'Start Testing',
            'ISSUE_TRANSITION'
        ),
        (
            'TESTING',
            'DONE',
            'COMPLETE',
            'Complete',
            'ISSUE_TRANSITION'
        ),
        (
            'IN_PROGRESS',
            'TODO',
            'MOVE_BACK_TO_TODO',
            'Move Back to To Do',
            'ISSUE_TRANSITION'
        ),
        (
            'CODE_REVIEW',
            'IN_PROGRESS',
            'RETURN_TO_DEVELOPMENT',
            'Return to Development',
            'ISSUE_TRANSITION'
        ),
        (
            'TESTING',
            'IN_PROGRESS',
            'RETURN_TO_DEVELOPMENT_FROM_TESTING',
            'Return to Development',
            'ISSUE_TRANSITION'
        )
) AS transition(
                from_code,
                to_code,
                code,
                name,
                requires_permission
    )
              ON TRUE
         JOIN workflow_statuses from_status
              ON from_status.workflow_id = w.id
                  AND from_status.code = transition.from_code
         JOIN workflow_statuses to_status
              ON to_status.workflow_id = w.id
                  AND to_status.code = transition.to_code
WHERE w.tenant_id = :'tenant_id'::bigint
  AND w.code = 'DEFAULT_SOFTWARE'
    ON CONFLICT (workflow_id, code) DO NOTHING;

INSERT INTO boards (
    tenant_id,
    project_id,
    name,
    board_type,
    filter_definition
)
SELECT
    :'tenant_id'::bigint,
    p.id,
    'AIWorkHub Board',
    'SCRUM',
    '{
        "projectKey": "AIW"
    }'::jsonb
FROM projects p
WHERE p.tenant_id = :'tenant_id'::bigint
  AND p.key = 'AIW'
  AND NOT EXISTS (
    SELECT 1
    FROM boards b
    WHERE b.project_id = p.id
      AND b.name = 'AIWorkHub Board'
);

INSERT INTO board_columns (
    board_id,
    name,
    status_code,
    position
)
SELECT
    b.id,
    c.name,
    c.status_code,
    c.position
FROM boards b
         CROSS JOIN (
    VALUES
        ('To Do', 'TODO', 1),
        ('In Progress', 'IN_PROGRESS', 2),
        ('Code Review', 'CODE_REVIEW', 3),
        ('Testing', 'TESTING', 4),
        ('Done', 'DONE', 5)
) AS c(name, status_code, position)
WHERE b.project_id = (
    SELECT p.id
    FROM projects p
    WHERE p.tenant_id = :'tenant_id'::bigint
      AND p.key = 'AIW'
)
    ON CONFLICT (board_id, status_code) DO NOTHING;

INSERT INTO releases (
    tenant_id,
    project_id,
    name,
    version,
    description,
    status
)
SELECT
    :'tenant_id'::bigint,
    p.id,
    'AIWorkHub 1.0',
    '1.0.0',
    'Initial AIWorkHub development release',
    'UNRELEASED'
FROM projects p
WHERE p.tenant_id = :'tenant_id'::bigint
  AND p.key = 'AIW'
    ON CONFLICT (project_id, version) DO NOTHING;