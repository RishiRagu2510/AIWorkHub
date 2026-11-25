---
plan_id: chronos-authenticated
title: Chronos Portal — Authenticated Timesheets, Work Logs & Period Reviews Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5175, file:frontend/chronos/src/routes.jsx, file:frontend/chronos/src/pages/Timesheets.jsx, file:frontend/chronos/src/pages/LogWorkPage.jsx, file:frontend/chronos/src/pages/TimesheetHistory.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 0977c97d1cd1ffef
---

# Chronos Portal — Authenticated Timesheets, Work Logs & Period Reviews Test Plan

## 1. Overview
- **Objective:** Verify authenticated timesheet and work tracking functionality on Chronos (`https://localhost:5175`) using the dedicated test account (`qa.test@griddynamics.com`), covering login session establishment, my timesheets metrics and daily breakdown, timesheet filter controls, layout switching (Table, Hierarchy, Calendar), work log creation page, timesheet history, and team timesheets overview.
- **In scope:**
  - Full email/password sign-in with dedicated test account (`qa.test@griddynamics.com`).
  - Landing on `/timesheets/my` and verifying user profile indicator ("QA Automated Tester").
  - Sidebar navigation links (`My timesheet`, `Log work`, `Timesheet history`, `Team timesheets`).
  - Timesheet period navigation (`Previous week`, `Current week`, `Next week`, and date range indicator).
  - Timesheet filter controls: Project combobox (8 options), Issue ID spinbutton, Billable combobox, Status combobox.
  - Timesheet summary metrics: Total hours, Billable hours, Non-billable hours, and 7-day daily breakdown cards (Mon–Sun).
  - Timesheet layout view switcher: Table view, Hierarchy view ("Project & task breakdown"), and Weekly calendar view.
  - Work log entry page (`/timesheets/log-work`): form elements (Issue selector, date picker, Hours input, description, billable toggle, and "Save work log" button).
  - Timesheet history (`/timesheets/history`): period metrics (Approved periods, Pending review, Draft/Rejected), period selector, and "Export Payroll CSV" button.
  - Team timesheets (`/timesheets/team`): team totals summary table with headers (User, Total hours, Billable hours, Non-billable hours).
- **Out of scope:**
  - Live third-party Jira Cloud external timesheet sync.
  - Destructive data wipes of historical timesheet periods.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded in `identity_tenant_db` with `ADMIN` role.
  - Services running: ApiGateway (`5204`), IdentityTenant (`5233`), Jira (`5432`/postgres `jira_db`), Chronos frontend (`5175`).
- **Preconditions / test data needs:**
  - `qa.test@griddynamics.com` credentials active.
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Sign in with valid credentials (`qa.test@griddynamics.com`) and redirect to `/timesheets/my` | Functional / Auth | Timesheets.jsx, Login.jsx | Session cookie established |
| R-02 | Display personalized user profile badge ("QA Automated Tester") and Chronos header branding | UI / Header | Header.jsx, UserIdentity.jsx | Profile avatar with initials "QA" |
| R-03 | Render primary sidebar timesheet navigation links | Navigation | Sidebar.jsx | My timesheet, Log work, Timesheet history, Team timesheets |
| R-04 | Support weekly period navigation with Previous, Current, Next week controls and date range label | Functional / Date | Timesheets.jsx | Updates displayed week range |
| R-05 | Render timesheet filter controls for Project, Issue ID, Billable, and Status | UI / Filter | Timesheets.jsx | Project combobox lists seeded projects |
| R-06 | Display timesheet summary metrics and Mon–Sun daily breakdown cards | UI / Metrics | Timesheets.jsx | Total, Billable, Non-billable hours |
| R-07 | Switch timesheet layout between Table view, Hierarchy view, and Weekly calendar view | UI / Layout | Timesheets.jsx | Toggles layout view components |
| R-08 | Navigate to `/timesheets/log-work` and display work log entry form elements | UI / Form | LogWorkPage.jsx, WorkLogForm.jsx | Inputs for Issue, date, hours, description |
| R-09 | Navigate to `/timesheets/history` and display submission history and Export Payroll CSV button | UI / History | TimesheetHistory.jsx | Metrics and CSV export action |
| R-10 | Navigate to `/timesheets/team` and display team totals aggregation table | UI / Team | Timesheets.jsx | Headers for User, Total, Billable, Non-billable |

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Should work log submission be persisted in this suite or read-only form validation? | Persisting logs mutates postgres `jira_db` | Form field presence, initial values, and client validation are in scope; mutations can be tested in a dedicated worklog lifecycle suite. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| - | None | - | - | - | New authenticated spec: `frontend/tests/e2e/chronos-auth.spec.ts`. |

**Impacted shared code:** None.  
**Summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning & Flows
| Flow | Starting Route | Action | Expected Outcome |
|---|---|---|---|
| Sign In | `/login` | Submit `qa.test@griddynamics.com` / `TestPassword123!` | Redirects to `/timesheets/my`, shows avatar "QA" |
| Header & Nav | `/timesheets/my` | Inspect header and sidebar | Header elements & 4 timesheet nav links visible |
| Week Controls | `/timesheets/my` | Click "Next week" then "Current week" | Date range shifts forward then returns to current week |
| Filter Controls | `/timesheets/my` | Inspect Project, Billable, Status comboboxes | All comboboxes present with default values |
| Metric Cards | `/timesheets/my` | Inspect Total, Billable, Non-billable cards | Metric values rendered along with Mon–Sun cards |
| Layout Switcher | `/timesheets/my` | Click "Hierarchy view" then "Weekly calendar view" | Heading changes to "Project & task breakdown", then calendar |
| Log Work Form | `/timesheets/log-work` | Inspect form inputs | Issue combobox, Hours textbox, Save work log button rendered |
| History View | `/timesheets/history` | Inspect history metrics and Export button | "Export Payroll CSV" button and 3 period summary cards rendered |
| Team View | `/timesheets/team` | Navigate to `/timesheets/team` | Team totals table rendered with User and hours columns |

## 5. Test Scenarios
```gherkin
@chronos @authenticated @smoke
Scenario: TC-001 Authenticated sign-in with test account
  Given the user is on the Chronos login page "https://localhost:5175/login"
  When the user submits credentials "qa.test@griddynamics.com" / "TestPassword123!"
  Then the browser should redirect to "/timesheets/my"
  And the user profile indicator "QA Automated Tester" should be visible

@chronos @authenticated
Scenario: TC-002 Chronos header branding and quick controls
  Given the user is authenticated on "/timesheets/my"
  Then the "Quick search" button should be visible
  And the "Switch to dark mode" button should be visible
  And the "Notifications" button should be visible
  And the user profile trigger should display "QA"

@chronos @authenticated
Scenario: TC-003 Timesheet sidebar navigation links
  Given the user is authenticated on "/timesheets/my"
  Then the sidebar should contain links to:
    | Link Name          | Target URL             |
    | My timesheet       | /timesheets/my         |
    | Log work           | /timesheets/log-work   |
    | Timesheet history  | /timesheets/history    |
    | Team timesheets    | /timesheets/team       |

@chronos @authenticated
Scenario: TC-004 Weekly period date navigation controls
  Given the user is authenticated on "/timesheets/my"
  When the user inspects the date navigation controls
  Then the "Previous week", "Current week", and "Next week" buttons should be enabled
  And the date range text matching /\d{4}-\d{2}-\d{2} to \d{4}-\d{2}-\d{2}/ should be visible
  When the user clicks "Next week"
  Then the date range should update
  When the user clicks "Current week"
  Then the date range should return to current week

@chronos @authenticated
Scenario: TC-005 Timesheet filter dropdown controls
  Given the user is authenticated on "/timesheets/my"
  Then the "Project" combobox should contain "All projects"
  And the "Issue ID" spinbutton should be visible
  And the "Billable" combobox should contain "All (Billable & Non-billable)"
  And the "Status" combobox should contain "All statuses"

@chronos @authenticated
Scenario: TC-006 Timesheet summary metric cards and daily breakdown
  Given the user is authenticated on "/timesheets/my"
  Then the "Total hours" metric card should be visible
  And the "Billable hours" metric card should be visible
  And the "Non-billable hours" metric card should be visible
  And the 7 daily breakdown cards for Mon through Sun should be visible

@chronos @authenticated
Scenario: TC-007 Switch layout views between Table, Hierarchy, and Calendar
  Given the user is authenticated on "/timesheets/my"
  When the user clicks "Hierarchy view"
  Then the heading "Project & task breakdown" should be visible
  When the user clicks "Weekly calendar view"
  Then the heading "Weekly calendar view" should be visible
  When the user clicks "Table view"
  Then the heading "Time by user & project" should be visible

@chronos @authenticated
Scenario: TC-008 Navigate to Log work page and verify form elements
  Given the user is authenticated on "/timesheets/my"
  When the user navigates to "/timesheets/log-work"
  Then the page heading should be "Log work"
  And the "Issue" combobox should be visible
  And the "Hours" textbox should be visible
  And the "Description" textbox should be visible
  And the "Billable" checkbox should be checked
  And the "Save work log" button should be visible

@chronos @authenticated
Scenario: TC-009 Navigate to Timesheet history and verify period records
  Given the user is authenticated on "/timesheets/my"
  When the user navigates to "/timesheets/history"
  Then the page heading should be "Timesheet history"
  And the "Approved periods" card should be visible
  And the "Pending review" card should be visible
  And the "Draft / Rejected" card should be visible
  And the "Export Payroll CSV" button should be visible

@chronos @authenticated
Scenario: TC-010 Navigate to Team timesheets and verify team totals table
  Given the user is authenticated on "/timesheets/my"
  When the user navigates to "/timesheets/team"
  Then the page heading should be "Team timesheets"
  And the heading "Team totals" should be visible
  And the team table should contain column headers:
    | User | Total hours | Billable hours | Non-billable hours |
```

## 6. Non-Functional & Cross-Cutting
- **Performance:** Navigation between tabs under 1.5s on local Vite dev server.
- **Accessibility:** All form elements have associated labels or accessible names; buttons have descriptive labels.
- **Rate Limit Safety:** Tests run in `serial` mode with single session per browser to prevent triggering ASP.NET Core IP rate-limiting.

## 7. Traceability Matrix (RTM)
| Requirement ID | Scenario | Priority |
|---|---|---|
| R-01 | TC-001 | P0 |
| R-02 | TC-002 | P0 |
| R-03 | TC-003 | P1 |
| R-04 | TC-004 | P1 |
| R-05 | TC-005 | P1 |
| R-06 | TC-006 | P1 |
| R-07 | TC-007 | P1 |
| R-08 | TC-008 | P0 |
| R-09 | TC-009 | P1 |
| R-10 | TC-010 | P1 |
