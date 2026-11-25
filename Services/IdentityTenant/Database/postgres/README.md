# IdentityTenant SQL

Use `python3 database/postgres/apply.py --apply` from the backend directory. See [database setup](../README.md).

The base schema and migrations include a case-insensitive unique company domain and tenant invitations. `002_seed.sql` installs reference permissions only; it no longer recreates the old development company. Migration `003` supports older schemas, `004` adds invitations, and `005` supplies the session timestamp column required for persistent login. Apply `005` before deploying the persistent-session implementation.

Google identity and email verification happen in the backend. The configured Google OAuth client ID resolves to exactly one enabled IdentityProvider and its tenant. Existing membership or an invitation in that tenant is required; email domains do not authorize Google enrollment. Login never grants roles. Configure tenant roles, permissions, provider mapping and initial administrators explicitly.

## Career catalogue

`011_career_catalog.sql` adds a global, tenant-independent catalogue: `career_profiles`, `grades`, `career_levels`, `skills`, and `career_level_skills`. It seeds exactly the 23 supplied profile names/codes, T0–T6, five .NET levels (T0–T4), five Data Scientist levels (T2–T6), and AI SDLC Engineer at T3 with title “AI SDLC Engineer”. Skills and required/target proficiency mappings are deliberately empty pending authoritative data. User-entered employee skills are not treated as an authoritative global catalogue.

Employees gain nullable `career_profile_id` and `career_level_id`. A composite FK ensures the level belongs to the selected profile; a level without a profile is rejected. Profile-only assignment is allowed for profiles whose levels have not been supplied. Derive title and catalogue grade through CareerLevel and its Grade navigation. Existing employee `grade_id`, `job_title_id`, and tenant-specific references are preserved and are not automatically translated into this catalogue. No employee assignments are inferred or seeded.

Primary keys, code uniqueness, profile/grade uniqueness, level/skill uniqueness, restrictive FK deletes, timestamps, and indexes are present. Prefer deactivating referenced master records. Proficiency columns store integers but no scale or expected values are seeded or inferred. Master data is not duplicated per tenant.

### EF Core migration path

The repository previously had SQL-managed schema upgrades and no EF migration history. Two new migrations establish an EF path without changing historical SQL files:

- `20261001112827_ExistingIdentityTenantBaseline` adopts an existing initialized IdentityTenant database without rewriting it. On an empty database, it uses the existing 001–010 scripts, including reference permissions only.
- `20261001112947_CareerCatalog` executes the same embedded 011 SQL as the SQL deployment path. It is repeatable if SQL deployment has already applied the catalogue.
- The generated model snapshot is checked for pending model changes. The integration tests cover an empty database and a legacy database containing employee data.
- Rollback is intentionally rejected to avoid deleting employee links, career data, or the adopted baseline. Use a reviewed forward migration.

From the backend directory:

```sh
dotnet tool restore
# Set IDENTITY_TENANT_MIGRATION_CONNECTION using your normal secret management.
dotnet ef database update --project services/IdentityTenant/IdentityTenant.Infrastructure
```

The design-time factory does not load application secrets. Alternatively, retain the existing `python3 database/postgres/apply.py --apply` deployment procedure. The SQL and EF paths share the embedded SQL source. No API, authentication, or frontend changes are part of this catalogue addition.

Tests are included in the root solution under `IdentityTenant.Infrastructure.Tests` and require Docker for isolated PostgreSQL containers.

## October 8 completion migrations

- `019_employee_skill_master.sql`: nullable Skill FK for legacy compatibility, unique tenant/employee/master links, and a flexible grade-family field. Reads derive canonical names/categories from the Skill master. New writes require an active master; the legacy name request is accepted only when it resolves unambiguously to an active master.
- `020_career_catalog_completion.sql`: adds missing profiles and H1–H4/HR mappings in both SQL and EF deployment paths, preserving existing profile names and titles. Grade codes and families remain configurable, including management grades.
- `021_normalize_legacy_employee_skills.sql`: creates missing masters from valid legacy names and links employee rows without deleting them. Case-insensitive matching reuses existing masters. Ambiguous duplicate masters, duplicate employee entries, empty/oversized legacy names remain intact for explicit reconciliation. Check `SELECT count(*) FROM employee_skills WHERE skill_id IS NULL` after deployment.

All three migrations are repeatable and have corresponding EF migrations. The model snapshot is tested. Apply migrations before restarting IdentityTenant. Employee setup has master-skill creation/editing/deactivation; employee profiles select these masters. Master changes emit SKILL_UPDATED through the existing employee outbox. Master records are global reference data; employee links and access remain tenant-scoped.

`022_hr_persona_permissions.sql` (EF `20261008000400_HrPersonaPermissions`) creates a tenant HR role where missing and grants all employee permissions, including sensitive records. Persona setup accepts ADMIN or HR, including role management; tenant boundaries and the last-active-administrator safeguard remain enforced. Creating an HR role through setup also grants these permissions. Assign users the HR role explicitly and renew their session to pick up permissions.

`025_employee_onboarding_links.sql` stores tenant-scoped employee/Jira completion receipts, with a composite employee foreign key. `026_hr_onboarding_review.sql` grants HR ISSUE_VIEW and PROJECT_VIEW for request review; existing Jira resource scope still applies. Both have corresponding EF migrations. Configure Identity `Services:Jira:BaseUrl` for authoritative approval verification before employee creation.
