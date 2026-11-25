CREATE TABLE tenants (
    email_domain VARCHAR(253),
                         id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                         name VARCHAR(200) NOT NULL,
                         slug VARCHAR(100) NOT NULL,
                         status VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',
                         created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                         updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                         CONSTRAINT uq_tenants_slug UNIQUE (slug),

                         CONSTRAINT ck_tenants_status
                             CHECK (status IN ('ACTIVE', 'SUSPENDED', 'DISABLED'))
);



CREATE UNIQUE INDEX ux_tenants_email_domain ON tenants (LOWER(email_domain));

CREATE TABLE tenant_settings (
                                 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                 tenant_id BIGINT NOT NULL,
                                 timezone VARCHAR(100) NOT NULL DEFAULT 'UTC',
                                 locale VARCHAR(20) NOT NULL DEFAULT 'en-US',
                                 settings JSONB NOT NULL DEFAULT '{}'::jsonb,
                                 created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                 updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                 CONSTRAINT uq_tenant_settings_tenant
                                     UNIQUE (tenant_id),

                                 CONSTRAINT fk_tenant_settings_tenant
                                     FOREIGN KEY (tenant_id)
                                         REFERENCES tenants(id)
                                         ON DELETE CASCADE
);


CREATE TABLE users (
                       id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                       tenant_id BIGINT NOT NULL,
                       email VARCHAR(320) NOT NULL,
                       display_name VARCHAR(200) NOT NULL,
                       status VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',
                       email_verified BOOLEAN NOT NULL DEFAULT FALSE,
                       last_login_at TIMESTAMPTZ,
                       created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                       updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                       CONSTRAINT uq_users_tenant_email
                           UNIQUE (tenant_id, email),

                       CONSTRAINT fk_users_tenant
                           FOREIGN KEY (tenant_id)
                               REFERENCES tenants(id)
                               ON DELETE CASCADE,

                       CONSTRAINT ck_users_status
                           CHECK (status IN ('ACTIVE', 'SUSPENDED', 'DISABLED'))
);


CREATE TABLE identity_providers (
                                    id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                    tenant_id BIGINT NOT NULL,
                                    provider_name VARCHAR(100) NOT NULL,
                                    provider_type VARCHAR(30) NOT NULL,
                                    client_id VARCHAR(500) NOT NULL,
                                    authority VARCHAR(500),
                                    enabled BOOLEAN NOT NULL DEFAULT TRUE,
                                    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                    CONSTRAINT fk_identity_providers_tenant
                                        FOREIGN KEY (tenant_id)
                                            REFERENCES tenants(id)
                                            ON DELETE CASCADE,

                                    CONSTRAINT ck_identity_providers_type
                                        CHECK (provider_type IN ('GOOGLE', 'MICROSOFT', 'OIDC'))
);


CREATE TABLE user_external_identities (
                                          id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                                          user_id BIGINT NOT NULL,
                                          identity_provider_id BIGINT NOT NULL,
                                          external_subject VARCHAR(500) NOT NULL,
                                          email VARCHAR(320),
                                          created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                          updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                          CONSTRAINT uq_external_identity
                                              UNIQUE (identity_provider_id, external_subject),

                                          CONSTRAINT fk_external_identity_user
                                              FOREIGN KEY (user_id)
                                                  REFERENCES users(id)
                                                  ON DELETE CASCADE,

                                          CONSTRAINT fk_external_identity_provider
                                              FOREIGN KEY (identity_provider_id)
                                                  REFERENCES identity_providers(id)
                                                  ON DELETE CASCADE
);

CREATE TABLE roles (
                       id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                       tenant_id BIGINT NOT NULL,
                       code VARCHAR(100) NOT NULL,
                       name VARCHAR(150) NOT NULL,
                       description TEXT,
                       created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                       updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                       CONSTRAINT uq_roles_tenant_code
                           UNIQUE (tenant_id, code),

                       CONSTRAINT fk_roles_tenant
                           FOREIGN KEY (tenant_id)
                               REFERENCES tenants(id)
                               ON DELETE CASCADE
);

CREATE TABLE permissions (
                             id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                             code VARCHAR(150) NOT NULL,
                             name VARCHAR(200) NOT NULL,
                             description TEXT,
                             created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                             updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                             CONSTRAINT uq_permissions_code
                                 UNIQUE (code)
);

CREATE TABLE role_permissions (
                                  role_id BIGINT NOT NULL,
                                  permission_id BIGINT NOT NULL,
                                  created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                  CONSTRAINT pk_role_permissions
                                      PRIMARY KEY (role_id, permission_id),

                                  CONSTRAINT fk_role_permissions_role
                                      FOREIGN KEY (role_id)
                                          REFERENCES roles(id)
                                          ON DELETE CASCADE,

                                  CONSTRAINT fk_role_permissions_permission
                                      FOREIGN KEY (permission_id)
                                          REFERENCES permissions(id)
                                          ON DELETE CASCADE
);

CREATE TABLE user_roles (
                            tenant_id BIGINT NOT NULL,
                            user_id BIGINT NOT NULL,
                            role_id BIGINT NOT NULL,
                            assigned_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                            CONSTRAINT pk_user_roles
                                PRIMARY KEY (user_id, role_id),

                            CONSTRAINT fk_user_roles_tenant
                                FOREIGN KEY (tenant_id)
                                    REFERENCES tenants(id)
                                    ON DELETE CASCADE,

                            CONSTRAINT fk_user_roles_user
                                FOREIGN KEY (user_id)
                                    REFERENCES users(id)
                                    ON DELETE CASCADE,

                            CONSTRAINT fk_user_roles_role
                                FOREIGN KEY (role_id)
                                    REFERENCES roles(id)
                                    ON DELETE CASCADE
);

CREATE TABLE teams (
                       id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                       tenant_id BIGINT NOT NULL,
                       name VARCHAR(150) NOT NULL,
                       code VARCHAR(100) NOT NULL,
                       description TEXT,
                       status VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',
                       created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                       updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                       CONSTRAINT uq_teams_tenant_code
                           UNIQUE (tenant_id, code),

                       CONSTRAINT fk_teams_tenant
                           FOREIGN KEY (tenant_id)
                               REFERENCES tenants(id)
                               ON DELETE CASCADE,

                       CONSTRAINT ck_teams_status
                           CHECK (status IN ('ACTIVE', 'DISABLED'))
);

CREATE TABLE team_members (
                              team_id BIGINT NOT NULL,
                              user_id BIGINT NOT NULL,
                              joined_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                              CONSTRAINT pk_team_members
                                  PRIMARY KEY (team_id, user_id),

                              CONSTRAINT fk_team_members_team
                                  FOREIGN KEY (team_id)
                                      REFERENCES teams(id)
                                      ON DELETE CASCADE,

                              CONSTRAINT fk_team_members_user
                                  FOREIGN KEY (user_id)
                                      REFERENCES users(id)
                                      ON DELETE CASCADE
);

CREATE TABLE user_sessions (
                               id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
                               user_id BIGINT NOT NULL,
                               session_token_hash VARCHAR(128) NOT NULL,
                               expires_at TIMESTAMPTZ NOT NULL,
                               revoked_at TIMESTAMPTZ,
                               last_seen_at TIMESTAMPTZ,
                               created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                               CONSTRAINT uq_user_sessions_token_hash
                                   UNIQUE (session_token_hash),

                               CONSTRAINT fk_user_sessions_user
                                   FOREIGN KEY (user_id)
                                       REFERENCES users(id)
                                       ON DELETE CASCADE
);

CREATE INDEX idx_users_tenant_id
    ON users (tenant_id);

CREATE INDEX idx_identity_providers_tenant_id
    ON identity_providers (tenant_id);

CREATE INDEX idx_user_external_identities_user_id
    ON user_external_identities (user_id);

CREATE INDEX idx_roles_tenant_id
    ON roles (tenant_id);

CREATE INDEX idx_user_roles_tenant_id
    ON user_roles (tenant_id);

CREATE INDEX idx_user_roles_role_id
    ON user_roles (role_id);

CREATE INDEX idx_teams_tenant_id
    ON teams (tenant_id);

CREATE INDEX idx_team_members_user_id
    ON team_members (user_id);

CREATE INDEX idx_user_sessions_user_id
    ON user_sessions (user_id);

CREATE INDEX idx_user_sessions_expires_at
    ON user_sessions (expires_at);