# PostgreSQL setup and upgrades

Run from the backend directory with the local PostgreSQL Docker container running:

```sh
python3 database/postgres/apply.py          # validate in disposable databases only
python3 database/postgres/apply.py --apply  # validate, then apply to application databases
```

The runner uses `psql` with `ON_ERROR_STOP`, validates all five schemas in disposable databases first, checks upgrade repeatability, and removes its temporary databases. It applies baseline scripts only to empty schemas and additive upgrades to existing schemas. It checks that every expected table and column exists afterward. It does not reset data or identity sequences. A partial baseline is rejected rather than silently skipped. Database creation is handled separately by `init/00_create_databases.sql`; the runner expects the five application databases to exist.

`identity_tenant/002_seed.sql` now contains only the global permission catalog and is safe to rerun. It does not create a company, user, role or role assignment. All other seed scripts are excluded from the runner.

After a complete data reset, configure a new active tenant, its optional company email domain, enabled Google provider, tenant roles and role permissions, and initial administrator before signing in. New verified Google accounts matching a registered active company domain receive DEVELOPER; that role must already exist for the tenant. Invitations and existing memberships are also supported. An empty organization database cannot accept sign-ins.

## Optional demo fixtures

`examples/identity_demo.sql` preserves the previous development tenant and role fixture for explicit use only. It does not configure a real Google provider or domain and is not a production bootstrap.

Jira demo scripts require the actual tenant ID instead of assuming ID 1:

```sh
docker exec -i aiworkhub-postgres psql -X -U aiworkhub -d jira_db -v ON_ERROR_STOP=1 -v tenant_id=YOUR_TENANT_ID < database/postgres/jira/002_seed.sql
docker exec -i aiworkhub-postgres psql -X -U aiworkhub -d jira_db -v ON_ERROR_STOP=1 -v tenant_id=YOUR_TENANT_ID < database/postgres/jira/010_seed.sql
```

Run these only after verifying the tenant exists in IdentityTenant. Cross-service tenant IDs cannot be checked by a PostgreSQL foreign key.


## Explicit local reset

`python3 database/postgres/reset_local.py --delete-all-and-bootstrap` permanently resets
all five named data volumes owned by the local `aiworkhub` Docker Compose project.
It stops the four backend writers, clears PostgreSQL/MongoDB/Redis/Kafka/MinIO,
rebuilds all five PostgreSQL schemas, restores reference permissions and the career catalogue,
and applies `setup/grid_dynamics.sql`. Only the Grid Dynamics tenant and initial Google
administrator are bootstrapped; no employee, project, issue, time, or leave records are restored.
Numeric generators are reset to the next valid value: 1 for empty tables, max(ID)+1 for
seeded tables. UUID identifiers are unchanged in format. OAuth configuration is preserved;
JWT keys are rotated and stored sessions are removed before services restart.
Run only after explicit authorization to delete all local application data.
