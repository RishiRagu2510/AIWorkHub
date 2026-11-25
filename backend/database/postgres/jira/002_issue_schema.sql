
CREATE TABLE issues (
                        id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                        tenant_id BIGINT NOT NULL,
                        project_id BIGINT NOT NULL,

                        issue_number BIGINT GENERATED ALWAYS AS IDENTITY,

                        issue_key VARCHAR(50) NOT NULL,

                        issue_type VARCHAR(30) NOT NULL DEFAULT 'TASK',

                        summary VARCHAR(500) NOT NULL,
                        description TEXT,

                        status_code VARCHAR(50) NOT NULL DEFAULT 'TODO',

                        priority VARCHAR(30) NOT NULL DEFAULT 'MEDIUM',

                        reporter_user_id BIGINT NOT NULL,
                        assignee_user_id BIGINT,

                        parent_issue_id BIGINT,

                        component_id BIGINT,

                        story_points NUMERIC(8,2),

                        due_date DATE,

                        resolved_at TIMESTAMPTZ,

                        created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                        CONSTRAINT uq_issues_issue_key
                            UNIQUE (issue_key),

                        CONSTRAINT uq_issues_project_issue_number
                            UNIQUE (project_id, issue_number),

                        CONSTRAINT fk_issues_project
                            FOREIGN KEY (project_id)
                                REFERENCES projects(id)
                                ON DELETE CASCADE,

                        CONSTRAINT fk_issues_parent
                            FOREIGN KEY (parent_issue_id)
                                REFERENCES issues(id)
                                ON DELETE SET NULL,

                        CONSTRAINT fk_issues_component
                            FOREIGN KEY (component_id)
                                REFERENCES components(id)
                                ON DELETE SET NULL,

                        CONSTRAINT ck_issues_type
                            CHECK (
                                issue_type IN (
                                               'EPIC',
                                               'STORY',
                                               'TASK',
                                               'BUG',
                                               'SUBTASK'
                                    )
                                ),

                        CONSTRAINT ck_issues_status
                            CHECK (
                                status_code IN (
                                                'TODO',
                                                'IN_PROGRESS',
                                                'CODE_REVIEW',
                                                'TESTING',
                                                'BLOCKED',
                                                'DONE',
                                                'CANCELLED'
                                    )
                                ),

                        CONSTRAINT ck_issues_priority
                            CHECK (
                                priority IN (
                                             'LOWEST',
                                             'LOW',
                                             'MEDIUM',
                                             'HIGH',
                                             'HIGHEST'
                                    )
                                ),

                        CONSTRAINT ck_issues_story_points
                            CHECK (
                                story_points IS NULL
                                    OR story_points >= 0
                                )
);

CREATE INDEX idx_issues_tenant_id
    ON issues (tenant_id);

CREATE INDEX idx_issues_project_id
    ON issues (project_id);

CREATE INDEX idx_issues_assignee_user_id
    ON issues (assignee_user_id);

CREATE INDEX idx_issues_reporter_user_id
    ON issues (reporter_user_id);

CREATE INDEX idx_issues_parent_issue_id
    ON issues (parent_issue_id);

CREATE INDEX idx_issues_component_id
    ON issues (component_id);

CREATE INDEX idx_issues_status
    ON issues (project_id, status_code);

CREATE INDEX idx_issues_priority
    ON issues (project_id, priority);

CREATE TABLE issue_comments (
                                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                tenant_id BIGINT NOT NULL,
                                issue_id BIGINT NOT NULL,
                                author_user_id BIGINT NOT NULL,

                                body TEXT NOT NULL,

                                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                CONSTRAINT fk_issue_comments_issue
                                    FOREIGN KEY (issue_id)
                                        REFERENCES issues(id)
                                        ON DELETE CASCADE
);

CREATE INDEX idx_issue_comments_issue_id
    ON issue_comments (issue_id);

CREATE INDEX idx_issue_comments_author_user_id
    ON issue_comments (author_user_id);


CREATE TABLE issue_labels (
                              id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                              tenant_id BIGINT NOT NULL,
                              issue_id BIGINT NOT NULL,

                              label VARCHAR(100) NOT NULL,

                              created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                              CONSTRAINT uq_issue_labels
                                  UNIQUE (issue_id, label),

                              CONSTRAINT fk_issue_labels_issue
                                  FOREIGN KEY (issue_id)
                                      REFERENCES issues(id)
                                      ON DELETE CASCADE
);

CREATE INDEX idx_issue_labels_issue_id
    ON issue_labels (issue_id);

CREATE INDEX idx_issue_labels_label
    ON issue_labels (label);

CREATE TABLE issue_attachments (
                                   id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                   tenant_id BIGINT NOT NULL,
                                   issue_id BIGINT NOT NULL,

                                   file_name VARCHAR(500) NOT NULL,
                                   object_key VARCHAR(1000) NOT NULL,

                                   content_type VARCHAR(200),
                                   file_size_bytes BIGINT,

                                   uploaded_by_user_id BIGINT NOT NULL,

                                   created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                   CONSTRAINT fk_issue_attachments_issue
                                       FOREIGN KEY (issue_id)
                                           REFERENCES issues(id)
                                           ON DELETE CASCADE
);

CREATE INDEX idx_issue_attachments_issue_id
    ON issue_attachments (issue_id);

CREATE INDEX idx_issue_attachments_object_key
    ON issue_attachments (object_key);

CREATE TABLE issue_links (
                             id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                             tenant_id BIGINT NOT NULL,

                             source_issue_id BIGINT NOT NULL,
                             target_issue_id BIGINT NOT NULL,

                             link_type VARCHAR(50) NOT NULL,

                             created_by_user_id BIGINT NOT NULL,

                             created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                             CONSTRAINT fk_issue_links_source
                                 FOREIGN KEY (source_issue_id)
                                     REFERENCES issues(id)
                                     ON DELETE CASCADE,

                             CONSTRAINT fk_issue_links_target
                                 FOREIGN KEY (target_issue_id)
                                     REFERENCES issues(id)
                                     ON DELETE CASCADE,

                             CONSTRAINT ck_issue_links_not_self
                                 CHECK (source_issue_id <> target_issue_id),

                             CONSTRAINT ck_issue_links_type
                                 CHECK (
                                     link_type IN (
                                                   'BLOCKS',
                                                   'IS_BLOCKED_BY',
                                                   'DUPLICATES',
                                                   'IS_DUPLICATED_BY',
                                                   'RELATES_TO'
                                         )
                                     )
);

CREATE INDEX idx_issue_links_source
    ON issue_links (source_issue_id);

CREATE INDEX idx_issue_links_target
    ON issue_links (target_issue_id);

CREATE TABLE issue_history (
                               id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                               tenant_id BIGINT NOT NULL,
                               issue_id BIGINT NOT NULL,

                               changed_by_user_id BIGINT,

                               field_name VARCHAR(100) NOT NULL,

                               old_value TEXT,
                               new_value TEXT,

                               created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                               CONSTRAINT fk_issue_history_issue
                                   FOREIGN KEY (issue_id)
                                       REFERENCES issues(id)
                                       ON DELETE CASCADE
);

CREATE INDEX idx_issue_history_issue_id
    ON issue_history (issue_id);

CREATE INDEX idx_issue_history_created_at
    ON issue_history (issue_id, created_at);

CREATE INDEX idx_issue_history_changed_by
    ON issue_history (changed_by_user_id);

