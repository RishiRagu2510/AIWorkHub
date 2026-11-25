CREATE TABLE ingestion_jobs (
                                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                tenant_id BIGINT NOT NULL,

                                source_id BIGINT,

                                document_id BIGINT,

                                job_type VARCHAR(50) NOT NULL,

                                status VARCHAR(30) NOT NULL DEFAULT 'QUEUED',

                                started_at TIMESTAMPTZ,
                                completed_at TIMESTAMPTZ,

                                documents_processed INTEGER NOT NULL DEFAULT 0,
                                chunks_created INTEGER NOT NULL DEFAULT 0,
                                embeddings_created INTEGER NOT NULL DEFAULT 0,

                                error_message TEXT,

                                metadata JSONB NOT NULL DEFAULT '{}'::jsonb,

                                created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                CONSTRAINT fk_ingestion_jobs_source
                                    FOREIGN KEY (source_id)
                                        REFERENCES knowledge_sources(id)
                                        ON DELETE SET NULL,

                                CONSTRAINT fk_ingestion_jobs_document
                                    FOREIGN KEY (document_id)
                                        REFERENCES documents(id)
                                        ON DELETE SET NULL,

                                CONSTRAINT ck_ingestion_jobs_type
                                    CHECK (
                                        job_type IN (
                                                     'FULL_SYNC',
                                                     'INCREMENTAL_SYNC',
                                                     'DOCUMENT_INGESTION',
                                                     'REINDEX'
                                            )
                                        ),

                                CONSTRAINT ck_ingestion_jobs_status
                                    CHECK (
                                        status IN (
                                                   'QUEUED',
                                                   'RUNNING',
                                                   'COMPLETED',
                                                   'FAILED',
                                                   'CANCELLED'
                                            )
                                        )
);

CREATE INDEX idx_ingestion_jobs_tenant_id
    ON ingestion_jobs (tenant_id);

CREATE INDEX idx_ingestion_jobs_source_id
    ON ingestion_jobs (source_id);

CREATE INDEX idx_ingestion_jobs_status
    ON ingestion_jobs (tenant_id, status);

CREATE INDEX idx_ingestion_jobs_created_at
    ON ingestion_jobs (created_at);