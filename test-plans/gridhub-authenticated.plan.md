---
plan_id: gridhub-authenticated
title: GridHub Workspace — Authenticated Dashboard, Projects & Issues Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5173, file:frontend/gridhub/src/routes.jsx, file:frontend/gridhub/src/pages/Dashboard.jsx, file:frontend/gridhub/src/pages/Projects.jsx, file:frontend/gridhub/src/pages/Issues.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 4ae6020e8b1c3d33
---

# GridHub Workspace — Authenticated Dashboard, Projects & Issues Test Plan

## 1. Overview
- **Objective:** Verify authenticated workspace functionality on GridHub (`https://localhost:5173`) using the dedicated test account (`qa.test@griddynamics.com`), covering login session establishment, dashboard metrics, projects portfolio, and issues Kanban board views.
- **In scope:**
  - Full email/password sign-in with dedicated test account (`qa.test@griddynamics.com`).
  - Landing on `/dashboard` and rendering user welcome heading ("Welcome back, QA!").
  - Sidebar navigation links (Dashboard, Accounts & hierarchy, Projects, My work, Issues, Audit logs).
  - Dashboard workspace metric summary cards (Projects count, Tracked issues count, Resolved count).
  - Recent projects sidebar listings (Bench, Forever Living Products, Google, Green Dot, Visa).
  - Projects directory (`/projects`): table columns, project rows, search filter, and Account portfolio filter.
  - Issues tracking (`/issues`): list view, issue queue items, status filtering chips ("All states", "To Do", "Done").
  - Issues Kanban board view (`/issues?View=board`): status columns (TO DO, IN PROGRESS, DONE) and card rendering.
  - Selected issue detail preview panel.
  - Authenticated session sign-out workflow.
- **Out of scope:**
  - Live third-party Jira Cloud webhooks or GitHub commit syncing.
  - Modifying underlying client production database records.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded in `identity_tenant_db` with `ADMIN` role.
  - Services running: ApiGateway (`5204`), IdentityTenant (`5233`), Jira (`5432`/postgres `jira_db`), GridHub frontend (`5173`).
- **Preconditions / test data needs:**
  - `qa.test@griddynamics.com` credentials active.
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Sign in with valid credentials (`qa.test@griddynamics.com`) and redirect to `/dashboard` | Functional / Auth | Login.jsx, AuthProvider.jsx | Session cookie established |
| R-02 | Display personalized greeting "Welcome back, QA!" and workspace summary metrics on dashboard | UI / Dashboard | Dashboard.jsx | Metric cards: Projects, Total issues, Resolved |
| R-03 | Render primary workspace navigation links in sidebar | Navigation | Sidebar.jsx | Dashboard, Accounts, Projects, Issues, Audit |
| R-04 | Render Recent projects list in sidebar with project keys and links | UI / Navigation | Sidebar.jsx | 7 seeded projects |
| R-05 | Navigate to `/projects` and display all projects table with Account, Project, Status, and Type | UI / Table | Projects.jsx | Table with 7 projects |
| R-06 | Filter project directory using search box | Functional / Filter | Projects.jsx | Filters table by keyword |
| R-07 | Navigate to `/issues` and display issue list with keys (`FLP360-1`, `RAPID-3`, `FLP360-2`) | UI / List | Issues.jsx | Issue queue with 3 seeded issues |
| R-08 | Switch issues view to Kanban "Board view" showing workflow columns | UI / Kanban | Issues.jsx | TO DO, IN PROGRESS, CODE REVIEW, DONE |
| R-09 | Select an issue from list and display detail inspection panel | UI / Detail | Issues.jsx | Detail pane for FLP360-1 |
| R-10 | Sign out via user menu and redirect to `/login` | Functional / Auth | UserMenu.jsx | Clears session cookie |

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Should issue creation be tested in this suite or a dedicated mutation suite? | Creating new issues writes to `jira_db` | Viewing, filtering, board transitions, and inspecting issues are in scope; creating issues can be added in a separate lifecycle suite. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| - | None | - | - | - | New authenticated spec: `frontend/tests/e2e/gridhub-auth.spec.ts`. |

**Impacted shared code:** None.  
**Summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning & Flows
| Flow | Starting Route | Action | Expected Outcome |
|---|---|---|---|
| Login Flow | `/login` | Submit `qa.test@griddynamics.com` / `TestPassword123!` | Redirects to `/dashboard`, shows welcome heading |
| Dashboard Overview | `/dashboard` | Verify metrics & sidebar | Metric numbers `7` projects, `3` issues visible |
| Projects Directory | `/projects` | View table & filter | 7 project rows rendered, filter works |
| Issues Board | `/issues` | Toggle Board view | Columns `TO DO`, `DONE` rendered with cards |
| Detail Inspection | `/issues` | Click `FLP360-1` card | Opens detail pane with summary and assignee |

## 4a. UI Verification
| TC | Screen / Action | Checked Elements | Observed | Result |
|---|---|---|---|---|
| TC-001 | `/login` → submit | Heading "Welcome back, QA!", URL `/dashboard` | Verified live | ✔ |
| TC-002 | `/dashboard` | Heading 1 "Welcome back, QA!", metric strong "7", strong "3" | Verified live | ✔ |
| TC-003 | Sidebar | Links "Dashboard", "Projects", "Issues", "My work" | Verified live | ✔ |
| TC-004 | Sidebar recent projects | Links for Bench, Google, Visa, Green Dot | Verified live | ✔ |
| TC-005 | `/projects` | Table columns "Account", "Project", "Type", "Status" | Verified live | ✔ |
| TC-006 | `/projects` search | Searchbox "Search projects or accounts" | Verified live | ✔ |
| TC-007 | `/issues` | List view with FLP360-1, FLP360-2, RAPID-3 | Verified live | ✔ |
| TC-008 | `/issues` board view | Button "Board view", columns TO DO, DONE | Verified live | ✔ |
| TC-009 | `/issues` detail pane | Heading/button with FLP360-1, assignee "Vetri Selvam Pari" | Verified live | ✔ |
| TC-010 | User profile → Sign out | Profile button "User profile: QA Automated Tester", Logout | Verified live | ✔ |

## 5. Scenarios

### TC-001 [NEW] should sign in successfully with dedicated test user and redirect to dashboard
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-01 · **Priority:** P1 · **Tags:** @smoke @auth @gridhub
- **Preconditions:** `qa.test@griddynamics.com` seeded and active
- **Steps:**
  1. Navigate to https://localhost:5173/login
  2. Fill email `qa.test@griddynamics.com` and password `TestPassword123!`
  3. Click "Sign in with email"
  4. Wait for URL to reach `/dashboard`
- **Expected:**
  - Browser URL is `/dashboard`.
  - Greeting "Welcome back, QA!" is visible.

### TC-002 [NEW] should display workspace metrics and summary cards on dashboard
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-02 · **Priority:** P1 · **Tags:** @regression @gridhub
- **Steps:**
  1. Navigate to `/dashboard`
  2. Verify metric cards for Projects, Total tracked issues, and Resolved
  3. Verify action buttons "Create issue" and "✦ Daily Standup Digest" are visible
- **Expected:**
  - Metric summary cards display accurate seeded project and issue counts.

### TC-003 [NEW] should render primary navigation links in sidebar
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-03 · **Priority:** P1 · **Tags:** @regression @gridhub
- **Steps:**
  1. Inspect sidebar navigation
  2. Verify links: "Dashboard" (`/dashboard`), "Projects" (`/projects`), "Issues" (`/issues`), "My work" (`/my-work`)
- **Expected:**
  - All navigation links are visible and point to valid routes.

### TC-004 [NEW] should display recent projects portfolio in sidebar
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-04 · **Priority:** P2 · **Tags:** @regression @gridhub
- **Steps:**
  1. Locate "Recent projects" region in sidebar
  2. Verify project links for Google, Visa, Bench, and Green Dot
- **Expected:**
  - Seeded portfolio projects are accessible from sidebar.

### TC-005 [NEW] should navigate to projects directory and display data table
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-05 · **Priority:** P1 · **Tags:** @regression @gridhub
- **Steps:**
  1. Navigate to `/projects`
  2. Verify heading "All projects"
  3. Verify table header contains Account, Project, Type, Status, Lead
  4. Verify table rows contain active projects (e.g. "Bench", "Google", "Visa")
- **Expected:**
  - Projects data table renders all active projects.

### TC-006 [NEW] should filter projects by keyword in searchbox
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-06 · **Priority:** P2 · **Tags:** @regression @gridhub
- **Steps:**
  1. Navigate to `/projects`
  2. Type "Visa" into "Search projects or accounts" searchbox
  3. Verify table displays only Visa project row
  4. Clear searchbox and verify full project count restores
- **Expected:**
  - Filter dynamically narrows down table rows.

### TC-007 [NEW] should display issues queue in list view with seeded tasks
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-07 · **Priority:** P1 · **Tags:** @regression @gridhub
- **Steps:**
  1. Navigate to `/issues`
  2. Verify heading "Issues"
  3. Verify issue items `FLP360-1`, `FLP360-2`, and `RAPID-3` are visible
- **Expected:**
  - Issue queue renders all active seeded issues.

### TC-008 [NEW] should switch issues view to Kanban board with status columns
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-08 · **Priority:** P1 · **Tags:** @regression @gridhub
- **Steps:**
  1. Navigate to `/issues`
  2. Click button "Board view"
  3. Verify URL contains `View=board`
  4. Verify columns "TO DO", "IN PROGRESS", "DONE" are visible
  5. Verify cards exist under "TO DO" (e.g. `FLP360-1`) and "DONE" (e.g. `FLP360-2`)
- **Expected:**
  - Kanban board layout displays with status columns and cards.

### TC-009 [NEW] should display issue detail pane when selecting an issue
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-09 · **Priority:** P1 · **Tags:** @regression @gridhub
- **Steps:**
  1. Navigate to `/issues`
  2. Click issue `FLP360-1` in queue
  3. Verify detail pane displays title "Login Page Throws 500 Error on Password Reset Request"
  4. Verify assignee "Vetri Selvam Pari" is displayed
- **Expected:**
  - Issue detail pane displays complete metadata.

### TC-010 [NEW] should sign out user and redirect to login page
- **Decision:** NEW → frontend/tests/e2e/gridhub-auth.spec.ts
- **Requirements:** R-10 · **Priority:** P1 · **Tags:** @smoke @auth @gridhub
- **Steps:**
  1. Click user profile trigger ("User profile: QA Automated Tester")
  2. Click "Sign out" or "Logout"
  3. Verify navigation redirects to `/login`
  4. Verify unauthenticated access to `/dashboard` redirects back to `/login`
- **Expected:**
  - User session is terminated and protected routes are locked.

## 6. Coverage and Traceability
| Requirement ID | Scenario | Priority | Technique | Decision |
|---|---|---|---|---|
| R-01 | TC-001 | P1 | End-to-end login | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-02 | TC-002 | P1 | UI verification | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-03 | TC-003 | P1 | Navigation check | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-04 | TC-004 | P2 | UI verification | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-05 | TC-005 | P1 | Table inspection | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-06 | TC-006 | P2 | Functional filter | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-07 | TC-007 | P1 | Queue inspection | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-08 | TC-008 | P1 | View toggle | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-09 | TC-009 | P1 | Detail inspection | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |
| R-10 | TC-010 | P1 | Session teardown | NEW → frontend/tests/e2e/gridhub-auth.spec.ts |

## 7. Gaps and Risks
- **Top Risks:**
  - Session cookies shared across test workers if run concurrently.
  - *Mitigation:* Each test runs in its own isolated browser context with dedicated login step or shared storage state.
