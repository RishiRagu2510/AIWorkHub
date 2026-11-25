BEGIN;
CREATE TABLE IF NOT EXISTS local_credentials (
 user_id BIGINT PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
 password_hash VARCHAR(512) NOT NULL,
 failed_attempts INTEGER NOT NULL DEFAULT 0,
 locked_until TIMESTAMPTZ,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 updated_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS email_verifications (
 token_hash VARCHAR(64) PRIMARY KEY,
 tenant_id BIGINT NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
 email VARCHAR(320) NOT NULL,
 display_name VARCHAR(200) NOT NULL,
 created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
 expires_at TIMESTAMPTZ NOT NULL,
 consumed_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS ix_email_verifications_email_created_at ON email_verifications(email,created_at);
ALTER TABLE user_sessions ADD COLUMN IF NOT EXISTS login_method VARCHAR(20) NOT NULL DEFAULT 'GOOGLE';
COMMIT;
