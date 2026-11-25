CREATE TABLE workflows (
                           id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                           tenant_id BIGINT NOT NULL,
                           project_id BIGINT,

                           code VARCHAR(100) NOT NULL,
                           name VARCHAR(200) NOT NULL,
                           description TEXT,

                           is_default BOOLEAN NOT NULL DEFAULT FALSE,
                           active BOOLEAN NOT NULL DEFAULT TRUE,

                           created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                           updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                           CONSTRAINT uq_workflows_tenant_code
                               UNIQUE (tenant_id, code),

                           CONSTRAINT fk_workflows_project
                               FOREIGN KEY (project_id)
                                   REFERENCES projects(id)
                                   ON DELETE CASCADE
);

CREATE INDEX idx_workflows_project_id
    ON workflows (project_id);

CREATE TABLE workflow_statuses (
                                   id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                   tenant_id BIGINT NOT NULL,
                                   workflow_id BIGINT NOT NULL,

                                   code VARCHAR(50) NOT NULL,
                                   name VARCHAR(150) NOT NULL,

                                   category VARCHAR(30) NOT NULL DEFAULT 'TODO',

                                   position INTEGER NOT NULL,

                                   is_initial BOOLEAN NOT NULL DEFAULT FALSE,
                                   is_final BOOLEAN NOT NULL DEFAULT FALSE,

                                   created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                   CONSTRAINT fk_workflow_statuses_workflow
                                       FOREIGN KEY (workflow_id)
                                           REFERENCES workflows(id)
                                           ON DELETE CASCADE,

                                   CONSTRAINT uq_workflow_statuses_code
                                       UNIQUE (workflow_id, code),

                                   CONSTRAINT uq_workflow_statuses_position
                                       UNIQUE (workflow_id, position),

                                   CONSTRAINT ck_workflow_statuses_category
                                       CHECK (
                                           category IN (
                                                        'TODO',
                                                        'IN_PROGRESS',
                                                        'DONE'
                                               )
                                           )
);

CREATE INDEX idx_workflow_statuses_workflow_id
    ON workflow_statuses (workflow_id);

CREATE TABLE workflow_transitions (
                                      id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                      tenant_id BIGINT NOT NULL,
                                      workflow_id BIGINT NOT NULL,

                                      from_status_id BIGINT,
                                      to_status_id BIGINT NOT NULL,

                                      code VARCHAR(100) NOT NULL,
                                      name VARCHAR(200) NOT NULL,

                                      requires_permission VARCHAR(150),

                                      created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                      CONSTRAINT fk_workflow_transitions_workflow
                                          FOREIGN KEY (workflow_id)
                                              REFERENCES workflows(id)
                                              ON DELETE CASCADE,

                                      CONSTRAINT fk_workflow_transitions_from_status
                                          FOREIGN KEY (from_status_id)
                                              REFERENCES workflow_statuses(id)
                                              ON DELETE CASCADE,

                                      CONSTRAINT fk_workflow_transitions_to_status
                                          FOREIGN KEY (to_status_id)
                                              REFERENCES workflow_statuses(id)
                                              ON DELETE CASCADE,

                                      CONSTRAINT uq_workflow_transition_code
                                          UNIQUE (workflow_id, code),

                                      CONSTRAINT ck_workflow_transition_not_same
                                          CHECK (
                                              from_status_id IS NULL
                                                  OR from_status_id <> to_status_id
                                              )
);

CREATE INDEX idx_workflow_transitions_workflow_id
    ON workflow_transitions (workflow_id);

CREATE INDEX idx_workflow_transitions_from_status
    ON workflow_transitions (from_status_id);

CREATE INDEX idx_workflow_transitions_to_status
    ON workflow_transitions (to_status_id);