CREATE TABLE notifications (
                               id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                               tenant_id BIGINT NOT NULL,

                               user_id BIGINT NOT NULL,

                               notification_type VARCHAR(100) NOT NULL,

                               title VARCHAR(500) NOT NULL,
                               message TEXT NOT NULL,

                               entity_type VARCHAR(100),
                               entity_id BIGINT,

                               channel VARCHAR(30) NOT NULL DEFAULT 'IN_APP',

                               read_at TIMESTAMPTZ,

                               created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                               CONSTRAINT ck_notifications_channel
                                   CHECK (
                                       channel IN (
                                                   'IN_APP',
                                                   'EMAIL',
                                                   'WEBHOOK'
                                           )
                                       )
);

CREATE INDEX idx_notifications_user
    ON notifications (tenant_id, user_id);

CREATE INDEX idx_notifications_unread
    ON notifications (tenant_id, user_id, read_at);

CREATE INDEX idx_notifications_created
    ON notifications (created_at);

CREATE TABLE notification_preferences (
                                          id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                          tenant_id BIGINT NOT NULL,

                                          user_id BIGINT NOT NULL,

                                          notification_type VARCHAR(100) NOT NULL,

                                          channel VARCHAR(30) NOT NULL,

                                          enabled BOOLEAN NOT NULL DEFAULT TRUE,

                                          created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                          updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                          CONSTRAINT uq_notification_preferences
                                              UNIQUE (
                                                      tenant_id,
                                                      user_id,
                                                      notification_type,
                                                      channel
                                                  )
);

CREATE INDEX idx_notification_preferences_user
    ON notification_preferences (tenant_id, user_id);

CREATE TABLE notification_templates (
                                        id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                        tenant_id BIGINT NOT NULL,

                                        code VARCHAR(100) NOT NULL,
                                        name VARCHAR(200) NOT NULL,

                                        channel VARCHAR(30) NOT NULL,

                                        subject_template TEXT,
                                        body_template TEXT NOT NULL,

                                        active BOOLEAN NOT NULL DEFAULT TRUE,

                                        created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
                                        updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                        CONSTRAINT uq_notification_templates
                                            UNIQUE (tenant_id, code, channel),

                                        CONSTRAINT ck_notification_templates_channel
                                            CHECK (
                                                channel IN (
                                                            'IN_APP',
                                                            'EMAIL',
                                                            'WEBHOOK'
                                                    )
                                                )
);

CREATE TABLE notification_deliveries (
                                         id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

                                         tenant_id BIGINT NOT NULL,

                                         notification_id BIGINT NOT NULL,

                                         channel VARCHAR(30) NOT NULL,

                                         status VARCHAR(30) NOT NULL DEFAULT 'QUEUED',

                                         delivered_at TIMESTAMPTZ,

                                         error_message TEXT,

                                         retry_count INTEGER NOT NULL DEFAULT 0,

                                         created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,

                                         CONSTRAINT fk_notification_deliveries_notification
                                             FOREIGN KEY (notification_id)
                                                 REFERENCES notifications(id)
                                                 ON DELETE CASCADE,

                                         CONSTRAINT ck_notification_deliveries_channel
                                             CHECK (
                                                 channel IN (
                                                             'IN_APP',
                                                             'EMAIL',
                                                             'WEBHOOK'
                                                     )
                                                 ),

                                         CONSTRAINT ck_notification_deliveries_status
                                             CHECK (
                                                 status IN (
                                                            'QUEUED',
                                                            'SENT',
                                                            'FAILED',
                                                            'CANCELLED'
                                                     )
                                                 ),

                                         CONSTRAINT ck_notification_deliveries_retry
                                             CHECK (retry_count >= 0)
);

CREATE INDEX idx_notification_deliveries_notification
    ON notification_deliveries (notification_id);

CREATE INDEX idx_notification_deliveries_status
    ON notification_deliveries (tenant_id, status);

