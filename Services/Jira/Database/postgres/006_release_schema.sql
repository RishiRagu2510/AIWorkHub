CREATE TABLE releases (
                          id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                          tenant_id BIGINT NOT NULL,
                          project_id BIGINT NOT NULL,

                          name VARCHAR(200) NOT NULL,
                          version VARCHAR(100) NOT NULL,
                          description TEXT,

                          status VARCHAR(30) NOT NULL DEFAULT 'UNRELEASED',

                          start_date DATE,
                          release_date DATE,

                          created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                          updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                          CONSTRAINT fk_releases_project
                              FOREIGN KEY (project_id)
                                  REFERENCES projects(id)
                                  ON DELETE CASCADE,

                          CONSTRAINT uq_releases_project_version
                              UNIQUE (project_id, version),

                          CONSTRAINT ck_releases_status
                              CHECK (
                                  status IN (
                                             'UNRELEASED',
                                             'RELEASED',
                                             'ARCHIVED'
                                      )
                                  ),

                          CONSTRAINT ck_releases_dates
                              CHECK (
                                  release_date IS NULL
                                      OR start_date IS NULL
                                      OR release_date >= start_date
                                  )
);

CREATE INDEX idx_releases_project_id
    ON releases (project_id);

CREATE INDEX idx_releases_status
    ON releases (project_id, status);