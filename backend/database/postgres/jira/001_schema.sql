CREATE TABLE projects (
                          id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                          tenant_id BIGINT NOT NULL,
                          key VARCHAR(1000) NOT NULL,
                          name VARCHAR(200) NOT NULL,
                          description TEXT,
                          project_type VARCHAR(30) NOT NULL DEFAULT 'SOFTWARE',
                          status VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',
                          project_lead_user_id BIGINT,
                          parent_project_id BIGINT,
                          created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                          updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                          CONSTRAINT uq_projects_tenant_id_id UNIQUE (tenant_id, id),
                          CONSTRAINT fk_projects_parent_same_tenant FOREIGN KEY (tenant_id, parent_project_id)
                              REFERENCES projects(tenant_id, id) ON DELETE RESTRICT,
                          CONSTRAINT ck_projects_not_self_parent CHECK (parent_project_id IS NULL OR parent_project_id <> id),

                          CONSTRAINT uq_projects_tenant_key
                              UNIQUE (tenant_id, key),

                          CONSTRAINT ck_projects_type
                              CHECK (project_type IN ('SOFTWARE', 'SERVICE', 'BUSINESS', 'BANKING')),

                          CONSTRAINT ck_projects_status
                              CHECK (status IN ('ACTIVE', 'ARCHIVED', 'DISABLED'))
);

CREATE INDEX ix_projects_tenant_parent ON projects(tenant_id, parent_project_id);

CREATE TABLE project_members (
                                 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                 tenant_id BIGINT NOT NULL,
                                 project_id BIGINT NOT NULL,
                                 user_id BIGINT NOT NULL,
                                 role VARCHAR(50) NOT NULL DEFAULT 'MEMBER',
                                 created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                 CONSTRAINT uq_project_members
                                     UNIQUE (project_id, user_id),

                                 CONSTRAINT fk_project_members_project
                                     FOREIGN KEY (project_id)
                                         REFERENCES projects(id)
                                         ON DELETE CASCADE,

                                 CONSTRAINT ck_project_members_role
                                     CHECK (role IN ('ADMIN', 'LEAD', 'MEMBER', 'VIEWER','DEVELOPER'))
);

CREATE TABLE components (
                            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                            tenant_id BIGINT NOT NULL,
                            project_id BIGINT NOT NULL,
                            name VARCHAR(150) NOT NULL,
                            description TEXT,
                            lead_user_id BIGINT,
                            created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                            updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                            CONSTRAINT uq_components_project_name
                                UNIQUE (project_id, name),

                            CONSTRAINT fk_components_project
                                FOREIGN KEY (project_id)
                                    REFERENCES projects(id)
                                    ON DELETE CASCADE
);
