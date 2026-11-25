---
plan_id: launchpad-authenticated
title: LaunchPad Portal — Authenticated Workspace Launcher & Organization Plan Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5171, file:frontend/landing/src/routes.jsx, file:frontend/landing/src/pages/Home.jsx, file:frontend/landing/src/pages/Plan.jsx, file:frontend/landing/src/components/Header.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 6470819969573091
---

# LaunchPad Portal — Authenticated Workspace Launcher & Organization Plan Test Plan

## 1. Overview
- **Objective:** Verify authenticated workspace portal launcher, quick launch action tiles, organization subscription plan overview, and profile inspection on LaunchPad (`https://localhost:5171`) using the dedicated test account (`qa.test@griddynamics.com`).
- **In scope:**
  - Full email/password sign-in with dedicated test account (`qa.test@griddynamics.com`).
  - Header branding (`LaunchPad home`), quick search, dark mode toggle, notifications bell, and user avatar initials ("QA").
  - Authenticated greeting heading ("Welcome, QA Automated Tester") and subtitle description on `/`.
  - Primary workspace launch tiles: GridHub (`Projects & Sprints`), Persona (`HR & Directory`), Chronos (`Time & Attendance`), and AIWorkHub (`AI Assistant & Central Hub`).
  - Validation of external target navigation URLs: GridHub (`https://localhost:5173/dashboard`), Persona (`https://localhost:5174/profile`), Chronos (`https://localhost:5175/timesheets/my`), and AIWorkHub (`https://localhost:5172/home`).
  - Subscription plan overview section on `/`: heading `Your plan`, portal inclusion count (`4 of 4 portals · 1 of 1 tool included`), and link `Manage plan`.
  - Detailed plan management page (`/plan`): `Enterprise Workspace Plan` badge, `5 / 5 Enabled Capabilities` metric, portals list, tools list (`Athenna AI chat`), and back link (`← All portals`).
  - User profile menu: name (`QA Automated Tester`), email (`qa.test@griddynamics.com`), role badge (`ADMIN`), organization (`Grid Dynamics`), and `Sign out` action.
- **Out of scope:**
  - Plan cancellation or subscription billing mutations.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded in `identity_tenant_db` with `ADMIN` role.
  - Services running: ApiGateway (`5204`), IdentityTenant (`5233`), LaunchPad frontend (`5171`).
- **Preconditions / test data needs:**
  - `qa.test@griddynamics.com` credentials active.
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Sign in with valid credentials (`qa.test@griddynamics.com`) and redirect to `/` | Functional / Auth | Login.jsx, AuthProvider.jsx | Session cookie established |
| R-02 | Display header branding, search trigger, dark mode toggle, and user profile avatar | UI / Header | Header.jsx, UserIdentity.jsx | Profile avatar with initials "QA" |
| R-03 | Render authenticated welcome heading (`Welcome, QA Automated Tester`) and subtitle description | UI / Home | Home.jsx | Main greeting on `/` |
| R-04 | Display all 4 primary workspace portal launch tiles | UI / Portals | Home.jsx | GridHub, Persona, Chronos, AIWorkHub |
| R-05 | Verify external target URLs for all 4 portal launch tiles | Navigation / Links | Home.jsx | Targets ports 5173, 5174, 5175, 5172 |
| R-06 | Render plan overview section on `/` with summary counts and manage plan link | UI / Plan | Home.jsx | 4 of 4 portals · 1 of 1 tool included |
| R-07 | Navigate to `/plan` and display plan heading, enterprise badge, and capabilities metric | UI / PlanPage | Plan.jsx | 5 / 5 Enabled Capabilities |
| R-08 | Render active portals list on `/plan` with included badges | UI / PlanPortals | Plan.jsx | Standalone workspace modules |
| R-09 | Render active tools list (`Athenna AI chat`) and requests status on `/plan` | UI / PlanTools | Plan.jsx | AI assistants list |
| R-10 | Open user profile menu, inspect user details, and verify sign out button | UI / Profile | Header.jsx | Full name, email, ADMIN badge, Sign out |

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Should clicking "Cancel plan" on `/plan` be tested in this suite? | Modifying active plan subscriptions affects workspace configuration | Plan items, badges, and capabilities metrics are asserted in view-only mode. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| - | None | - | - | - | New authenticated spec: `frontend/tests/e2e/launchpad-auth.spec.ts`. |

**Impacted shared code:** None.  
**Summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning & Flows
| Flow | Starting Route | Action | Expected Outcome |
|---|---|---|---|
| Sign In | `/login` | Submit `qa.test@griddynamics.com` / `TestPassword123!` | Redirects to `https://localhost:5171/` |
| Header Nav | `/` | Inspect navigation banner | Brand, dark mode toggle, notifications, "QA" avatar visible |
| Greeting | `/` | Inspect welcome banner | "Welcome, QA Automated Tester" and subtitle visible |
| Launch Tiles | `/` | Inspect navigation list items | GridHub, Persona, Chronos, AIWorkHub tiles visible |
| Target URLs | `/` | Inspect tile href attributes | Validated URLs to 5173, 5174, 5175, 5172 |
| Plan Summary | `/` | Inspect plan region on `/` | Heading "Your plan", 4 of 4 portals, "Manage plan" link |
| Plan Page | `/plan` | Navigate to `/plan` | "Enterprise Workspace Plan", "5 / 5 Enabled Capabilities" |
| Portals List | `/plan` | Inspect Portals section on `/plan` | 4 portals with "Included" badges visible |
| Tools List | `/plan` | Inspect Tools section on `/plan` | Athenna AI chat with "Included" badge visible |
| User Profile | `/` | Click User profile button | Popover shows name, email, ADMIN badge, and Sign out button |

---

## 5. Test Scenarios

### TC-001: Sign in with dedicated test user and establish session
- **Priority:** Critical
- **Requirement:** R-01
- **Tags:** `@auth`, `@critical`
- **Preconditions:** Unauthenticated browser session.
- **Steps:**
  1. Navigate to `https://localhost:5171/login`.
  2. Fill email input with `qa.test@griddynamics.com`.
  3. Fill password input with `TestPassword123!`.
  4. Click `Sign in with email` button.
- **Expected Results:**
  - User is redirected to `https://localhost:5171/`.

### TC-002: Display header branding, controls, and user profile avatar
- **Priority:** High
- **Requirement:** R-02
- **Tags:** `@ui`, `@header`
- **Preconditions:** Authenticated session on `https://localhost:5171/`.
- **Steps:**
  1. Inspect header banner.
  2. Verify brand link `LaunchPad home` is visible.
  3. Verify dark mode toggle button, notifications button, and user profile avatar button with initials "QA".
- **Expected Results:**
  - All header controls and profile badge are visible.

### TC-003: Render authenticated welcome heading and subtitle description
- **Priority:** High
- **Requirement:** R-03
- **Tags:** `@ui`, `@greeting`
- **Preconditions:** Authenticated session on `https://localhost:5171/`.
- **Steps:**
  1. Locate main greeting heading.
  2. Verify heading text contains "Welcome, QA Automated Tester".
  3. Verify subtitle description describes workspace portal selection.
- **Expected Results:**
  - Greeting heading and description are rendered.

### TC-004: Display all 4 primary workspace portal launch tiles
- **Priority:** Critical
- **Requirement:** R-04
- **Tags:** `@ui`, `@portals`
- **Preconditions:** Authenticated session on `https://localhost:5171/`.
- **Steps:**
  1. Locate portals navigation list.
  2. Verify link with text matching /GridHub/i is visible.
  3. Verify link with text matching /Persona/i is visible.
  4. Verify link with text matching /Chronos/i is visible.
  5. Verify link with text matching /AIWorkHub/i is visible.
- **Expected Results:**
  - All four launch tiles are visible in navigation list.

### TC-005: Verify external target URLs for all 4 portal launch tiles
- **Priority:** High
- **Requirement:** R-05
- **Tags:** `@navigation`, `@links`
- **Preconditions:** Authenticated session on `https://localhost:5171/`.
- **Steps:**
  1. Inspect href attribute of GridHub link (target `https://localhost:5173/dashboard`).
  2. Inspect href attribute of Persona link (target `https://localhost:5174/profile`).
  3. Inspect href attribute of Chronos link (target `https://localhost:5175/timesheets/my`).
  4. Inspect href attribute of AIWorkHub link (target `https://localhost:5172/home`).
- **Expected Results:**
  - All four tile links point to their respective portal URLs.

### TC-006: Render plan overview section on home page
- **Priority:** High
- **Requirement:** R-06
- **Tags:** `@ui`, `@plan`
- **Preconditions:** Authenticated session on `https://localhost:5171/`.
- **Steps:**
  1. Locate section with heading `Your plan`.
  2. Verify plan tag `AIWorkHub Plan` is visible.
  3. Verify summary text `4 of 4 portals · 1 of 1 tool included` is visible.
  4. Verify link `Manage plan` points to `/plan`.
- **Expected Results:**
  - Plan overview region and link are rendered.

### TC-007: Navigate to plan management page and display plan metrics
- **Priority:** High
- **Requirement:** R-07
- **Tags:** `@ui`, `@planPage`
- **Preconditions:** Authenticated session.
- **Steps:**
  1. Navigate to `https://localhost:5171/plan`.
  2. Verify heading `Your plan` is visible.
  3. Verify plan badge `Enterprise Workspace Plan` is visible.
  4. Verify metric text `5 / 5 Enabled Capabilities` is visible.
  5. Verify back link `← All portals` points to `/`.
- **Expected Results:**
  - Plan management header, badge, and capability metrics are rendered.

### TC-008: Render active portals list on plan management page
- **Priority:** High
- **Requirement:** R-08
- **Tags:** `@ui`, `@planPortals`
- **Preconditions:** Authenticated session on `https://localhost:5171/plan`.
- **Steps:**
  1. Locate region `Portals`.
  2. Verify count text `4 of 4 included` is visible.
  3. Verify list items for GridHub, Persona, Chronos, and AIWorkHub each display `Included` status.
- **Expected Results:**
  - All four portal subscriptions are marked as Included.

### TC-009: Render active tools list and access requests status
- **Priority:** High
- **Requirement:** R-09
- **Tags:** `@ui`, `@planTools`
- **Preconditions:** Authenticated session on `https://localhost:5171/plan`.
- **Steps:**
  1. Locate region `Tools`.
  2. Verify tool item `Athenna AI chat` displays `Included` status.
  3. Locate region `Requests from your company`.
  4. Verify text indicates `No open requests.`.
- **Expected Results:**
  - Tools list and requests status are displayed.

### TC-010: Open user profile menu, inspect user details, and verify sign out button
- **Priority:** High
- **Requirement:** R-10
- **Tags:** `@ui`, `@profile`
- **Preconditions:** Authenticated session.
- **Steps:**
  1. Click `User profile` button in header.
  2. Verify profile menu displays user name `QA Automated Tester`.
  3. Verify email `qa.test@griddynamics.com`.
  4. Verify role badge `ADMIN` and company `Grid Dynamics`.
  5. Verify button `Sign out` is visible.
- **Expected Results:**
  - User profile details and Sign out action are displayed.

---

## 6. Coverage Checklist & Gap Report
- [x] Functional coverage: Authentication, welcome greeting, launch tile routing, plan inspection, user profile menu.
- [x] UI coverage: Header branding, avatar initials, portal tiles, plan summary cards, capabilities metrics.
- [x] Error handling & resilience: Unauthenticated redirection tested in existing `landing.spec.ts`.
- [x] Cross-portal navigation: External launch URLs to 5173, 5174, 5175, and 5172 verified.
- [x] Security: Dedicated synthetic test credentials (`qa.test@griddynamics.com`), no plain text passwords in git.

## 7. Requirements Traceability Matrix (RTM)
| Requirement ID | Test Scenario | Priority | Status |
|---|---|---|---|
| R-01 | TC-001 | Critical | Designed |
| R-02 | TC-002 | High | Designed |
| R-03 | TC-003 | High | Designed |
| R-04 | TC-004 | Critical | Designed |
| R-05 | TC-005 | High | Designed |
| R-06 | TC-006 | High | Designed |
| R-07 | TC-007 | High | Designed |
| R-08 | TC-008 | High | Designed |
| R-09 | TC-009 | High | Designed |
| R-10 | TC-010 | High | Designed |
