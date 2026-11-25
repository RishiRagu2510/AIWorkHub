CREATE TABLE embeddings (
                            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                            tenant_id BIGINT NOT NULL,

                            document_chunk_id BIGINT NOT NULL,

                            embedding_model VARCHAR(200) NOT NULL,

                            embedding_provider VARCHAR(100),

                            embedding_dimension INTEGER NOT NULL,

                            embedding vector NOT NULL,

                            created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                            CONSTRAINT fk_embeddings_chunk
                                FOREIGN KEY (document_chunk_id)
                                    REFERENCES document_chunks(id)
                                    ON DELETE CASCADE,

                            CONSTRAINT ck_embeddings_dimension
                                CHECK (embedding_dimension > 0)
);

CREATE INDEX idx_embeddings_chunk_id
    ON embeddings (document_chunk_id);

CREATE INDEX idx_embeddings_model
    ON embeddings (
                   tenant_id,
                   embedding_model
        );