CREATE TABLE audit_logs (
                            id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                            tenant_id BIGINT,

                            actor_user_id BIGINT,

                            action VARCHAR(150) NOT NULL,

                            entity_type VARCHAR(100),
                            entity_id BIGINT,

                            source VARCHAR(50),

                            old_values JSONB,
                            new_values JSONB,

                            metadata JSONB NOT NULL DEFAULT '{}'::jsonb,

                            occurred_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_audit_logs_tenant
    ON audit_logs (tenant_id);

CREATE INDEX idx_audit_logs_actor
    ON audit_logs (tenant_id, actor_user_id);

CREATE INDEX idx_audit_logs_entity
    ON audit_logs (entity_type, entity_id);

CREATE INDEX idx_audit_logs_occurred_at
    ON audit_logs (occurred_at);

CREATE TABLE security_events (
                                 id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                 tenant_id BIGINT,

                                 user_id BIGINT,

                                 event_type VARCHAR(100) NOT NULL,

                                 severity VARCHAR(30) NOT NULL DEFAULT 'INFO',

                                 source_ip INET,

                                 user_agent TEXT,

                                 metadata JSONB NOT NULL DEFAULT '{}'::jsonb,

                                 occurred_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                 CONSTRAINT ck_security_events_severity
                                     CHECK (
                                         severity IN (
                                                      'INFO',
                                                      'WARNING',
                                                      'HIGH',
                                                      'CRITICAL'
                                             )
                                         )
);

CREATE INDEX idx_security_events_tenant
    ON security_events (tenant_id);

CREATE INDEX idx_security_events_type
    ON security_events (event_type);

CREATE INDEX idx_security_events_severity
    ON security_events (severity);

CREATE INDEX idx_security_events_occurred
    ON security_events (occurred_at);

CREATE TABLE ai_action_logs (
                                id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                tenant_id BIGINT,

                                user_id BIGINT,

                                agent_run_id VARCHAR(200),

                                action_type VARCHAR(150) NOT NULL,

                                tool_name VARCHAR(200),

                                target_type VARCHAR(100),
                                target_id BIGINT,

                                action_status VARCHAR(30) NOT NULL,

                                approval_request_id BIGINT,

                                input_metadata JSONB,
                                output_metadata JSONB,

                                occurred_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                CONSTRAINT ck_ai_action_logs_status
                                    CHECK (
                                        action_status IN (
                                                          'REQUESTED',
                                                          'ALLOWED',
                                                          'DENIED',
                                                          'APPROVAL_REQUIRED',
                                                          'APPROVED',
                                                          'EXECUTED',
                                                          'FAILED'
                                            )
                                        )
);

CREATE INDEX idx_ai_action_logs_tenant
    ON ai_action_logs (tenant_id);

CREATE INDEX idx_ai_action_logs_agent
    ON ai_action_logs (agent_run_id);

CREATE INDEX idx_ai_action_logs_action
    ON ai_action_logs (action_type);

CREATE INDEX idx_ai_action_logs_occurred
    ON ai_action_logs (occurred_at);

CREATE TABLE data_access_logs (
                                  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                  tenant_id BIGINT,

                                  user_id BIGINT,

                                  service_name VARCHAR(150) NOT NULL,

                                  operation VARCHAR(100) NOT NULL,

                                  resource_type VARCHAR(100) NOT NULL,
                                  resource_id BIGINT,

                                  access_result VARCHAR(30) NOT NULL,

                                  metadata JSONB NOT NULL DEFAULT '{}'::jsonb,

                                  occurred_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                  CONSTRAINT ck_data_access_logs_result
                                      CHECK (
                                          access_result IN (
                                                            'ALLOWED',
                                                            'DENIED'
                                              )
                                          )
);

CREATE INDEX idx_data_access_logs_tenant
    ON data_access_logs (tenant_id);

CREATE INDEX idx_data_access_logs_user
    ON data_access_logs (user_id);

CREATE INDEX idx_data_access_logs_resource
    ON data_access_logs (resource_type, resource_id);

CREATE INDEX idx_data_access_logs_occurred
    ON data_access_logs (occurred_at);

