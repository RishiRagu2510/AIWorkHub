
CREATE TABLE IF NOT EXISTS tenant_invitations (
 id uuid PRIMARY KEY, tenant_id bigint NOT NULL REFERENCES tenants(id), email varchar(320) NOT NULL,
 role_code varchar(50) NOT NULL DEFAULT 'DEVELOPER' CHECK (role_code='DEVELOPER'),
 invited_by_user_id bigint NOT NULL, created_at timestamptz NOT NULL,
 expires_at timestamptz NOT NULL, accepted_at timestamptz, revoked_at timestamptz
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_tenant_invitations_pending_email
 ON tenant_invitations(email) WHERE accepted_at IS NULL AND revoked_at IS NULL;
