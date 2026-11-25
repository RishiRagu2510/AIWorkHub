




ALTER TABLE documents ADD COLUMN IF NOT EXISTS allowed_roles TEXT[] NOT NULL DEFAULT '{}';


ALTER TABLE documents ADD COLUMN IF NOT EXISTS uploaded_by BIGINT;


CREATE UNIQUE INDEX IF NOT EXISTS uq_documents_uploaded_title
    ON documents (source_id, lower(title))
    WHERE uploaded_by IS NOT NULL;


CREATE UNIQUE INDEX IF NOT EXISTS uq_knowledge_sources_company_documents
    ON knowledge_sources (tenant_id)
    WHERE source_type = 'FILE' AND name = 'Company documents';


ALTER TABLE document_chunks
    ADD COLUMN IF NOT EXISTS search_vector tsvector
    GENERATED ALWAYS AS (to_tsvector('english', content)) STORED;

CREATE INDEX IF NOT EXISTS idx_document_chunks_search
    ON document_chunks USING GIN (search_vector);
