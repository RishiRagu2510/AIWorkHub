CREATE TABLE boards (
                        id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                        tenant_id BIGINT NOT NULL,
                        project_id BIGINT NOT NULL,

                        name VARCHAR(200) NOT NULL,
                        board_type VARCHAR(30) NOT NULL DEFAULT 'SCRUM',

                        filter_definition JSONB NOT NULL DEFAULT '{}'::jsonb,

                        created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                        CONSTRAINT fk_boards_project
                            FOREIGN KEY (project_id)
                                REFERENCES projects(id)
                                ON DELETE CASCADE,

                        CONSTRAINT ck_boards_type
                            CHECK (
                                board_type IN (
                                               'SCRUM',
                                               'KANBAN'
                                    )
                                )
);

CREATE INDEX idx_boards_project_id
    ON boards (project_id);

CREATE TABLE board_columns (
                               id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                               board_id BIGINT NOT NULL,

                               name VARCHAR(150) NOT NULL,
                               status_code VARCHAR(50) NOT NULL,

                               position INTEGER NOT NULL,

                               created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                               CONSTRAINT fk_board_columns_board
                                   FOREIGN KEY (board_id)
                                       REFERENCES boards(id)
                                       ON DELETE CASCADE,

                               CONSTRAINT uq_board_columns_position
                                   UNIQUE (board_id, position),

                               CONSTRAINT uq_board_columns_status
                                   UNIQUE (board_id, status_code)
);

CREATE INDEX idx_board_columns_board_id
    ON board_columns (board_id);

