CREATE TABLE document_chunks (
                                 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                 tenant_id BIGINT NOT NULL,

                                 document_version_id BIGINT NOT NULL,

                                 chunk_index INTEGER NOT NULL,

                                 content TEXT NOT NULL,

                                 token_count INTEGER,

                                 start_offset INTEGER,
                                 end_offset INTEGER,

                                 metadata JSONB NOT NULL DEFAULT '{}'::jsonb,

                                 created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                 CONSTRAINT uq_document_chunks_version_index
                                     UNIQUE (document_version_id, chunk_index),

                                 CONSTRAINT fk_document_chunks_version
                                     FOREIGN KEY (document_version_id)
                                         REFERENCES document_versions(id)
                                         ON DELETE CASCADE,

                                 CONSTRAINT ck_document_chunks_index
                                     CHECK (chunk_index >= 0),

                                 CONSTRAINT ck_document_chunks_token_count
                                     CHECK (
                                         token_count IS NULL
                                             OR token_count >= 0
                                         )
);

CREATE INDEX idx_document_chunks_version_id
    ON document_chunks (document_version_id);

CREATE INDEX idx_document_chunks_tenant_id
    ON document_chunks (tenant_id);