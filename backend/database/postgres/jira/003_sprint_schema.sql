CREATE TABLE sprints (
                         id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                         tenant_id BIGINT NOT NULL,
                         project_id BIGINT NOT NULL,

                         name VARCHAR(200) NOT NULL,
                         goal TEXT,

                         status VARCHAR(30) NOT NULL DEFAULT 'PLANNED',

                         start_date DATE,
                         end_date DATE,

                         created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                         updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                         CONSTRAINT fk_sprints_project
                             FOREIGN KEY (project_id)
                                 REFERENCES projects(id)
                                 ON DELETE CASCADE,

                         CONSTRAINT ck_sprints_status
                             CHECK (
                                 status IN (
                                            'PLANNED',
                                            'ACTIVE',
                                            'COMPLETED',
                                            'CANCELLED'
                                     )
                                 ),

                         CONSTRAINT ck_sprints_dates
                             CHECK (
                                 end_date IS NULL
                                     OR start_date IS NULL
                                     OR end_date >= start_date
                                 )
);

CREATE INDEX idx_sprints_project_id
    ON sprints (project_id);

CREATE INDEX idx_sprints_status
    ON sprints (project_id, status);

CREATE TABLE sprint_issues (
                               sprint_id BIGINT NOT NULL,
                               issue_id BIGINT NOT NULL,

                               added_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                               CONSTRAINT pk_sprint_issues
                                   PRIMARY KEY (sprint_id, issue_id),

                               CONSTRAINT fk_sprint_issues_sprint
                                   FOREIGN KEY (sprint_id)
                                       REFERENCES sprints(id)
                                       ON DELETE CASCADE,

                               CONSTRAINT fk_sprint_issues_issue
                                   FOREIGN KEY (issue_id)
                                       REFERENCES issues(id)
                                       ON DELETE CASCADE
);

CREATE INDEX idx_sprint_issues_issue_id
    ON sprint_issues (issue_id);