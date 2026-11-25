

BEGIN;
ALTER TABLE tenants ADD COLUMN IF NOT EXISTS email_domain VARCHAR(253);
CREATE UNIQUE INDEX IF NOT EXISTS ux_tenants_email_domain ON tenants (LOWER(email_domain));
COMMIT;
