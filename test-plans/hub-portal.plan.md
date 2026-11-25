---
plan_id: hub-portal
title: Hub Portal (AIWorkHub Central Portal & Knowledge) — Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5172, file:frontend/hub/src/routes.jsx, file:frontend/hub/src/main.jsx, file:frontend/src/App.jsx, file:frontend/src/pages/Login.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 9dd38e9d1fe5d465
---

# Hub Portal (AIWorkHub Central Portal & Knowledge) — Test Plan

## 1. Overview
- **Objective:** Verify public authentication entry, portal branding, copy, and unauthenticated route protections for the central AIWorkHub organization portal (`@gridhub/hub` on `https://localhost:5172`).
- **In scope:**
  - AIWorkHub portal login page: banner "AIWorkHub home", heading "Sign in to your organization", portal badges ("AIWorkHub", "AIWorkHub Central Portal"), and intro copy ("Your unified launchpad for all workspaces.Connected in one place.").
  - Company email/password form inputs, submission validation, and invalid credential alert handling.
  - Google Single Sign-On (GIS) button rendering and error banner absence.
  - Collapsible "Trouble signing in?" disclosure.
  - Hub footer branding ("AIWorkHub", "AIWorkHub Central Portal").
  - Protected route guards: unauthenticated access to `/`, `/home`, and `/knowledge` redirects to `/login`.
- **Out of scope:**
  - Live third-party Google account authentication and MFA challenges.
  - Live cross-portal workspace launches or knowledge article authoring (requires authenticated session with active tenant memberships).
- **Assumptions:**
  - Hub dev server runs over local HTTPS on `https://localhost:5172` with dev SSL certificates.
  - Playwright browser context runs with `ignoreHTTPSErrors: true`.
  - Backend API Gateway (`http://localhost:5204`) and IdentityTenant (`http://localhost:5233`) remain accessible for `/api` requests.
- **Preconditions / test data needs:**
  - Hub dev server listening on `https://localhost:5172`.
- **Change summary:** NEW 12 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Display AIWorkHub portal branding, header "AIWorkHub home", heading "Sign in to your organization", badges ("AIWorkHub", "AIWorkHub Central Portal"), and copy "Your unified launchpad for all workspaces.Connected in one place." | UI / Branding | Login.jsx, routes.jsx | Verified live via Playwright MCP |
| R-02 | Redirect unauthenticated root access (`/`) to `/login` | Navigation / Guard | App.jsx | ProtectedRoute |
| R-03 | Redirect unauthenticated access to `/home` to `/login` | Navigation / Guard | routes.jsx, App.jsx | Access guard |
| R-04 | Redirect unauthenticated access to `/knowledge` to `/login` | Navigation / Guard | routes.jsx, App.jsx | Access guard |
| R-05 | Provide company email textbox (with placeholder `you@company.com`) and password textbox with accessible labels | Functional / UI | PasswordSignIn.jsx | Form fields |
| R-06 | Render Google Identity Services (GIS) button ("Continue with Google. Opens in new tab") without error alert | Integration / SSO | GoogleSignIn.jsx | Verified live via Playwright MCP |
| R-07 | Provide expandable "Trouble signing in?" disclosure showing organization administrator guidance | UI / Usability | Login.jsx | `<details>` / `<summary>` |
| R-08 | Display footer containing "AIWorkHub" and "AIWorkHub Central Portal" brand labels | UI / Branding | Login.jsx | `<footer className="sso-footer">` |
| R-09 | Display accessible alert message when submitting incorrect email or password | Error handling | PasswordSignIn.jsx | Alert on invalid credentials |
| R-10 | Prevent form submission and focus email field when submitting empty form | Validation | PasswordSignIn.jsx | HTML5 required validation |
| R-11 | Reject invalid email syntax without '@' symbol | Validation | PasswordSignIn.jsx | HTML5 email validation |
| R-12 | Mask entered password characters | Security / UI | PasswordSignIn.jsx | `type="password"` |

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Will authenticated hub home dashboard and knowledge base management be planned in a separate plan? | Requires user session with tenant memberships | Yes, this plan covers central Hub portal entry and route access guards; authenticated cross-portal launchpad will be planned separately. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| - | None | - | - | - | No existing tests for Hub portal (`@gridhub/hub`). |

**Impacted shared code:** None. New spec will be created in `frontend/tests/e2e/hub.spec.ts`.  
**Summary:** NEW 12 · UPDATE 0 · REUSE 0 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning (EP) & Boundary Value Analysis (BVA)
| Route / State | Target Route | Expected Redirect | Auth State |
|---|---|---|---|
| Protected root | `/` | `/login` | Unauthenticated |
| Protected home | `/home` | `/login` | Unauthenticated |
| Protected knowledge | `/knowledge` | `/login` | Unauthenticated |

## 4a. UI Verification
| TC | Step / screen | Checked (role + name, label, or text) | Observed | Result |
|---|---|---|---|---|
| TC-001 | Login page | link "AIWorkHub home", heading "Sign in to your organization", text "Your unified launchpad for all workspaces.Connected in one place." | Visible, exact | ✔ |
| TC-002 | `/` | Navigation to `/` | Redirects to `/login` | ✔ |
| TC-003 | `/home` | Navigation to `/home` | Redirects to `/login` | ✔ |
| TC-004 | `/knowledge` | Navigation to `/knowledge` | Redirects to `/login` | ✔ |
| TC-005 | Form inputs | textbox "Company email", textbox "Password", button "Sign in with email" | Visible, enabled | ✔ |
| TC-006 | Google SSO | button "Continue with Google. Opens in new tab" | Rendered inside GIS iframe | ✔ |
| TC-007 | Disclosure | group "Trouble signing in?" | Expands on click | ✔ |
| TC-008 | Footer | generic "AIWorkHub", generic "AIWorkHub Central Portal" | Visible in contentinfo | ✔ |
| TC-009 | Invalid submit | text "Email or password is incorrect. After repeated failed attempts, wait 15 minutes before trying again." | Error alert displayed | ✔ |
| TC-010 | Empty submit | HTML5 required validation on email input | Blocks submission, focuses email | ✔ |
| TC-011 | Invalid email format | Input without `@` symbol | Blocked by HTML5 validation | ✔ |
| TC-012 | Password masking | Password input | Has `type="password"` attribute | ✔ |

## 5. Scenarios

### TC-001 [NEW] should display AIWorkHub login page branding and copy
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-01 · **Technique:** UI verification
- **Priority:** P1 · **Tags:** @smoke @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** Hub dev server running on https://localhost:5172
- **Steps:**
  1. Navigate to https://localhost:5172/login
  2. Verify link "AIWorkHub home" is visible with `href="/login"`
  3. Verify heading "Sign in to your organization" is visible
  4. Verify intro text contains "Your unified launchpad for all workspaces.Connected in one place."
- **Expected:**
  - AIWorkHub branding, badges, and copy are visible.

### TC-002 [NEW] should redirect unauthenticated user from root URL to login page
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-02 · **Technique:** route guard
- **Priority:** P1 · **Tags:** @smoke @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** no active session
- **Steps:**
  1. Navigate to https://localhost:5172/
  2. Wait for navigation to complete
- **Expected:**
  - URL ends with `/login`.
  - Login form is visible.

### TC-003 [NEW] should redirect unauthenticated user from /home to login page
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-03 · **Technique:** route guard
- **Priority:** P1 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** no active session
- **Steps:**
  1. Navigate to https://localhost:5172/home
- **Expected:**
  - URL ends with `/login`.

### TC-004 [NEW] should redirect unauthenticated user from /knowledge to login page
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-04 · **Technique:** route guard
- **Priority:** P1 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** no active session
- **Steps:**
  1. Navigate to https://localhost:5172/knowledge
- **Expected:**
  - URL ends with `/login`.

### TC-005 [NEW] should display email and password inputs with correct placeholders
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-05 · **Technique:** equivalence partitioning
- **Priority:** P1 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5172/login
- **Steps:**
  1. Verify textbox "Company email" has placeholder "you@company.com" and attribute `type="email"`
  2. Verify password textbox has attribute `type="password"`
  3. Verify button "Sign in with email" is visible and enabled
- **Expected:**
  - Form controls conform to specification.

### TC-006 [NEW] should render Google SSO button without error notice
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-06 · **Technique:** integration check
- **Priority:** P2 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5172/login
- **Steps:**
  1. Wait for Google Identity Services initialization
  2. Verify button "Continue with Google. Opens in new tab" is visible
  3. Verify no alert banner is rendered in `.google-identity-signin`
- **Expected:**
  - Google button rendered successfully in active iframe.

### TC-007 [NEW] should expand help section when clicking Trouble signing in
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-07 · **Technique:** UI disclosure
- **Priority:** P2 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5172/login
- **Steps:**
  1. Click on "Trouble signing in?" summary element
  2. Verify expanded guidance text is visible
- **Expected:**
  - Details disclosure opens and displays guidance text.

### TC-008 [NEW] should display footer branding with AIWorkHub and AIWorkHub Central Portal
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-08 · **Technique:** UI verification
- **Priority:** P3 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5172/login
- **Steps:**
  1. Locate footer with class `.sso-footer`
  2. Verify "AIWorkHub" and "AIWorkHub Central Portal" are visible inside footer
- **Expected:**
  - Footer branding displays properly.

### TC-009 [NEW] should display alert message when submitting invalid email or password
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-09 · **Technique:** error handling
- **Priority:** P1 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5172/login
- **Steps:**
  1. Enter invalid email "unregistered.user@example.com"
  2. Enter invalid password "WrongPassword123!"
  3. Click "Sign in with email"
- **Expected:**
  - Alert displays text "Email or password is incorrect. After repeated failed attempts, wait 15 minutes before trying again."

### TC-010 [NEW] should prevent submission and focus email field when submitting empty form
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-10 · **Technique:** boundary value analysis
- **Priority:** P2 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5172/login
- **Steps:**
  1. Leave email and password blank
  2. Click "Sign in with email"
- **Expected:**
  - Form is not submitted.
  - Email field is focused.

### TC-011 [NEW] should reject invalid email syntax without @ symbol
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-11 · **Technique:** validation
- **Priority:** P2 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5172/login
- **Steps:**
  1. Enter "invalid-email-no-at" in email field
  2. Enter valid password
  3. Click "Sign in with email"
- **Expected:**
  - HTML5 browser validation blocks submission.
  - No network login request is made.

### TC-012 [NEW] should mask entered password characters
- **Decision:** NEW → frontend/tests/e2e/hub.spec.ts
- **Requirements:** R-12 · **Technique:** security check
- **Priority:** P2 · **Tags:** @regression @hub · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5172/login
- **Steps:**
  1. Inspect password input attribute
- **Expected:**
  - Input has `type="password"`.

## 6. Coverage and Traceability
### Requirements Traceability Matrix (RTM)
| Requirement ID | Scenario | Priority | Technique | Decision |
|---|---|---|---|---|
| R-01 | TC-001 | P1 | UI verification | NEW → frontend/tests/e2e/hub.spec.ts |
| R-02 | TC-002 | P1 | Route guard | NEW → frontend/tests/e2e/hub.spec.ts |
| R-03 | TC-003 | P1 | Route guard | NEW → frontend/tests/e2e/hub.spec.ts |
| R-04 | TC-004 | P1 | Route guard | NEW → frontend/tests/e2e/hub.spec.ts |
| R-05 | TC-005 | P1 | Equivalence partitioning | NEW → frontend/tests/e2e/hub.spec.ts |
| R-06 | TC-006 | P2 | Integration check | NEW → frontend/tests/e2e/hub.spec.ts |
| R-07 | TC-007 | P2 | UI disclosure | NEW → frontend/tests/e2e/hub.spec.ts |
| R-08 | TC-008 | P3 | UI verification | NEW → frontend/tests/e2e/hub.spec.ts |
| R-09 | TC-009 | P1 | Error handling | NEW → frontend/tests/e2e/hub.spec.ts |
| R-10 | TC-010 | P2 | Boundary value analysis | NEW → frontend/tests/e2e/hub.spec.ts |
| R-11 | TC-011 | P2 | Validation check | NEW → frontend/tests/e2e/hub.spec.ts |
| R-12 | TC-012 | P2 | Security check | NEW → frontend/tests/e2e/hub.spec.ts |

## 7. Gaps and Risks
- **Top Risks:**
  1. Google Identity Services CDN script latency in test runner.  
     *Mitigation:* Web-first assertion with generous timeout for GIS ready-state.
- **Gaps:** Authenticated cross-portal workspace launchpad and knowledge management require active session with tenant memberships.
