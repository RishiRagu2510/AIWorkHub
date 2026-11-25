CREATE TABLE documents (
                           id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                           tenant_id BIGINT NOT NULL,

                           source_id BIGINT NOT NULL,

                           external_document_id VARCHAR(500),

                           title VARCHAR(500) NOT NULL,

                           document_type VARCHAR(100),

                           mime_type VARCHAR(200),

                           uri VARCHAR(1000),

                           status VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',

                           checksum VARCHAR(128),

                           metadata JSONB NOT NULL DEFAULT '{}'::jsonb,

                           created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                           updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                           CONSTRAINT fk_documents_source
                               FOREIGN KEY (source_id)
                                   REFERENCES knowledge_sources(id)
                                   ON DELETE CASCADE,

                           CONSTRAINT ck_documents_status
                               CHECK (
                                   status IN (
                                              'ACTIVE',
                                              'ARCHIVED',
                                              'DELETED',
                                              'ERROR'
                                       )
                                   )
);

CREATE INDEX idx_documents_tenant_id
    ON documents (tenant_id);

CREATE INDEX idx_documents_source_id
    ON documents (source_id);

CREATE INDEX idx_documents_external_id
    ON documents (source_id, external_document_id);

CREATE INDEX idx_documents_status
    ON documents (tenant_id, status);

CREATE TABLE document_versions (
                                   id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                   tenant_id BIGINT NOT NULL,

                                   document_id BIGINT NOT NULL,

                                   version_number INTEGER NOT NULL,

                                   content TEXT NOT NULL,

                                   checksum VARCHAR(128),

                                   content_format VARCHAR(30) NOT NULL DEFAULT 'TEXT',

                                   metadata JSONB NOT NULL DEFAULT '{}'::jsonb,

                                   created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                   CONSTRAINT uq_document_versions
                                       UNIQUE (document_id, version_number),

                                   CONSTRAINT fk_document_versions_document
                                       FOREIGN KEY (document_id)
                                           REFERENCES documents(id)
                                           ON DELETE CASCADE,

                                   CONSTRAINT ck_document_versions_number
                                       CHECK (version_number > 0),

                                   CONSTRAINT ck_document_versions_format
                                       CHECK (
                                           content_format IN (
                                                              'TEXT',
                                                              'MARKDOWN',
                                                              'HTML',
                                                              'JSON'
                                               )
                                           )
);

CREATE INDEX idx_document_versions_document_id
    ON document_versions (document_id);

CREATE INDEX idx_document_versions_created_at
    ON document_versions (document_id, created_at);