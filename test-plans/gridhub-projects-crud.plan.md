---
plan_id: gridhub-projects-crud
title: GridHub Projects — Lifecycle, CRUD, Hierarchy & Governance Test Plan
version: 1.0
status: Approved
created: 2026-11-19
source: [url:https://localhost:5173/projects, file:frontend/gridhub/src/pages/Projects.jsx, file:frontend/src/features/projects/ProjectCreateDialog.jsx, file:frontend/src/features/projects/ProjectManageDialog.jsx, file:frontend/src/features/projects/AccountField.jsx]
target_env: local
ui_verified: 2026-11-19 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-19
approved_hash: 39b046d05e8ec539
---

# GridHub Projects — Lifecycle, CRUD, Hierarchy & Governance Test Plan

## 1. Overview
- **Objective:** Verify end-to-end interactive mutation and governance workflows for projects in GridHub (`https://localhost:5173/projects`) using dedicated test user credentials (`qa.test@griddynamics.com`), testing project validation, modal dialog creation, portfolio stats ribbon, data table rendering, search/filters, hierarchy view toggle, project editing, and safe deletion.
- **In scope:**
  - Form validation on "New project" modal dialog (empty account, invalid key syntax).
  - Project creation with unique timestamped key, account name, type, and description.
  - Confirmation notice display (`Project “{key}” created in {account}.`) and modal closure.
  - Verification of newly created project in directory table (Key link, Account avatar badge, Type badge, Active status).
  - Increment verification on projects stats ribbon (`{N} projects` and `{N} active`).
  - Search filter verification by unique key in search input (`[name="projectSearch"]`).
  - View switcher verification between Flat List and Hierarchy tree layouts.
  - Project modification via "Edit" row action (updating description, project type to `BANKING`), verifying table reflection.
  - Project deletion via "Delete" row action modal (`button.danger:has-text("Delete project")`), checking confirmation notice (`Project deleted.`).
  - Purge verification ensuring deleted project is completely removed from directory and search.
- **Out of scope:**
  - Multi-tenant cross-organization project isolation.
  - Direct third-party Jira Cloud webhooks.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded with `ADMIN` role.
  - Backend services running: ApiGateway (`5204`), IdentityTenant (`5233`), PostgreSQL (`5432`).
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Clicking "New project" opens modal dialog with Account and Project Key inputs | Functional | Projects.jsx | Admin role required |
| R-02 | Form validation blocks submission when required fields are empty or key has invalid format | Functional | ProjectCreateDialog.jsx | Regex `/^[A-Za-z][A-Za-z0-9_ -]{0,999}$/` |
| R-03 | Valid project creation closes modal, triggers notice banner, and persists project | Functional | ProjectCreateDialog.jsx | `useCreateProjectMutation` |
| R-04 | Projects directory data table renders newly created project with Account initials and Type badge | Functional | Projects.jsx | Table row inspection |
| R-05 | Projects stats ribbon reflects incremented total and active project counts | Functional | Projects.jsx | Header ribbon metrics |
| R-06 | Search input dynamically filters directory table to matching project key | Functional | Projects.jsx | `[name="projectSearch"]` |
| R-07 | View switcher toggles between List and Hierarchy presentation modes | Functional | Projects.jsx | `button:has-text("Hierarchy")` |
| R-08 | Edit action opens governance modal and saves updated metadata (Description & Type) | Functional | ProjectManageDialog.jsx | `mode="edit"` |
| R-09 | Delete action opens confirmation modal and permanently deletes unused project | Functional | ProjectManageDialog.jsx | `mode="delete"` |
| R-10 | Deleted project is immediately purged from data table and search results | Functional | Projects.jsx | Deletion verification |

## 3. Scenarios

### GH-PROJ-01: Validation — Prevent submission when required fields are empty
- **Goal:** Verify "New project" dialog enforces required field constraints.
- **Preconditions:** Authenticated as test user on `https://localhost:5173/projects`.
- **Steps:**
  1. Click "New project" button (`button.primary:has-text("New project")`).
  2. In dialog, leave Account and Project key inputs empty.
  3. Click "Create project" submit button (`button.btn-create`).
  4. Verify error prompt is displayed and form is not submitted.
  5. Close dialog via cancel button.
- **Expected Result:** Validation prevents empty submission; modal remains open until cancelled.

### GH-PROJ-02: Validation — Reject invalid project key syntax
- **Goal:** Verify project key regex rule rejects keys starting with numeric digits or invalid symbols.
- **Preconditions:** "New project" dialog opened.
- **Steps:**
  1. Click "New project" button.
  2. Enter valid Account name: `QA Test Account`.
  3. Enter invalid Project key: `123-INVALID`.
  4. Click "Create project" submit button.
  5. Inspect error message element `#project-key-error`.
- **Expected Result:** Displays `Use up to 1000 characters, starting with a letter. Spaces, numbers, underscores, and hyphens are allowed.`.

### GH-PROJ-03: Creation — Create new standalone project with valid metadata
- **Goal:** Successfully create a unique project via modal dialog.
- **Preconditions:** Authenticated as test user on `https://localhost:5173/projects`.
- **Steps:**
  1. Click "New project" button.
  2. Fill Account name: `E2E-Acc-{proj}-{timestamp}`.
  3. Fill Project key: `PROJ-{timestamp}`.
  4. Select Project Type: `Software`.
  5. Fill Description: `E2E automated test project for GridHub projects lifecycle`.
  6. Click "Create project" button.
  7. Await modal close and notice banner `Project “PROJ-{timestamp}” created in E2E-Acc-...`.
- **Expected Result:** Project created successfully; dialog closes and confirmation notice appears.

### GH-PROJ-04: Directory List — Render newly created project in data table
- **Goal:** Verify project row attributes in projects table.
- **Preconditions:** Project `PROJ-{timestamp}` created.
- **Steps:**
  1. Locate row containing `PROJ-{timestamp}` in `table tbody tr`.
  2. Inspect project key link `/projects/{id}`.
  3. Inspect Account initials badge (`abbr.account-short`).
  4. Inspect Type badge (`span.project-type`) showing `Software`.
  5. Inspect Status badge showing `ACTIVE`.
- **Expected Result:** Table row displays all project metadata correctly.

### GH-PROJ-05: Stats Ribbon — Reflect incremented project metrics
- **Goal:** Verify directory header stats ribbon reflects current project counts.
- **Preconditions:** Projects directory loaded.
- **Steps:**
  1. Inspect `.projects-stats-ribbon .projects-count`.
  2. Inspect `.projects-stats-ribbon .projects-status-badge`.
- **Expected Result:** Stats ribbon shows active projects matching backend count.

### GH-PROJ-06: Filtering & Search — Locate project by unique key in search input
- **Goal:** Filter project directory by entering search text.
- **Preconditions:** Project `PROJ-{timestamp}` exists.
- **Steps:**
  1. Enter `PROJ-{timestamp}` into `input[name="projectSearch"]`.
  2. Verify table filters to 1 row.
  3. Verify filter summary: `Showing 1 of {N} projects`.
  4. Clear search input via `button[aria-label="Clear search text"]`.
- **Expected Result:** Table correctly isolates matching project and restores full list on clear.

### GH-PROJ-07: Hierarchy View Switcher — Toggle between List and Hierarchy views
- **Goal:** Validate view toggling between List and Hierarchy representations.
- **Preconditions:** On `https://localhost:5173/projects`.
- **Steps:**
  1. Click "Hierarchy" view toggle button (`button:has-text("Hierarchy")`).
  2. Verify button has `active` class and `aria-pressed="true"`.
  3. Verify hierarchy explanatory notice: `Projects are grouped beneath their accessible parents...`.
  4. Click "List" view toggle button (`button:has-text("List")`).
  5. Verify List mode is restored and active.
- **Expected Result:** Smooth toggle between List and Hierarchy views without UI regressions.

### GH-PROJ-08: Project Edit — Update description and project type via Edit modal
- **Goal:** Mutate project metadata and verify updates in table.
- **Preconditions:** Project `PROJ-{timestamp}` row located.
- **Steps:**
  1. In project row, click "Edit" button.
  2. Verify `ProjectManageDialog` opens with title `Edit PROJ-{timestamp}`.
  3. Update Description: `Updated E2E Description for Banking Governance`.
  4. Update Project Type: select `BANKING`.
  5. Click "Save project" button (`button:has-text("Save project")`).
  6. Await modal close and confirmation notice `Project updated.`.
  7. Verify Type badge in table now displays `Banking`.
- **Expected Result:** Project metadata updated and reflected immediately in UI.

### GH-PROJ-09: Project Delete — Delete unused project with confirmation
- **Goal:** Permanently remove the test project.
- **Preconditions:** Project `PROJ-{timestamp}` row located.
- **Steps:**
  1. In project row, click "Delete" button (`button.project-delete-action`).
  2. Verify `ProjectManageDialog` opens with title `Delete PROJ-{timestamp}`.
  3. Click confirm delete button (`button.danger:has-text("Delete project")`).
  4. Await modal close and notice `Project deleted.`.
- **Expected Result:** Project deleted successfully and confirmation notice rendered.

### GH-PROJ-10: Purge Verification — Ensure project no longer appears in directory
- **Goal:** Verify complete removal of deleted project from table and search.
- **Preconditions:** Project `PROJ-{timestamp}` deleted.
- **Steps:**
  1. Search for `PROJ-{timestamp}` in `input[name="projectSearch"]`.
  2. Verify table displays empty state: `No projects match your filters`.
  3. Verify project row is no longer attached to DOM.
- **Expected Result:** Zero references to deleted project remain in the directory.

## 4. Requirements Traceability Matrix (RTM)
| Scenario ID | Requirement Ref | Test File | Target URL | Method |
|---|---|---|---|---|
| GH-PROJ-01 | R-01, R-02 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-02 | R-02 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-03 | R-01, R-03 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-04 | R-04 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-05 | R-05 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-06 | R-06 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-07 | R-07 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-08 | R-08 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-09 | R-09 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
| GH-PROJ-10 | R-10 | tests/e2e/gridhub-projects-crud.spec.ts | https://localhost:5173/projects | Automated E2E |
