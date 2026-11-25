# Architecture
Test framework design, conventions, and the coverage map for this Playwright project.

## Layers and Folders
- Test Directory: `Frontend/tests/e2e`
- Configuration: `Frontend/playwright.config.ts`
- Monorepo Portals: Multiple frontend portals (`gridhub`, `hub`, `landing`, `persona`, `chronos`) under `Frontend/apps/`
- Backend Services: .NET solution (`AIWorkHub.sln`) with independently bounded services in `Services/`

## Fixtures and Page Objects
- Page Objects: None currently configured; specs interact directly with page locators.
- Fixtures: Standard `@playwright/test` imports (`test`, `expect`).

## Test Data and Auth
- Auth Strategy: Direct UI login verification in E2E specs.
- Test Data: In-line assertions and regex matches; no synthetic data fixtures configured yet.
- Environment Loading: Playwright `baseURL` set to `http://localhost:5173`; proxy configured in Vite to `http://localhost:5204`.

## Configuration and Environments
- Base URL: `http://localhost:5173`
- Projects: Chromium, Firefox, WebKit
- Parallelism: `fullyParallel: true`
- Retries: 2 on CI, 0 locally
- Workers: 1 on CI, default parallel locally
- WebServer: Runs `npm run dev:gridhub` on `http://localhost:5173` with 30s timeout and reuses existing server locally.

## CI
- CI Pipeline: `.github/workflows/ci.yml` builds and tests the backend, builds all frontend portals, and checks browser-test discovery.
- Reporters: `['html', { open: 'never' }]`, `['list']`
- Artifacts: Traces recorded on first retry, screenshots on failure.

## Conventions and Commands
Generated: 2026-11-17 | Source commit: uncommitted

### Project rules (highest priority)
- Standard Playwright assertions and role-based locators — Frontend/tests/e2e/auth.spec.ts

### Layout
- testDir: Frontend/tests/e2e | specs: *.spec.ts | pages: none | fixtures: @playwright/test
- data: inline | plans: Docs/testing/test-plans/ | aliases: none

### Conventions
- test import: @playwright/test · titles: describe('Feature', ...) / test('should ...', ...)
- tags: none · test-management ID: none
- locators: getByRole, locator, getByText · testIdAttribute: data-testid · navigation: page.goto('/login') relative to baseURL
- step style: inline assertions · max tests per file: none

### Commands
- run one file: npx playwright test Tests/e2e/auth.spec.ts · run by tag: npx playwright test --grep <tag>
- env selection: VITE_API_BASE_URL · auth: UI login
- timeouts: test 30000ms (default), expect 5000ms (default), action default · reporters local/CI: html, list
- lint / format / typecheck: npm run lint (oxlint) · npm run format (prettier)

### Tools available (last check)
- Playwright MCP: yes · playwright-cli: Version 1.65.1 · Jira: no · Confluence: no

### Page Object Models (POMs)
- Location: `Frontend/tests/pages/`
- Implemented models:
  - `LoginPage`: Form credentials, GIS SSO button, alert messages, and rate-limit resilient submission (`signInWithRetry`).
  - `HeaderComponent`: Portal branding verification, dark mode toggle, notifications bell, and user profile management (`openUserProfile`, `expectUserProfile`, `signOut`).
  - `CommandPaletteComponent`: Global search palette trigger, searchbox, keyboard shortcut actions, and ESC dismissal.
  - `PortalSwitcherComponent`: Cross-portal navigation popover trigger and multi-portal destination link verification.
  - `IssueEditorPage`: Form input controls, project listbox picker, priority dropdown, and draft recovery handling.
  - `IssueDetailsPage`: Issue title editing, status state machine transitions, metadata inspection, comment creation, and deletion modal.
  - `WorkLogPage`: Work log creation form, assigned issue select, duration hours input, description, and weekly target metrics cards.
  - `TimesheetsPage`: Timesheet table records, inline row editing, billable/draft badge assertions, layout switching, and deletion.
  - `AbsencePage`: Leave and WFH request creation form, custom select/calendar picker, quota warning alerts, balance cards, requests table, and cancellation actions.
  - `KnowledgePage`: Company knowledge document upload panel, custom dropzone with drag/file picker, document title and audience configuration, document inventory table, preview reader modal with raw text extractor, and inline deletion with two-step confirmation.
  - `ProjectsPage`: Projects directory table, stats ribbon metrics, keyword search input and filters, list vs. hierarchy layout toggles, ProjectCreateDialog modal with account combobox and project type selector, and ProjectManageDialog modal for editing and permanent deletion.
  - `PlanPageModel`: Subscription plan overview, hero capability metrics ratio, Portals and Tools catalog inspection with Included badges, inline Cancel Plan confirmation prompt, cancellation request submission, pending review pulse badge, and request withdrawal.
  - `AthennaChatPage`: Header greeting bar, agent status (Online & Ready), RBAC pill, telemetry metric cards (Daily Updated Issues and Daily Work Log), glowing orb ready stage, starter cards grid, model picker popover, prompt dock input and submit button, and multi-turn chat stream conversation bubbles with Athenna avatar.

### Deviations from pw-standards
- None. (Page Object Models are established under `Frontend/tests/pages`).

## Coverage Map
### Authentication & Landing
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/auth.spec.ts | TC-001 (should display login page and brand elements); TC-002 (should display password sign-in form with email and password inputs); TC-003 (should display Google SSO sign-in option with rendered GIS button); TC-004 (should support expanding trouble signing in help section); TC-005 (should display footer with portal name); TC-006 (should prevent submission and focus email field when submitting empty form); TC-007 (should display alert message when submitting invalid email or password); TC-008 (should reject invalid email syntax without '@' symbol); TC-009 (should redirect unauthenticated user from root URL to login page); TC-010 (should support collapsing help section when clicked again); TC-011 (should mask entered password characters); TC-012 (should disable form fieldset and show busy text during email sign-in submission); TC-013 (should display error notification if Google SSO configuration endpoint fails); TC-014 (should display error notice when landing with login error query parameter) | R-01, R-02, R-03, R-04, R-05, R-06, R-07, R-08, R-09, BR-02 | none | auth-sso |

### Landing Portal (LaunchPad & Email Verification)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/landing.spec.ts | TC-001 (LaunchPad branding & tagline); TC-002 (root redirect to /login); TC-003 (email/password form placeholders); TC-004 (GIS button rendering); TC-005 (trouble signing in expand); TC-006 (footer branding); TC-007 (missing token alert); TC-008 (password setup form with token); TC-009 (URL hash sanitation); TC-010 (password mismatch error); TC-011 (whitespace-only password error); TC-012 (return to sign in link) | R-01 to R-12 | none | landing-portal |

### Chronos Portal (Timesheets & Time Tracking)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/chronos.spec.ts | TC-001 (Chronos branding & copy); TC-002 (root redirect to /login); TC-003 (/timesheets/my redirect); TC-004 (/timesheets/log-work redirect); TC-005 (/timesheets/history redirect); TC-006 (/timesheets/team redirect); TC-007 (email/password form placeholders); TC-008 (GIS button rendering); TC-009 (trouble signing in expand); TC-010 (footer branding); TC-011 (invalid login alert); TC-012 (empty form submission prevention) | R-01 to R-12 | none | chronos-portal |

### Persona Portal (Employee Directory & Profile)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/persona.spec.ts | TC-001 (Persona branding & copy); TC-002 (root redirect to /login); TC-003 (/profile redirect); TC-004 (/employees redirect); TC-005 (/employees/leave redirect); TC-006 (/employees/org-chart redirect); TC-007 (email/password form placeholders); TC-008 (GIS button rendering); TC-009 (trouble signing in expand); TC-010 (footer branding); TC-011 (invalid login alert); TC-012 (empty form submission prevention) | R-01 to R-12 | none | persona-portal |

### Hub Portal (AIWorkHub Central Portal & Knowledge)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/hub.spec.ts | TC-001 (AIWorkHub branding & copy); TC-002 (root redirect to /login); TC-003 (/home redirect); TC-004 (/knowledge redirect); TC-005 (email/password form placeholders); TC-006 (GIS button rendering); TC-007 (trouble signing in expand); TC-008 (footer branding); TC-009 (invalid login alert); TC-010 (empty form submission prevention); TC-011 (invalid email syntax rejection); TC-012 (password masking) | R-01 to R-12 | none | hub-portal |

### GridHub Workspace (Authenticated Dashboard, Projects & Issues)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/gridhub-auth.spec.ts | TC-001 (sign in with dedicated test user and redirect to dashboard); TC-002 (dashboard metrics and summary cards); TC-003 (primary sidebar navigation links); TC-004 (recent projects portfolio in sidebar); TC-005 (projects directory data table); TC-006 (filter projects searchbox); TC-007 (issues queue in list view); TC-008 (switch to Kanban board with columns); TC-009 (issue detail pane inspection); TC-010 (sign out and redirect to login) | R-01 to R-10 | LoginPage, HeaderComponent | gridhub-authenticated |

### Chronos Portal (Authenticated Timesheets, Work Logs & Period Reviews)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/chronos-auth.spec.ts | TC-001 (sign in with dedicated test user and redirect to /timesheets/my); TC-002 (header branding, quick search, dark mode toggle, and notification bell); TC-003 (timesheet navigation links in sidebar); TC-004 (navigate weekly periods using date controls); TC-005 (timesheet filter dropdown controls); TC-006 (summary metric cards and daily breakdown); TC-007 (switch layout between Table, Hierarchy, and Calendar views); TC-008 (Log work page and verify form elements); TC-009 (Timesheet history and display period records); TC-010 (Team timesheets and display team totals table) | R-01 to R-10 | LoginPage, HeaderComponent | chronos-authenticated |

### Persona Portal (Authenticated Employee Directory, Profiles, Org Chart & Leave)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/persona-auth.spec.ts | TC-001 (sign in with dedicated test user and establish session); TC-002 (header branding, quick search, dark mode toggle, and notification bell); TC-003 (employee navigation links in sidebar); TC-004 (employee directory employee count and data table); TC-005 (filter employee directory by keyword in searchbox); TC-006 (employee directory filter dropdowns); TC-007 (individual employee profile); TC-008 (organization chart and hierarchy tree); TC-009 (Leave & WFH dashboard and balance cards); TC-010 (organization setup catalog metrics and tabs) | R-01 to R-10 | LoginPage, HeaderComponent | persona-authenticated |

### Hub Portal (Authenticated Central Copilot, Knowledge Base & Cross-Portal Launchpad)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/hub-auth.spec.ts | TC-001 (sign in with dedicated test user and establish session); TC-002 (header branding, navigation links, and user profile avatar); TC-003 (Athenna copilot welcome banner and RBAC status); TC-004 (workspace summary overview cards in sidebar); TC-005 (quick action prompt cards for cross-portal queries); TC-006 (Athenna copilot prompt input box with model selector); TC-007 (open and dismiss Global Quick Search dialog with ESC key); TC-008 (open cross-portal launchpad popover and verify links); TC-009 (Company Knowledge Base indexed documents table); TC-010 (document text viewer modal, inspect contents, and dismiss reader) | R-01 to R-10 | LoginPage, HeaderComponent, CommandPaletteComponent, PortalSwitcherComponent | hub-authenticated |

### LaunchPad Portal (Authenticated Workspace Launcher & Organization Plan)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/launchpad-auth.spec.ts | TC-001 (sign in with dedicated test user and establish session); TC-002 (header branding, controls, and user profile avatar); TC-003 (authenticated welcome heading and subtitle description); TC-004 (display all 4 primary workspace portal launch tiles); TC-005 (verify external target URLs for all 4 portal launch tiles); TC-006 (plan overview section on home page); TC-007 (plan management page and plan metrics); TC-008 (active portals list on plan management page); TC-009 (active tools list and access requests status); TC-010 (open user profile menu, inspect user details, and verify sign out button) | R-01 to R-10 | LoginPage, HeaderComponent | launchpad-authenticated |

### GridHub Issues (Full Lifecycle CRUD Mutations & State Machine)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/gridhub-issues-crud.spec.ts | GH-CRUD-01 (validate form on empty submission); GH-CRUD-02 (create new task issue with unique summary); GH-CRUD-03 (verify issue details, metadata, and description); GH-CRUD-04 (transition issue status and verify notification); GH-CRUD-05 (add new comment and verify timeline); GH-CRUD-06 (edit issue summary and verify update); GH-CRUD-07 (locate updated issue in table list view); GH-CRUD-08 (render issue card in Kanban board view); GH-CRUD-09 (delete issue and redirect back to issues list); GH-CRUD-10 (verify deleted issue purged from search results) | R-01 to R-10 | LoginPage, HeaderComponent, IssueEditorPage, IssueDetailsPage | gridhub-issues-crud |

### Chronos Portal (Work Logs & Timesheet Mutation Lifecycle)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/chronos-worklog-crud.spec.ts | CW-CRUD-01 (validate form on empty submission); CW-CRUD-02 (log new work entry with valid hours, issue, and description); CW-CRUD-03 (update weekly total logged hours and completion metric card); CW-CRUD-04 (navigate to my timesheet and verify entry in data table); CW-CRUD-05 (display billable and period draft status badges); CW-CRUD-06 (edit work log entry and update duration and description); CW-CRUD-07 (verify updated duration and description in table); CW-CRUD-08 (switch to Hierarchy view and verify entry grouping); CW-CRUD-09 (delete work log entry using row action and inline confirmation); CW-CRUD-10 (verify deleted entry is purged from table) | R-01 to R-10 | LoginPage, WorkLogPage, TimesheetsPage | chronos-worklog-crud |

### Persona Portal (Leave & WFH Mutation Lifecycle)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/persona-absence-crud.spec.ts | PR-CRUD-01 (prevent submission when required fields missing); PR-CRUD-02 (enforce leave quota and disable submit on exceeded balance); PR-CRUD-03 (submit work from home request with unique reason); PR-CRUD-04 (display success banner and reset form upon submission); PR-CRUD-05 (render new request in table with PENDING badge and WFH kind chip); PR-CRUD-06 (submit 1-day floating holiday within available allowance); PR-CRUD-07 (reflect pending reservation on floating holiday balance card); PR-CRUD-08 (trigger cancellation on pending floating holiday request); PR-CRUD-09 (verify cancellation notice and updated CANCELLED status badge); PR-CRUD-10 (verify restored available balance on floating holiday card) | R-01 to R-10 | LoginPage, AbsencePage | persona-absence-crud |

### Hub Portal (Knowledge Base Documents Mutation Lifecycle)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/hub-knowledge-crud.spec.ts | HK-CRUD-01 (disable upload button when no file selected); HK-CRUD-02 (require at least one role when Specific Roles is selected); HK-CRUD-03 (enable upload button when role checkbox is checked); HK-CRUD-04 (upload and ingest new document with Everyone audience); HK-CRUD-05 (display success notification and reset form upon ingestion); HK-CRUD-06 (render newly ingested document in table with badges); HK-CRUD-07 (reflect incremented document count in header metric); HK-CRUD-08 (open preview modal on row click and render extracted text); HK-CRUD-09 (dismiss document preview modal via Done Reading button); HK-CRUD-10 (delete document via inline confirmation and purge from table) | R-01 to R-10 | LoginPage, KnowledgePage | hub-knowledge-crud |

### GridHub Projects (Full Lifecycle CRUD Mutations, Hierarchy & Governance)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/gridhub-projects-crud.spec.ts | GH-PROJ-01 (prevent submission when required fields empty); GH-PROJ-02 (reject invalid project key syntax starting with digits); GH-PROJ-03 (create new standalone project with valid metadata); GH-PROJ-04 (render newly created project in data table with badges); GH-PROJ-05 (reflect active project metrics in header stats ribbon); GH-PROJ-06 (filter directory table by unique project key in search input); GH-PROJ-07 (toggle between List and Hierarchy view modes); GH-PROJ-08 (edit project metadata and update type to Banking); GH-PROJ-09 (delete project via confirmation modal); GH-PROJ-10 (verify deleted project purged from table and search) | R-01 to R-10 | LoginPage, ProjectsPage | gridhub-projects-crud |

### LaunchPad Plan (Subscription Lifecycle, Cancellation Mutation & Withdrawal)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/launchpad-plan-crud.spec.ts | LP-PLAN-01 (render title, description, and Enterprise Plan hero banner); LP-PLAN-02 (display Enabled Capabilities ratio in hero banner); LP-PLAN-03 (list all 4 primary portals with Included status badges); LP-PLAN-04 (list Athenna AI chat with Included status badge); LP-PLAN-05 (open and safely dismiss cancellation prompt); LP-PLAN-06 (submit cancellation request); LP-PLAN-07 (display Cancellation Under Review status on item row); LP-PLAN-08 (display submitted request in Requests section and hero pill); LP-PLAN-09 (withdraw the cancellation request); LP-PLAN-10 (verify restoration of Cancel Plan button on item) | R-01 to R-10 | LoginPage, PlanPageModel | launchpad-plan-crud |

### Hub Portal (Athenna AI Copilot Workspace Chat)
| Spec file | Tests (titles or IDs) | Requirement refs | POMs | Plan |
|---|---|---|---|---|
| Frontend/tests/e2e/hub-athenna-chat.spec.ts | HK-CHAT-01 (render personalized welcome, Online & Ready status, and RBAC pill); HK-CHAT-02 (render Daily Updated Issues and Daily Work Log metric cards); HK-CHAT-03 (display glowing AI orb, AI is ready heading, and multi-portal subtitle); HK-CHAT-04 (render cross-portal starter prompt cards grid); HK-CHAT-05 (open model popover and display available intelligence engines); HK-CHAT-06 (switch model selection and update selector button badge); HK-CHAT-07 (validate prompt input and toggle send button active state); HK-CHAT-08 (trigger prompt via starter card and transition stage to chat stream); HK-CHAT-09 (render assistant bubble with Athenna avatar and generated response); HK-CHAT-10 (submit follow-up query and append new turn to active stream) | R-01 to R-10 | LoginPage, AthennaChatPage | hub-athenna-chat |





