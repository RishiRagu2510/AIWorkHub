---
plan_id: gridhub-issues-crud
title: GridHub Issues — Full Lifecycle & CRUD Mutation Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5173, file:frontend/gridhub/src/routes.jsx, file:frontend/gridhub/src/pages/IssueEditor.jsx, file:frontend/gridhub/src/pages/IssueDetails.jsx, file:frontend/src/features/issues/IssueForm.jsx, file:frontend/src/features/comments/IssueComments.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: c420118831f77c03
---

# GridHub Issues — Full Lifecycle & CRUD Mutation Test Plan

## 1. Overview
- **Objective:** Verify end-to-end mutation and lifecycle workflows for issues in GridHub (`https://localhost:5173`) using dedicated test user credentials (`qa.test@griddynamics.com`), testing issue validation, creation, detail inspection, status transition, commenting, editing, Kanban board rendering, and deletion.
- **In scope:**
  - Form validation on `/issues/create` (submitting without required project and summary).
  - Issue creation with unique summary timestamp, description, priority, and task type.
  - Redirect to `/issues/:id` and confirmation banner `Issue created.`.
  - Issue detail page verification (title, project, status `TO DO`, priority, and description).
  - Issue status transition (e.g. `TO DO` to `IN PROGRESS`), checking confirmation and badge updates.
  - Adding a new comment to the issue timeline, checking confirmation and comment persistence.
  - Editing issue summary via `/issues/:id/edit`, verifying update persistence.
  - Verifying the issue appears in the `/issues` data table list.
  - Verifying the issue card appears in the Kanban board view (`/issues?View=board`).
  - Deleting the issue with confirmation dialog, checking redirect to `/issues` and deletion confirmation.
- **Out of scope:**
  - Multi-tenant cross-organization issue syncing.
  - Direct third-party Jira Cloud webhooks.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded with `ADMIN` role.
  - Backend services running: ApiGateway (`5204`), IdentityTenant (`5233`), PostgreSQL (`5432`).
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Prevent issue creation and display validation feedback when required fields (Project, Summary) are missing | Functional / Validation | IssueForm.jsx | Alerts on missing project / summary |
| R-02 | Create a new task issue with valid project, summary, description, and priority, redirecting to `/issues/:id` | Functional / Mutation | IssueEditor.jsx, IssueForm.jsx | Notice: "Issue created." |
| R-03 | Render issue detail page with correct title, metadata panel (Status `TO DO`, Priority, Project), and description | UI / Detail | IssueDetails.jsx | Key, Summary, Status, Details panel |
| R-04 | Transition issue status using workflow state transition form and verify status update notice | Functional / Mutation | IssueDetails.jsx | Notice: "Issue status updated." |
| R-05 | Add a comment to the issue and verify timeline rendering and notice | Functional / Mutation | IssueComments.jsx | Notice: "Comment added." |
| R-06 | Navigate to `/issues/:id/edit`, modify summary, save changes, and verify update notice and detail heading | Functional / Mutation | IssueEditor.jsx | Notice: "Issue updated." |
| R-07 | Locate the updated issue in the `/issues` list view via search filter | UI / Search | Issues.jsx | Filter by unique keyword |
| R-08 | Locate the updated issue card in the `/issues?View=board` Kanban view in its active column | UI / Kanban | Issues.jsx, IssueBoard.jsx | Card visible in status column |
| R-09 | Delete the issue using action header delete button and confirmation dialog | Functional / Mutation | IssueDetails.jsx | Notice: "Issue deleted.", redirects to `/issues` |
| R-10 | Verify the deleted issue is purged and no longer appears in the `/issues` search results | Functional / Verification | Issues.jsx | Assert empty search results |

## 3. Scenarios & Test Cases
### Scenario 1: Form Validation on Empty Creation (R-01)
- **Tag:** `@functional @validation`
- **Given:** Authenticated user navigates to `/issues/create`.
- **When:** Submits form without selecting project or entering summary.
- **Then:** Validation alert appears indicating required fields.

### Scenario 2: Create New Issue (R-02)
- **Tag:** `@functional @mutation @smoke`
- **Given:** User on `/issues/create`.
- **When:** Selects first project, enters unique summary `E2E-Task-[timestamp]`, description, priority `HIGH`, and clicks `Create issue`.
- **Then:** Redirects to `/issues/:id` with alert `Issue created.`.

### Scenario 3: Verify Issue Details (R-03)
- **Tag:** `@ui @detail`
- **Given:** User on newly created issue page `/issues/:id`.
- **Then:** Page title matches summary, metadata shows status `TO DO`, priority `HIGH`.

### Scenario 4: Transition Status (R-04)
- **Tag:** `@functional @mutation`
- **Given:** User on issue details page.
- **When:** Selects status transition and clicks `Transition issue`.
- **Then:** Notice `Issue status updated.` appears and status updates.

### Scenario 5: Add Comment (R-05)
- **Tag:** `@functional @mutation`
- **Given:** User on issue details page comments section.
- **When:** Enters comment body and clicks `Comment`.
- **Then:** Notice `Comment added.` appears and comment body is visible in list.

### Scenario 6: Edit Issue (R-06)
- **Tag:** `@functional @mutation`
- **Given:** User on issue details page.
- **When:** Clicks `Edit issue`, appends `-Updated` to summary, and clicks `Save changes`.
- **Then:** Notice `Issue updated.` appears and heading updates.

### Scenario 7: Search in Issues List (R-07)
- **Tag:** `@ui @search`
- **Given:** User navigates to `/issues`.
- **When:** Filters search by unique summary keyword.
- **Then:** Issue row is displayed in table.

### Scenario 8: View on Kanban Board (R-08)
- **Tag:** `@ui @kanban`
- **Given:** User on `/issues`.
- **When:** Switches to `Board view`.
- **Then:** Issue card is visible in active status column.

### Scenario 9: Delete Issue (R-09)
- **Tag:** `@functional @mutation @cleanup`
- **Given:** User on issue details page.
- **When:** Clicks `Delete`, confirms deletion in modal dialog.
- **Then:** Notice `Issue deleted.` appears and user is redirected to `/issues`.

### Scenario 10: Verify Deletion (R-10)
- **Tag:** `@functional @verification`
- **Given:** User on `/issues`.
- **When:** Searches for deleted issue summary.
- **Then:** Issue is not found in table.
