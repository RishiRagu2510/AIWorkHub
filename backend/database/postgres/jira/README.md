# Jira SQL

Use `python3 database/postgres/apply.py --apply` from the backend directory. See [database setup](../README.md).

The schema includes projects and inherited subproject membership, issues and comments, boards, notifications, attachment metadata, preferences, activity, watchers, workflow rules, request replay records, soft deletion and the transactional event outbox. File contents live in object storage.

Scripts `011` onward are repeatable upgrades. `019_runtime_schema.sql` records soft deletion and outbox changes previously missing from the SQL directory. PostgreSQL's built-in `xmin` supplies optimistic concurrency versions; no application version column is required.

`002_seed.sql` and `010_seed.sql` are optional demo fixtures, excluded from the runner. They require an explicit `tenant_id` psql variable and must not be used to restore old demo data after a clean reset.
