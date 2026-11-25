CREATE TABLE project_repositories (
                                      id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                      tenant_id BIGINT NOT NULL,
                                      project_id BIGINT NOT NULL,

                                      github_repository_id BIGINT NOT NULL,

                                      is_primary BOOLEAN NOT NULL DEFAULT FALSE,

                                      created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                      updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                      CONSTRAINT fk_project_repositories_project
                                          FOREIGN KEY (project_id)
                                              REFERENCES projects(id)
                                              ON DELETE CASCADE,

                                      CONSTRAINT uq_project_repositories
                                          UNIQUE (project_id, github_repository_id)
);

CREATE INDEX idx_project_repositories_project_id
    ON project_repositories (project_id);

CREATE INDEX idx_project_repositories_github_repository_id
    ON project_repositories (github_repository_id);