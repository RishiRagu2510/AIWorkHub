---
plan_id: hub-authenticated
title: Hub Portal — Authenticated Central Copilot, Knowledge Base & Cross-Portal Launchpad Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5172, file:frontend/hub/src/routes.jsx, file:frontend/hub/src/pages/Home.jsx, file:frontend/hub/src/pages/KnowledgeBase.jsx, file:frontend/hub/src/components/Header.jsx, file:frontend/hub/src/components/CommandPalette.jsx, file:frontend/hub/src/components/PortalSwitcher.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 5611e1dc527e3edd
---

# Hub Portal — Authenticated Central Copilot, Knowledge Base & Cross-Portal Launchpad Test Plan

## 1. Overview
- **Objective:** Verify authenticated workspace copilot, Athenna prompt workflows, company knowledge base inspection, cross-portal launchpad navigation, and global search on AIWorkHub Central Portal (`https://localhost:5172`) using the dedicated test account (`qa.test@griddynamics.com`).
- **In scope:**
  - Full email/password sign-in with dedicated test account (`qa.test@griddynamics.com`).
  - Header branding (`AIWorkHub`), navigation links (`AI Chat`, `Knowledge Base`), quick search, dark mode toggle, notifications bell, and user avatar initials ("QA").
  - Athenna workspace copilot greeting ("Welcome back, QA Automated Tester", "Online & Ready", "RBAC: ADMIN").
  - Workspace sidebar cards (`Daily Updated Issues`, `Chat History`, `Issue Completion`, `Daily Work Log` with target 8.0h/day).
  - Quick action prompt cards for cross-portal assistant queries (`GridHub Sprint Health`, `Persona All Employees in Excel`, `GridHub Issues Report in Excel`, `Persona Leave Requests to Decide`).
  - Athenna prompt input area (textbox `Ask Athenna across GridHub, Persona, and Chronos...`, model selector `Gemini Flash Fast & Capable`, send prompt control).
  - Global quick search command palette dialog with quick actions (`Create new issue`, `Browse all issues`, `Browse projects`, `My timesheet`) and ESC keyboard dismissal.
  - Cross-portal launchpad popover navigation links to GridHub (`5173`), Persona (`5174`), and Chronos (`5175`).
  - Company Knowledge Base (`/knowledge`): indexed docs badge ("2 indexed docs"), document upload zone, and indexed documents data table.
  - Document text reader modal: opening extracted text view for "company policy handbook", inspecting metadata (`v1 • 6,570 characters • Audience: Everyone`), and dismissing via "Done Reading".
- **Out of scope:**
  - Live AI model inference streaming (tested via mock/UI interaction without burning external LLM tokens).
  - Permanent document upload mutation or backend vector database deletion.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded in `identity_tenant_db` with `ADMIN` role.
  - Services running: ApiGateway (`5204`), IdentityTenant (`5233`), Hub frontend (`5172`).
- **Preconditions / test data needs:**
  - `qa.test@griddynamics.com` credentials active.
  - Initial knowledge base seeded with policy handbook and project guide documents.
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Sign in with valid credentials (`qa.test@griddynamics.com`) and redirect to `/home` | Functional / Auth | Login.jsx, AuthProvider.jsx | Session cookie established |
| R-02 | Display header branding, navigation links (`AI Chat`, `Knowledge Base`), search trigger, dark mode toggle, and user profile avatar | UI / Header | Header.jsx, UserIdentity.jsx | Profile avatar with initials "QA" |
| R-03 | Render Athenna copilot welcome banner, RBAC admin status, and AI readiness summary on `/home` | UI / Copilot | Home.jsx | "Welcome back, QA Automated Tester" |
| R-04 | Display workspace summary overview cards in sidebar (`Daily Updated Issues`, `Chat History`, `Issue Completion`, `Daily Work Log`) | UI / Sidebar | Home.jsx | Live synced work logs and tasks |
| R-05 | Display quick action prompt cards for cross-portal queries | UI / Prompts | Home.jsx | GridHub, Persona, Chronos query cards |
| R-06 | Render Athenna copilot prompt input box with model selector and file attachment buttons | UI / Input | Home.jsx | Ask Athenna prompt textbox |
| R-07 | Open and dismiss Global Quick Search dialog with ESC key and verify quick action options | Functional / Search | CommandPalette.jsx | Palette with issue/project shortcuts |
| R-08 | Open cross-portal launchpad popover and verify links to GridHub, Persona, and Chronos | Navigation / Launchpad | PortalSwitcher.jsx | Links to 5173, 5174, 5175 |
| R-09 | Navigate to Company Knowledge Base (`/knowledge`) and display indexed documents table with metadata | UI / Knowledge | KnowledgeBase.jsx | Policy handbook & project guide |
| R-10 | Open document text viewer modal for indexed document, inspect contents, and dismiss reader | UI / Modal | KnowledgeBase.jsx | Full policy document reading modal |

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Should live file upload to Knowledge Base be asserted in this suite? | Uploading files permanently affects tenant document storage | Upload zone UI elements and document reader inspection are asserted without destructive uploads. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| - | None | - | - | - | New authenticated spec: `frontend/tests/e2e/hub-auth.spec.ts`. |

**Impacted shared code:** None.  
**Summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning & Flows
| Flow | Starting Route | Action | Expected Outcome |
|---|---|---|---|
| Sign In | `/login` | Submit `qa.test@griddynamics.com` / `TestPassword123!` | Redirects to `/home` with valid session |
| Header Nav | `/home` | Inspect navigation banner | Brand, AI Chat, Knowledge Base, dark toggle, "QA" avatar visible |
| Copilot Banner | `/home` | Inspect main copilot header | "Welcome back, QA Automated Tester", "RBAC: ADMIN", "Online & Ready" |
| Sidebar Cards | `/home` | Inspect sidebar cards | Daily Updated Issues, Chat History, Issue Completion, Daily Work Log visible |
| Quick Action Cards | `/home` | Inspect prompt cards | Sprint Health, All Employees, Issues Report, Leave Requests cards visible |
| Prompt Input | `/home` | Inspect copilot prompt bar | Textbox, Gemini Flash model selector, Attach files button visible |
| Quick Search | `/home` | Click Quick search button then click ESC | Dialog opens with shortcuts, dismisses on ESC |
| Portal Switcher | `/home` | Click Open portals button | Popover opens with GridHub, Persona, and Chronos links |
| Knowledge Base | `/knowledge` | Navigate to `/knowledge` | Heading "Company Knowledge Base", 2 indexed docs badge, table rows visible |
| Document Reader | `/knowledge` | Click "company policy handbook" then "Done Reading" | Reader modal displays extracted policy text, closes cleanly |

---

## 5. Test Scenarios

### TC-001: Sign in with dedicated test user and establish session
- **Priority:** Critical
- **Requirement:** R-01
- **Tags:** `@auth`, `@critical`
- **Preconditions:** Unauthenticated browser session.
- **Steps:**
  1. Navigate to `https://localhost:5172/login`.
  2. Fill email input with `qa.test@griddynamics.com`.
  3. Fill password input with `TestPassword123!`.
  4. Click `Sign in with email` button.
- **Expected Results:**
  - User is redirected to `https://localhost:5172/home`.

### TC-002: Display header branding, navigation links, and user profile avatar
- **Priority:** High
- **Requirement:** R-02
- **Tags:** `@ui`, `@header`
- **Preconditions:** Authenticated session on `https://localhost:5172/home`.
- **Steps:**
  1. Inspect header banner.
  2. Verify brand link `AIWorkHub` points to `/home`.
  3. Verify navigation links `AI Chat` (`/home`) and `Knowledge Base` (`/knowledge`).
  4. Verify quick search button, dark mode toggle button, notifications button, and user profile avatar initials "QA".
- **Expected Results:**
  - All header controls and profile badge are visible.

### TC-003: Render Athenna copilot welcome banner and RBAC status
- **Priority:** High
- **Requirement:** R-03
- **Tags:** `@ui`, `@copilot`
- **Preconditions:** Authenticated session on `https://localhost:5172/home`.
- **Steps:**
  1. Locate main greeting heading.
  2. Verify heading text contains "Welcome back, QA Automated Tester".
  3. Verify agent status "Athenna Autonomous Workspace Agent" and "Online & Ready".
  4. Verify RBAC badge text "RBAC: ADMIN".
- **Expected Results:**
  - Copilot greeting, agent status, and admin RBAC badge are rendered.

### TC-004: Display workspace summary overview cards in sidebar
- **Priority:** High
- **Requirement:** R-04
- **Tags:** `@ui`, `@sidebar`
- **Preconditions:** Authenticated session on `https://localhost:5172/home`.
- **Steps:**
  1. Verify heading "Daily Updated Issues" is visible.
  2. Verify heading "Chat History" is visible.
  3. Verify heading "Issue Completion" is visible.
  4. Verify heading "Daily Work Log" is visible with target hours.
- **Expected Results:**
  - All four sidebar cards are rendered with summary metrics.

### TC-005: Display quick action prompt cards for cross-portal queries
- **Priority:** High
- **Requirement:** R-05
- **Tags:** `@ui`, `@prompts`
- **Preconditions:** Authenticated session on `https://localhost:5172/home`.
- **Steps:**
  1. Locate quick action buttons in main copilot view.
  2. Verify button with text matching /GridHub Sprint Health/i is visible.
  3. Verify button with text matching /Persona All Employees in Excel/i is visible.
  4. Verify button with text matching /GridHub Issues Report in Excel/i is visible.
  5. Verify button with text matching /Persona Leave Requests to Decide/i is visible.
- **Expected Results:**
  - All four quick action query buttons are visible and clickable.

### TC-006: Render Athenna copilot prompt input box with model selector
- **Priority:** High
- **Requirement:** R-06
- **Tags:** `@ui`, `@input`
- **Preconditions:** Authenticated session on `https://localhost:5172/home`.
- **Steps:**
  1. Locate prompt textbox with placeholder /Ask Athenna across GridHub, Persona, and Chronos/i.
  2. Verify model selector button is visible showing "Gemini Flash".
  3. Verify "Attach files" button and "Send prompt" button are visible.
- **Expected Results:**
  - Prompt bar elements are rendered correctly.

### TC-007: Open and dismiss Global Quick Search dialog with ESC key
- **Priority:** High
- **Requirement:** R-07
- **Tags:** `@functional`, `@search`
- **Preconditions:** Authenticated session on `https://localhost:5172/home`.
- **Steps:**
  1. Click `Quick search` button in header.
  2. Verify dialog `Global search` opens with searchbox.
  3. Verify quick action shortcuts (`Create new issue`, `Browse all issues`, `Browse projects`, `My timesheet`) are visible.
  4. Click ESC badge or press Escape key.
- **Expected Results:**
  - Global search dialog opens and dismisses cleanly.

### TC-008: Open cross-portal launchpad popover and verify links
- **Priority:** High
- **Requirement:** R-08
- **Tags:** `@navigation`, `@launchpad`
- **Preconditions:** Authenticated session on `https://localhost:5172/home`.
- **Steps:**
  1. Click `Open portals` button in header.
  2. Verify navigation menu `Portals` opens.
  3. Verify links to GridHub (`https://localhost:5173/dashboard`), Persona (`https://localhost:5174/profile`), and Chronos (`https://localhost:5175/timesheets/my`) are present.
- **Expected Results:**
  - Portals popover displays links to all three workspace portals.

### TC-009: Navigate to Company Knowledge Base and display indexed documents table
- **Priority:** Critical
- **Requirement:** R-09
- **Tags:** `@ui`, `@knowledge`
- **Preconditions:** Authenticated session.
- **Steps:**
  1. Navigate to `https://localhost:5172/knowledge`.
  2. Verify heading `Company Knowledge Base` is visible.
  3. Verify indexed documents badge indicates indexed documents (e.g., "2" indexed docs).
  4. Verify document upload area is visible.
  5. Verify table with columnheaders `Document`, `Audience`, `Version`, `Last Updated`, and `Action` is rendered.
- **Expected Results:**
  - Knowledge base catalog and data table are displayed.

### TC-010: Open document text viewer modal, inspect contents, and dismiss reader
- **Priority:** High
- **Requirement:** R-010
- **Tags:** `@ui`, `@modal`
- **Preconditions:** Authenticated session on `https://localhost:5172/knowledge`.
- **Steps:**
  1. Click row or cell containing "company policy handbook".
  2. Verify document preview modal opens with heading `company policy handbook`.
  3. Verify subtitle details contain metadata (version, character count, audience).
  4. Verify policy text contains "AIWORKHUB ENTERPRISE COMPANY POLICY & EMPLOYEE HANDBOOK".
  5. Click button `Done Reading`.
- **Expected Results:**
  - Policy document modal renders extracted text and closes cleanly upon clicking `Done Reading`.

---

## 6. Coverage Checklist & Gap Report
- [x] Functional coverage: Authentication, copilot greeting, knowledge base catalog, document reader modal, quick search, portal switcher.
- [x] UI coverage: Header branding, user initials avatar, sidebar summary cards, prompt input controls, table layout, modal viewer.
- [x] Error handling & resilience: Unauthenticated redirection tested in existing `hub.spec.ts`.
- [x] Cross-portal navigation: Launchpad popover links to 5173, 5174, and 5175.
- [x] Security: Dedicated synthetic test credentials (`qa.test@griddynamics.com`), no plain text passwords in git.

## 7. Requirements Traceability Matrix (RTM)
| Requirement ID | Test Scenario | Priority | Status |
|---|---|---|---|
| R-01 | TC-001 | Critical | Designed |
| R-02 | TC-002 | High | Designed |
| R-03 | TC-003 | High | Designed |
| R-04 | TC-004 | High | Designed |
| R-05 | TC-005 | High | Designed |
| R-06 | TC-006 | High | Designed |
| R-07 | TC-007 | High | Designed |
| R-08 | TC-008 | High | Designed |
| R-09 | TC-009 | Critical | Designed |
| R-10 | TC-010 | High | Designed |
