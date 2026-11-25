# API endpoint verification — 25 November 2026

Local solution verification completed using .NET 10, ASP.NET Core TestServer, and isolated disposable PostgreSQL databases. No application database was changed.

**1,931 tests passed; 0 failed; 0 skipped.**

| Test project | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Jira.Application.Tests | 4 | 0 | 0 |
| Jira.Infrastructure.Tests | 12 | 0 | 0 |
| Audit.Infrastructure.Tests | 6 | 0 | 0 |
| Audit.Api.Tests | 3 | 0 | 0 |
| ApiGateway.Tests | 22 | 0 | 0 |
| IdentityTenant.Infrastructure.Tests | 23 | 0 | 0 |
| AIAgent.Application.Tests | 1130 | 0 | 0 |
| IdentityTenant.Application.Tests | 18 | 0 | 0 |
| Backend.Authorization.Tests | 713 | 0 | 0 |

## Endpoint coverage

All 161 supplied HTTP method/path combinations are registered. The inventory tests also send anonymous requests and require 401 for endpoints declaring authorization. Public endpoints have registration checks in the inventory; authentication behavior is tested separately. Upload probes use multipart content.

The existing suites exercise authenticated CRUD, response contracts, permission enforcement, tenant isolation, version conflicts, assignment eligibility, employee setup and career sections, leave decisions, planning, collaboration, attachments, work logs, timesheet approval/rejection, notifications, and audit behavior. Inventory success is not a claim that every possible payload or branch has been tested.

Additional checks cover the full HTTP password registration → verification → login → current user → token → logout lifecycle, rejected verification-token reuse, incorrect passwords, and rejection of a revoked session cookie. Board WebSocket tests verify a successful stream and rejection of another tenant. Existing tests verify issue and notification WebSocket behavior.

**URL correction:** use `/hubs/boards/{boardId}`. The supplied `/hubs/boards` URL is incomplete. `/hubs/issues` and `/hubs/notifications` are correct. All three valid hub URLs reject anonymous negotiation.

## Repairs made

- Restored missing NuGet packages so the Audit dependency and API test project could build.
- Fixed two test-host cookie registration calls after `ConfigureCookie` gained an optional parameter.
- Corrected the career-catalog test fixture: verify that migration 024 clears catalog data for an empty organization, then explicitly load the catalog data required by those tests.
- Corrected the employee outbox test to register `PersonaDbContext`, which the publisher now requires.
- Added the endpoint inventory, HTTP authentication lifecycle, and board-stream regression tests.

No production endpoint implementation changes were needed for the verified scenarios.

## Limits

This is local automated verification, not a smoke test of a deployed environment. Google identity-provider success uses simulated validated credentials; live Google OAuth and real SMTP delivery were not exercised. Photo/attachment storage uses test implementations in the API suite, and message delivery uses test transports where applicable. Gateway tests verify routing with a test upstream; they do not prove the deployed gateway, service configuration, storage, broker, or network is healthy. No load or soak testing was performed. Existing nullable-reference and test-analyzer warnings remain in a clean compilation.

## Reproduce

Docker must be running.

```sh
dotnet restore AIWorkHub.sln --disable-parallel
dotnet test AIWorkHub.sln --no-restore -m:1 --logger 'trx;LogFilePrefix=endpoint-audit' --results-directory /private/tmp/aiworkhub-endpoint-audit
```

TRX files and `test-run.log` alongside this report contain the final run evidence.

## HTTP inventory

Each row passed route registration and, where authorization is declared, anonymous rejection. See the workflow suites for authenticated behavior; this table does not represent 161 successful authenticated CRUD operations.

| Method | Path | Inventory result |
|---|---|---|
| POST | `/api/auth/password/login` | Passed |
| POST | `/api/auth/password/register` | Passed |
| POST | `/api/auth/password/verify` | Passed |
| POST | `/api/auth/google` | Passed |
| GET | `/api/auth/google/configuration` | Passed |
| GET | `/api/auth/me` | Passed |
| GET | `/api/auth/context` | Passed |
| POST | `/api/auth/token` | Passed |
| GET | `/api/auth/token-dev` | Passed |
| POST | `/api/auth/logout` | Passed |
| GET | `/api/auth/permission-test` | Passed |
| GET | `/api/users` | Passed |
| GET | `/api/users/{userId}` | Passed |
| GET | `/api/users/{userId}/assignment-eligibility` | Passed |
| GET | `/api/users/{id}/collaboration-eligibility` | Passed |
| GET | `/api/users/timesheet-approvers` | Passed |
| GET | `/api/tenants/me` | Passed |
| GET | `/api/tenants/{tenantId}` | Passed |
| GET | `/api/invitations` | Passed |
| POST | `/api/invitations` | Passed |
| DELETE | `/api/invitations/{id}` | Passed |
| GET | `/api/employees` | Passed |
| POST | `/api/employees` | Passed |
| GET | `/api/employees/{id}` | Passed |
| PUT | `/api/employees/{id}` | Passed |
| DELETE | `/api/employees/{id}` | Passed |
| GET | `/api/employees/me` | Passed |
| PUT | `/api/employees/me/profile` | Passed |
| GET | `/api/employees/org-chart` | Passed |
| GET | `/api/employees/manager-options` | Passed |
| GET | `/api/employees/login-users` | Passed |
| GET | `/api/employees/{id}/career-history` | Passed |
| POST | `/api/employees/{id}/photo` | Passed |
| GET | `/api/employees/{id}/photo/{image}` | Passed |
| GET | `/api/employees/absence` | Passed |
| POST | `/api/employees/absence` | Passed |
| GET | `/api/employees/absence/policy` | Passed |
| PUT | `/api/employees/absence/policy` | Passed |
| POST | `/api/employees/absence/{id}/decision` | Passed |
| GET | `/api/employees/career-catalog` | Passed |
| GET | `/api/employees/references` | Passed |
| POST | `/api/employees/references` | Passed |
| PUT | `/api/employees/references/{id}` | Passed |
| GET | `/api/employees/setup` | Passed |
| GET | `/api/employees/setup/users` | Passed |
| POST | `/api/employees/setup/users/{userId}/roles` | Passed |
| PUT | `/api/employees/setup/users/{userId}/roles` | Passed |
| POST | `/api/employees/setup/{kind}` | Passed |
| DELETE | `/api/employees/setup/{kind}` | Passed |
| PUT | `/api/employees/setup/{kind}/{id}` | Passed |
| DELETE | `/api/employees/setup/{kind}/{id}` | Passed |
| GET | `/api/projects` | Passed |
| POST | `/api/projects` | Passed |
| GET | `/api/projects/{projectId}` | Passed |
| PUT | `/api/projects/{projectId}` | Passed |
| DELETE | `/api/projects/{projectId}` | Passed |
| PUT | `/api/projects/{projectId}/parent` | Passed |
| GET | `/api/projects/{projectId}/subprojects` | Passed |
| POST | `/api/projects/{projectId}/subprojects` | Passed |
| GET | `/api/projects/by-user/{userId}` | Passed |
| GET | `/api/projects/context` | Passed |
| GET | `/api/projects/accounts` | Passed |
| POST | `/api/projects/accounts` | Passed |
| PUT | `/api/projects/accounts/{id}` | Passed |
| DELETE | `/api/projects/accounts/{id}` | Passed |
| GET | `/api/projects/{projectId}/members` | Passed |
| POST | `/api/projects/{projectId}/members` | Passed |
| DELETE | `/api/projects/{projectId}/members/{userId}` | Passed |
| GET | `/api/projects/{projectId}/members/effective` | Passed |
| GET | `/api/projects/{projectId}/assignment-members` | Passed |
| GET | `/api/projects/allocations` | Passed |
| POST | `/api/projects/allocations` | Passed |
| DELETE | `/api/projects/allocations/{id}` | Passed |
| GET | `/api/issues/search` | Passed |
| POST | `/api/issues` | Passed |
| GET | `/api/issues/{issueId}` | Passed |
| PUT | `/api/issues/{issueId}` | Passed |
| DELETE | `/api/issues/{issueId}` | Passed |
| GET | `/api/issues/key/{issueKey}` | Passed |
| GET | `/api/issues/project/{projectId}` | Passed |
| PUT | `/api/issues/{issueId}/assignee` | Passed |
| PUT | `/api/issues/{issueId}/transition` | Passed |
| GET | `/api/boards` | Passed |
| POST | `/api/boards` | Passed |
| GET | `/api/boards/{id}` | Passed |
| GET | `/api/boards/{boardId}/backlog` | Passed |
| GET | `/api/boards/{boardId}/sprints` | Passed |
| POST | `/api/boards/{boardId}/sprints` | Passed |
| PUT | `/api/boards/{boardId}/issues/{issueId}/placement` | Passed |
| POST | `/api/sprints/{id}/start` | Passed |
| POST | `/api/sprints/{id}/complete` | Passed |
| GET | `/api/projects/{projectId}/components` | Passed |
| POST | `/api/projects/{projectId}/components` | Passed |
| PUT | `/api/projects/{projectId}/components/{id}` | Passed |
| GET | `/api/projects/{projectId}/versions` | Passed |
| POST | `/api/projects/{projectId}/versions` | Passed |
| PUT | `/api/projects/{projectId}/versions/{id}` | Passed |
| GET | `/api/projects/{projectId}/labels` | Passed |
| GET | `/api/projects/{id}/workflow` | Passed |
| PUT | `/api/projects/{id}/workflow` | Passed |
| GET | `/api/issues/{issueId}/comments` | Passed |
| POST | `/api/issues/{issueId}/comments` | Passed |
| PUT | `/api/issues/{issueId}/comments/{commentId}` | Passed |
| DELETE | `/api/issues/{issueId}/comments/{commentId}` | Passed |
| GET | `/api/issues/{issueId}/attachments` | Passed |
| POST | `/api/issues/{issueId}/attachments` | Passed |
| GET | `/api/issues/{issueId}/attachments/{id}/download` | Passed |
| DELETE | `/api/issues/{issueId}/attachments/{id}` | Passed |
| GET | `/api/issues/{id}/watchers` | Passed |
| PUT | `/api/issues/{id}/watchers/me` | Passed |
| DELETE | `/api/issues/{id}/watchers/me` | Passed |
| GET | `/api/issues/{id}/activity` | Passed |
| GET | `/api/activity` | Passed |
| GET | `/api/worklogs` | Passed |
| POST | `/api/worklogs` | Passed |
| GET | `/api/worklogs/{id}` | Passed |
| PUT | `/api/worklogs/{id}` | Passed |
| DELETE | `/api/worklogs/{id}` | Passed |
| GET | `/api/timesheets/me` | Passed |
| GET | `/api/timesheets/summary` | Passed |
| POST | `/api/timesheets/submit` | Passed |
| POST | `/api/timesheets/{id}/submit` | Passed |
| GET | `/api/timesheets/team` | Passed |
| GET | `/api/timesheets/report` | Passed |
| GET | `/api/timesheets/hierarchy` | Passed |
| GET | `/api/timesheets/{periodId}/approvers` | Passed |
| POST | `/api/timesheets/approve` | Passed |
| POST | `/api/timesheets/reject` | Passed |
| POST | `/api/timesheets/{id}/approve` | Passed |
| POST | `/api/timesheets/{id}/reject` | Passed |
| GET | `/api/preferences` | Passed |
| PUT | `/api/preferences/{key}` | Passed |
| GET | `/api/notifications` | Passed |
| GET | `/api/notifications/unread-count` | Passed |
| PUT | `/api/notifications/{id}/read` | Passed |
| POST | `/api/notifications/read-all` | Passed |
| GET | `/api/audit/logs` | Passed |
| GET | `/api/employees/{id}/skills` | Passed |
| POST | `/api/employees/{id}/skills` | Passed |
| PUT | `/api/employees/{id}/skills/{entryId}` | Passed |
| DELETE | `/api/employees/{id}/skills/{entryId}` | Passed |
| GET | `/api/employees/{id}/education` | Passed |
| POST | `/api/employees/{id}/education` | Passed |
| PUT | `/api/employees/{id}/education/{entryId}` | Passed |
| DELETE | `/api/employees/{id}/education/{entryId}` | Passed |
| GET | `/api/employees/{id}/certifications` | Passed |
| POST | `/api/employees/{id}/certifications` | Passed |
| PUT | `/api/employees/{id}/certifications/{entryId}` | Passed |
| DELETE | `/api/employees/{id}/certifications/{entryId}` | Passed |
| GET | `/api/employees/{id}/experience` | Passed |
| POST | `/api/employees/{id}/experience` | Passed |
| PUT | `/api/employees/{id}/experience/{entryId}` | Passed |
| DELETE | `/api/employees/{id}/experience/{entryId}` | Passed |
| GET | `/api/employees/{id}/languages` | Passed |
| POST | `/api/employees/{id}/languages` | Passed |
| PUT | `/api/employees/{id}/languages/{entryId}` | Passed |
| DELETE | `/api/employees/{id}/languages/{entryId}` | Passed |
| GET | `/api/employees/{id}/compensation` | Passed |
| POST | `/api/employees/{id}/compensation` | Passed |
| PUT | `/api/employees/{id}/compensation/{entryId}` | Passed |
| DELETE | `/api/employees/{id}/compensation/{entryId}` | Passed |
