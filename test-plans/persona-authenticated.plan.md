---
plan_id: persona-authenticated
title: Persona Portal — Authenticated Employee Directory, Profiles, Org Chart & Leave Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5174, file:frontend/persona/src/routes.jsx, file:frontend/persona/src/pages/EmployeeDirectory.jsx, file:frontend/persona/src/pages/EmployeeProfile.jsx, file:frontend/persona/src/pages/EmployeeOrgChart.jsx, file:frontend/persona/src/pages/EmployeeAbsence.jsx, file:frontend/persona/src/pages/EmployeeSetup.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 574604634d58bdfe
---

# Persona Portal — Authenticated Employee Directory, Profiles, Org Chart & Leave Test Plan

## 1. Overview
- **Objective:** Verify authenticated employee directory, profile inspection, organization hierarchy, leave balances, and setup catalog features on Persona (`https://localhost:5174`) using the dedicated test account (`qa.test@griddynamics.com`).
- **In scope:**
  - Full email/password sign-in with dedicated test account (`qa.test@griddynamics.com`).
  - Header branding, quick search, dark mode toggle, notifications bell, and user avatar initials ("QA").
  - Sidebar employee navigation links (`My profile`, `Leave & WFH`, `Organization chart`, `Employee management`, `Organization setup`, `Employee reports`).
  - Employee directory (`/employees`): employee count banner ("Showing 21 of 21 employees"), action buttons (Download CSV, Spreadsheet View), and data table.
  - Directory search filtering: filter table by employee name (e.g., "Akash") and clear filter.
  - Directory dropdown filters: department, grade, and status comboboxes.
  - Individual employee profile (`/employees/26`): name heading ("Akash Baskar"), employee ID ("# EMP-022"), Grade & Job title ("T1 · Junior Software Engineer"), Department ("Developer"), and company timeline.
  - Interactive organization hierarchy (`/employees/org-chart`): tree heading, employee & manager stats ("21 employees · 6 reporting managers"), zoom controls, and manager card nodes.
  - Leave & WFH dashboard (`/employees/leave`): annual policy card, balance metric cards (Floating holidays, Sick leave, Earned leave), and request form inputs.
  - Organization setup catalog (`/employees/setup`): catalog metric summary buttons (Departments, Roles, Permissions, Skills, Grades, Job Titles) and category tab switching.
- **Out of scope:**
  - Direct database deletions of permanent tenant records.
  - Live third-party HRIS/payroll synchronization.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded in `identity_tenant_db` with `ADMIN` role.
  - Services running: ApiGateway (`5204`), IdentityTenant (`5233`), Persona (`5234`), Persona frontend (`5174`).
- **Preconditions / test data needs:**
  - `qa.test@griddynamics.com` credentials active.
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Sign in with valid credentials (`qa.test@griddynamics.com`) and establish authenticated session | Functional / Auth | Login.jsx, AuthProvider.jsx | Session cookie established |
| R-02 | Display header branding, quick search, dark mode toggle, notification bell, and user profile badge | UI / Header | Header.jsx, UserIdentity.jsx | Profile avatar with initials "QA" |
| R-03 | Render primary sidebar employee navigation links | Navigation | Sidebar.jsx | Profile, Leave, Org chart, Admin, Setup, Reports |
| R-04 | Navigate to `/employees` and display directory header with count and table columns | UI / Directory | EmployeeDirectory.jsx | 21 employees in seeded database |
| R-05 | Filter employee directory by keyword in searchbox | Functional / Search | EmployeeDirectory.jsx | Filters table rows dynamically |
| R-06 | Render directory filter comboboxes for department, grade, and status | UI / Filter | EmployeeDirectory.jsx | Options populated from catalog |
| R-07 | Navigate to employee profile `/employees/26` and render full employee details | UI / Profile | EmployeeProfile.jsx | Shows name, badge, title, department |
| R-08 | Navigate to `/employees/org-chart` and display organization reporting tree and stats | UI / OrgChart | EmployeeOrgChart.jsx | Shows 21 employees, 6 managers, zoom buttons |
| R-09 | Navigate to `/employees/leave` and display leave balance cards and request form | UI / Leave | EmployeeAbsence.jsx | Floating holidays, Sick leave, Earned leave |
| R-10 | Navigate to `/employees/setup` and display organization setup metrics and catalog tabs | UI / Setup | EmployeeSetup.jsx | Metrics for departments, roles, grades |

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Should employee creation or deletion be tested in this suite or a dedicated mutation suite? | Creating new employees adds records to `identity_tenant_db` | Directory search, filtering, profile views, org chart hierarchy, and leave balances are in scope; destructive record deletion belongs in a dedicated lifecycle suite. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| - | None | - | - | - | New authenticated spec: `frontend/tests/e2e/persona-auth.spec.ts`. |

**Impacted shared code:** None.  
**Summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning & Flows
| Flow | Starting Route | Action | Expected Outcome |
|---|---|---|---|
| Sign In | `/login` | Submit `qa.test@griddynamics.com` / `TestPassword123!` | Authenticates and establishes session |
| Header & Nav | `/profile` | Inspect header and sidebar | Header elements & 6 employee navigation links visible |
| Directory Overview | `/employees` | Inspect table & count banner | Table visible with "Showing 21 of 21 employees" |
| Directory Search | `/employees` | Type "Akash" in searchbox | Table filters to matching employee row |
| Directory Dropdowns | `/employees` | Inspect department, grade, status dropdowns | Comboboxes rendered with options |
| Profile Inspection | `/employees/26` | View profile page | Displays "Akash Baskar", "# EMP-022", "Developer" |
| Org Chart Tree | `/employees/org-chart` | View hierarchy tree | Displays tree nodes and manager stats |
| Leave Balances | `/employees/leave` | View balance cards | Shows 12d available for Floating, Sick, Earned leave |
| Setup Catalog | `/employees/setup` | Inspect setup metrics & tabs | Metric buttons for Departments, Roles, Grades visible |

## 5. Test Scenarios
```gherkin
@persona @authenticated @smoke
Scenario: TC-001 Authenticated sign-in with test account
  Given the user is on the Persona login page "https://localhost:5174/login"
  When the user submits credentials "qa.test@griddynamics.com" / "TestPassword123!"
  Then the session cookie should be established
  And the user profile trigger should display "QA"

@persona @authenticated
Scenario: TC-002 Persona header branding and quick controls
  Given the user is authenticated on Persona
  When the user inspects the header
  Then the "Search people, directory, employees" button should be visible
  And the "Switch to dark mode" button should be visible
  And the "Notifications" button should be visible
  And the user profile trigger should display "QA"

@persona @authenticated
Scenario: TC-003 Employee navigation links in sidebar
  Given the user is authenticated on Persona
  Then the sidebar should contain navigation links to:
    | Link Name             | Target URL             |
    | My profile            | /profile               |
    | Leave & WFH           | /employees/leave       |
    | Organization chart    | /employees/org-chart   |
    | Employee management   | /employees/admin       |
    | Organization setup    | /employees/setup       |
    | Employee reports      | /employees/reports     |

@persona @authenticated
Scenario: TC-004 Employee directory table and employee count
  Given the user is authenticated on Persona
  When the user navigates to "/employees"
  Then the page heading should be "Employee directory"
  And the text matching /Showing 21 of 21 employees/ should be visible
  And the "Download CSV" button should be visible
  And the "Spreadsheet View" button should be visible
  And the employee directory table should be visible with columns:
    | Employee | Department & Role | Grade | Status | Actions |

@persona @authenticated
Scenario: TC-005 Search employee directory by keyword
  Given the user is on "/employees"
  When the user enters "Akash" into the directory searchbox
  Then the employee directory table should display "Akash Baskar"
  When the user clears the searchbox
  Then the full list of 21 employees should be displayed

@persona @authenticated
Scenario: TC-006 Employee directory filter dropdowns
  Given the user is on "/employees"
  Then the "department" combobox should contain "All departments"
  And the "grade" combobox should contain "All grades"
  And the "Status" combobox should contain "All Statuses"

@persona @authenticated
Scenario: TC-007 Navigate to individual employee profile
  Given the user is authenticated on Persona
  When the user navigates to "/employees/26"
  Then the employee heading should be "Akash Baskar"
  And the employee badge should display "ACTIVE"
  And the employee ID "# EMP-022" should be visible
  And the job title "Junior Software Engineer" should be visible
  And the department "Developer" should be visible
  And the "Company timeline" region should be visible

@persona @authenticated
Scenario: TC-008 Interactive organization hierarchy tree
  Given the user is authenticated on Persona
  When the user navigates to "/employees/org-chart"
  Then the page heading should be "Organization hierarchy"
  And the summary stats should display "21" employees and "6" reporting managers
  And the zoom controls for "Zoom Canvas" should be visible
  And the root reporting manager node "Sivachandran Kannan" should be visible

@persona @authenticated
Scenario: TC-009 Leave & WFH dashboard and balances
  Given the user is authenticated on Persona
  When the user navigates to "/employees/leave"
  Then the page heading should be "Leave & WFH"
  And the "Floating holidays" balance card should display "12" days available
  And the "Sick leave" balance card should display "12" days available
  And the "Earned leave" balance card should display "12" days available
  And the "Request type" combobox should be visible in the new request panel

@persona @authenticated
Scenario: TC-010 Organization setup catalog metrics and tabs
  Given the user is authenticated on Persona
  When the user navigates to "/employees/setup"
  Then the page heading should be "Organization setup"
  And the metric button for "Departments" should display "4"
  And the metric button for "Roles" should display "6"
  And the metric button for "Permissions" should display "74"
  And the metric button for "Grades" should display "9"
  And the metric button for "Job Titles" should display "9"
  And the navigation for "Setup categories" should be visible
```

## 6. Non-Functional & Cross-Cutting
- **Performance:** Directory table load time under 1.5s on local Vite dev server.
- **Accessibility:** All form elements have associated labels or accessible names; buttons have descriptive labels.
- **Rate Limit Safety:** Tests run in `serial` mode with single session per browser to prevent triggering ASP.NET Core IP rate-limiting.

## 7. Traceability Matrix (RTM)
| Requirement ID | Scenario | Priority |
|---|---|---|
| R-01 | TC-001 | P0 |
| R-02 | TC-002 | P0 |
| R-03 | TC-003 | P1 |
| R-04 | TC-004 | P0 |
| R-05 | TC-005 | P1 |
| R-06 | TC-006 | P1 |
| R-07 | TC-007 | P0 |
| R-08 | TC-008 | P1 |
| R-09 | TC-009 | P1 |
| R-10 | TC-010 | P1 |
