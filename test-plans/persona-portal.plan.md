---
plan_id: persona-portal
title: Persona Portal (Employee Directory & Profile) — Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5174, file:frontend/persona/src/routes.jsx, file:frontend/persona/src/main.jsx, file:frontend/src/App.jsx, file:frontend/src/pages/Login.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 8905bad01152c94c
---

# Persona Portal (Employee Directory & Profile) — Test Plan

## 1. Overview
- **Objective:** Verify public authentication entry, portal branding, copy, and unauthenticated route protections for the Persona Employee portal (`@gridhub/persona` on `https://localhost:5174`).
- **In scope:**
  - Persona portal login page: banner "Persona home", heading "Sign in to your organization", portal badges ("Persona", "Employee portal"), and intro copy ("Your profile, career, and organization.Connected through Persona.").
  - Company email/password form inputs, submission validation, and invalid credential alert handling.
  - Google Single Sign-On (GIS) button rendering and error banner absence.
  - Collapsible "Trouble signing in?" disclosure.
  - Persona footer branding ("Persona", "Employee portal").
  - Protected route guards: unauthenticated access to `/`, `/profile`, `/employees`, `/employees/leave`, and `/employees/org-chart` redirects to `/login`.
- **Out of scope:**
  - Live third-party Google account authentication and MFA challenges.
  - Live employee profile updates or org chart modifications (requires authenticated session with `EMPLOYEE_VIEW` / `EMPLOYEE_CREATE` permissions).
- **Assumptions:**
  - Persona dev server runs over local HTTPS on `https://localhost:5174` with dev SSL certificates.
  - Playwright browser context runs with `ignoreHTTPSErrors: true`.
  - Backend API Gateway (`http://localhost:5204`) and IdentityTenant (`http://localhost:5233`) remain accessible for `/api` requests.
- **Preconditions / test data needs:**
  - Persona dev server listening on `https://localhost:5174`.
- **Change summary:** NEW 12 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Display Persona portal branding, header "Persona home", heading "Sign in to your organization", badges ("Persona", "Employee portal"), and copy "Your profile, career, and organization.Connected through Persona." | UI / Branding | Login.jsx, routes.jsx | Verified live via Playwright MCP |
| R-02 | Redirect unauthenticated root access (`/`) to `/login` | Navigation / Guard | App.jsx | ProtectedRoute |
| R-03 | Redirect unauthenticated access to `/profile` to `/login` | Navigation / Guard | routes.jsx, App.jsx | Access guard |
| R-04 | Redirect unauthenticated access to `/employees` to `/login` | Navigation / Guard | routes.jsx, App.jsx | Access guard |
| R-05 | Redirect unauthenticated access to `/employees/leave` to `/login` | Navigation / Guard | routes.jsx, App.jsx | Access guard |
| R-06 | Redirect unauthenticated access to `/employees/org-chart` to `/login` | Navigation / Guard | routes.jsx, App.jsx | Access guard |
| R-07 | Provide company email textbox (with placeholder `you@company.com`) and password textbox with accessible labels | Functional / UI | PasswordSignIn.jsx | Form fields |
| R-08 | Render Google Identity Services (GIS) button ("Continue with Google. Opens in new tab") without error alert | Integration / SSO | GoogleSignIn.jsx | Verified live via Playwright MCP |
| R-09 | Provide expandable "Trouble signing in?" disclosure showing organization administrator guidance | UI / Usability | Login.jsx | `<details>` / `<summary>` |
| R-10 | Display footer containing "Persona" and "Employee portal" brand labels | UI / Branding | Login.jsx | `<footer className="sso-footer">` |
| R-11 | Display accessible alert message when submitting incorrect email or password | Error handling | PasswordSignIn.jsx | Alert on invalid credentials |
| R-12 | Prevent form submission and focus email field when submitting empty form | Validation | PasswordSignIn.jsx | HTML5 required validation |

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Will authenticated employee onboarding and org chart scenarios be planned in a separate plan? | Requires user session with `EMPLOYEE_VIEW` / `EMPLOYEE_CREATE` permissions | Yes, this plan covers Persona portal entry and route access guards; authenticated directory and profile management will be planned separately. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| - | None | - | - | - | No existing tests for Persona portal (`@gridhub/persona`). |

**Impacted shared code:** None. New spec will be created in `frontend/tests/e2e/persona.spec.ts`.  
**Summary:** NEW 12 · UPDATE 0 · REUSE 0 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning (EP) & Boundary Value Analysis (BVA)
| Route / State | Target Route | Expected Redirect | Auth State |
|---|---|---|---|
| Protected root | `/` | `/login` | Unauthenticated |
| Protected profile | `/profile` | `/login` | Unauthenticated |
| Protected directory | `/employees` | `/login` | Unauthenticated |
| Protected leave | `/employees/leave` | `/login` | Unauthenticated |
| Protected org-chart | `/employees/org-chart` | `/login` | Unauthenticated |

## 4a. UI Verification
| TC | Step / screen | Checked (role + name, label, or text) | Observed | Result |
|---|---|---|---|---|
| TC-001 | Login page | link "Persona home", heading "Sign in to your organization", text "Your profile, career, and organization.Connected through Persona." | Visible, exact | ✔ |
| TC-002 | `/` | Navigation to `/` | Redirects to `/login` | ✔ |
| TC-003 | `/profile` | Navigation to `/profile` | Redirects to `/login` | ✔ |
| TC-004 | `/employees` | Navigation to `/employees` | Redirects to `/login` | ✔ |
| TC-005 | `/employees/leave` | Navigation to `/employees/leave` | Redirects to `/login` | ✔ |
| TC-006 | `/employees/org-chart` | Navigation to `/employees/org-chart` | Redirects to `/login` | ✔ |
| TC-007 | Form inputs | textbox "Company email", textbox "Password", button "Sign in with email" | Visible, enabled | ✔ |
| TC-008 | Google SSO | button "Continue with Google. Opens in new tab" | Rendered inside GIS iframe | ✔ |
| TC-009 | Disclosure | group "Trouble signing in?" | Expands on click | ✔ |
| TC-010 | Footer | generic "Persona", generic "Employee portal" | Visible in contentinfo | ✔ |
| TC-011 | Invalid submit | text "Email or password is incorrect. After repeated failed attempts, wait 15 minutes before trying again." | Error alert displayed | ✔ |
| TC-012 | Empty submit | HTML5 required validation on email input | Blocks submission, focuses email | ✔ |

## 5. Scenarios

### TC-001 [NEW] should display Persona login page branding and copy
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-01 · **Technique:** UI verification
- **Priority:** P1 · **Tags:** @smoke @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** Persona dev server running on https://localhost:5174
- **Steps:**
  1. Navigate to https://localhost:5174/login
  2. Verify link "Persona home" is visible with `href="/login"`
  3. Verify heading "Sign in to your organization" is visible
  4. Verify intro text contains "Your profile, career, and organization.Connected through Persona."
- **Expected:**
  - Persona branding, badges, and copy are visible.

### TC-002 [NEW] should redirect unauthenticated user from root URL to login page
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-02 · **Technique:** route guard
- **Priority:** P1 · **Tags:** @smoke @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** no active session
- **Steps:**
  1. Navigate to https://localhost:5174/
  2. Wait for navigation to complete
- **Expected:**
  - URL ends with `/login`.
  - Login form is visible.

### TC-003 [NEW] should redirect unauthenticated user from /profile to login page
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-03 · **Technique:** route guard
- **Priority:** P1 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** no active session
- **Steps:**
  1. Navigate to https://localhost:5174/profile
- **Expected:**
  - URL ends with `/login`.

### TC-004 [NEW] should redirect unauthenticated user from /employees to login page
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-04 · **Technique:** route guard
- **Priority:** P1 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** no active session
- **Steps:**
  1. Navigate to https://localhost:5174/employees
- **Expected:**
  - URL ends with `/login`.

### TC-005 [NEW] should redirect unauthenticated user from /employees/leave to login page
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-05 · **Technique:** route guard
- **Priority:** P1 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** no active session
- **Steps:**
  1. Navigate to https://localhost:5174/employees/leave
- **Expected:**
  - URL ends with `/login`.

### TC-006 [NEW] should redirect unauthenticated user from /employees/org-chart to login page
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-06 · **Technique:** route guard
- **Priority:** P1 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** no active session
- **Steps:**
  1. Navigate to https://localhost:5174/employees/org-chart
- **Expected:**
  - URL ends with `/login`.

### TC-007 [NEW] should display email and password inputs with correct placeholders
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-07 · **Technique:** equivalence partitioning
- **Priority:** P1 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5174/login
- **Steps:**
  1. Verify textbox "Company email" has placeholder "you@company.com" and attribute `type="email"`
  2. Verify password textbox has attribute `type="password"`
  3. Verify button "Sign in with email" is visible and enabled
- **Expected:**
  - Form controls conform to specification.

### TC-008 [NEW] should render Google SSO button without error notice
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-08 · **Technique:** integration check
- **Priority:** P2 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5174/login
- **Steps:**
  1. Wait for Google Identity Services initialization
  2. Verify button "Continue with Google. Opens in new tab" is visible
  3. Verify no alert banner is rendered in `.google-identity-signin`
- **Expected:**
  - Google button rendered successfully in active iframe.

### TC-009 [NEW] should expand help section when clicking Trouble signing in
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-09 · **Technique:** UI disclosure
- **Priority:** P2 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5174/login
- **Steps:**
  1. Click on "Trouble signing in?" summary element
  2. Verify expanded guidance text is visible
- **Expected:**
  - Details disclosure opens and displays guidance text.

### TC-010 [NEW] should display footer branding with Persona and Employee portal
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-10 · **Technique:** UI verification
- **Priority:** P3 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5174/login
- **Steps:**
  1. Locate footer with class `.sso-footer`
  2. Verify "Persona" and "Employee portal" are visible inside footer
- **Expected:**
  - Footer branding displays properly.

### TC-011 [NEW] should display alert message when submitting invalid email or password
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-11 · **Technique:** error handling
- **Priority:** P1 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5174/login
- **Steps:**
  1. Enter invalid email "unregistered.user@example.com"
  2. Enter invalid password "WrongPassword123!"
  3. Click "Sign in with email"
- **Expected:**
  - Alert displays text "Email or password is incorrect. After repeated failed attempts, wait 15 minutes before trying again."

### TC-012 [NEW] should prevent submission and focus email field when submitting empty form
- **Decision:** NEW → frontend/tests/e2e/persona.spec.ts
- **Requirements:** R-12 · **Technique:** boundary value analysis
- **Priority:** P2 · **Tags:** @regression @persona · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5174/login
- **Steps:**
  1. Leave email and password blank
  2. Click "Sign in with email"
- **Expected:**
  - Form is not submitted.
  - Email field is focused.

## 6. Coverage and Traceability
### Requirements Traceability Matrix (RTM)
| Requirement ID | Scenario | Priority | Technique | Decision |
|---|---|---|---|---|
| R-01 | TC-001 | P1 | UI verification | NEW → frontend/tests/e2e/persona.spec.ts |
| R-02 | TC-002 | P1 | Route guard | NEW → frontend/tests/e2e/persona.spec.ts |
| R-03 | TC-003 | P1 | Route guard | NEW → frontend/tests/e2e/persona.spec.ts |
| R-04 | TC-004 | P1 | Route guard | NEW → frontend/tests/e2e/persona.spec.ts |
| R-05 | TC-005 | P1 | Route guard | NEW → frontend/tests/e2e/persona.spec.ts |
| R-06 | TC-006 | P1 | Route guard | NEW → frontend/tests/e2e/persona.spec.ts |
| R-07 | TC-007 | P1 | Equivalence partitioning | NEW → frontend/tests/e2e/persona.spec.ts |
| R-08 | TC-008 | P2 | Integration check | NEW → frontend/tests/e2e/persona.spec.ts |
| R-09 | TC-009 | P2 | UI disclosure | NEW → frontend/tests/e2e/persona.spec.ts |
| R-10 | TC-010 | P3 | UI verification | NEW → frontend/tests/e2e/persona.spec.ts |
| R-11 | TC-011 | P1 | Error handling | NEW → frontend/tests/e2e/persona.spec.ts |
| R-12 | TC-012 | P2 | Boundary value analysis | NEW → frontend/tests/e2e/persona.spec.ts |

## 7. Gaps and Risks
- **Top Risks:**
  1. Google Identity Services CDN script latency in test runner.  
     *Mitigation:* Web-first assertion with generous timeout for GIS ready-state.
- **Gaps:** Authenticated directory browsing, profile editing, and org chart operations require active session with `EMPLOYEE_VIEW` permissions.
