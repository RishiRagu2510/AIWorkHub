CREATE TABLE knowledge_sources (
                                   id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                   tenant_id BIGINT NOT NULL,

                                   source_type VARCHAR(50) NOT NULL,
                                   name VARCHAR(255) NOT NULL,
                                   description TEXT,

                                   external_system VARCHAR(100),
                                   external_reference VARCHAR(500),

                                   configuration JSONB NOT NULL DEFAULT '{}'::jsonb,

                                   status VARCHAR(30) NOT NULL DEFAULT 'ACTIVE',

                                   created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                   updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                   CONSTRAINT ck_knowledge_sources_type
                                       CHECK (
                                           source_type IN (
                                                           'DOCUMENTATION',
                                                           'JIRA',
                                                           'SERVICE_MANAGEMENT',
                                                           'GITHUB',
                                                           'FILE',
                                                           'URL',
                                                           'MANUAL'
                                               )
                                           ),

                                   CONSTRAINT ck_knowledge_sources_status
                                       CHECK (
                                           status IN (
                                                      'ACTIVE',
                                                      'INACTIVE',
                                                      'ERROR'
                                               )
                                           )
);

CREATE INDEX idx_knowledge_sources_tenant_id
    ON knowledge_sources (tenant_id);

CREATE INDEX idx_knowledge_sources_type
    ON knowledge_sources (tenant_id, source_type);

CREATE INDEX idx_knowledge_sources_status
    ON knowledge_sources (tenant_id, status);

