---
plan_id: auth-sso
title: Authentication & SSO (GridHub) — Test Plan
version: 1.0
status: Approved
created: 2026-11-17
source: [url:https://localhost:5173/login, file:frontend/tests/e2e/auth.spec.ts, file:frontend/src/pages/Login.jsx, file:frontend/src/features/auth/PasswordSignIn.jsx, file:frontend/src/features/auth/GoogleSignIn.jsx]
target_env: local
ui_verified: 2026-11-17 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-17
approved_hash: 2e027fc29487d917
---

# Authentication & SSO (GridHub) — Test Plan

## 1. Overview
- **Objective:** Verify end-to-end authentication capabilities for the GridHub portal, including company email/password sign-in, Google SSO button rendering and identity initialization, troubleshooting help disclosure, input validation, and unauthenticated routing protection.
- **In scope:** 
  - Login page visual structure, portal branding (GridHub), and copy.
  - Email/password credential input, HTML5 form validation, submit states, and invalid credential alert handling.
  - Google Single Sign-On (SSO) button rendering via Google Identity Services (GIS) and backend configuration endpoint integration.
  - Collapsible "Trouble signing in?" assistance disclosure.
  - Route guard redirecting unauthenticated root requests (`/`) to `/login`.
  - Handling of `error` query parameter on the login route.
- **Out of scope:** 
  - Upstream third-party Google authentication popup consent screens and real Google account OTP/MFA challenges.
  - Backend database credential seeding or production identity provider administration.
  - Password reset / forgot password flow (not currently implemented in UI).
- **Assumptions:** 
  - Local Vite dev server runs over HTTPS on `https://localhost:5173` with proxy access to backend ApiGateway (`http://localhost:5204`).
  - Playwright browser context runs with `ignoreHTTPSErrors: true` to accept local self-signed dev certificates.
  - Google Identity Services client script (`https://accounts.google.com/gsi/client`) is accessible in non-isolated test environments.
- **Preconditions / test data needs:** 
  - GridHub dev server listening at `https://localhost:5173`.
  - Backend ApiGateway listening at `http://localhost:5204` and IdentityTenant at `http://localhost:5233`.
  - Synthetic test credentials: `TEST_USER_EMAIL` / `TEST_USER_PASSWORD` from environment variables for happy-path execution; deterministic synthetic invalid credentials for negative tests.
- **Change summary:** NEW 9 · UPDATE 2 · REUSE 3 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Display GridHub portal branding, page heading "Sign in to your organization", and workspace intro copy | UI / Branding | Login.jsx, auth.spec.ts:8 | Verified live via Playwright MCP |
| R-02 | Provide email/password sign-in form with accessible labels, correct input types, autocomplete, and submit button | Functional / UI | PasswordSignIn.jsx, auth.spec.ts:15 | Fields: email, password, button |
| R-03 | Initialize Google Identity Services and render Google SSO iframe button ("Continue with Google") | Integration / SSO | GoogleSignIn.jsx, auth.spec.ts:19 | Requires `/api/auth/google/configuration` |
| R-04 | Provide expandable "Trouble signing in?" disclosure displaying organization administrator contact guidance | UI / Usability | Login.jsx, auth.spec.ts:23 | `<details>` / `<summary>` component |
| R-05 | Display portal footer containing portal name ("GridHub") and copy ("Enterprise work management") | UI / Branding | Login.jsx, auth.spec.ts:30 | `<footer className="sso-footer">` |
| R-06 | Display accessible alert message when submitting incorrect email or password | Error handling | PasswordSignIn.jsx | Alert: "Email or password is incorrect. After repeated failed attempts, wait 15 minutes before trying again." |
| R-07 | Validate required inputs and email format using HTML5 validation | Form validation | PasswordSignIn.jsx | Required fields, email format |
| R-08 | Unauthenticated access to protected workspace routes (e.g. `/`) redirects to `/login` | Navigation / Auth Guard | App.jsx | ProtectedRoute component |
| R-09 | Display error notification and clear query string when navigating to `/login` with `?error=` parameter | Error handling | Login.jsx | `useNotice` toast notification |

### Entities and Fields
- **Email Field**: `name="email"`, `type="email"`, `required`, `maxLength=320`, `placeholder="you@company.com"`, `autoComplete="username"`.
- **Password Field**: `name="password"`, `type="password"`, `required`, `maxLength=128`, `autoComplete="current-password"`.
- **Sign In Button**: `type="submit"`, text: "Sign in with email", text while busy: "Please wait…".
- **Google SSO Button**: Rendered by Google Identity Services SDK inside container `#google-signin-btn`.

### Business Rules
- BR-01: IdentityTenant is the authority for password and SSO token issuance.
- BR-02: Form submission is locked while in-flight (`lock.current`) and fields are disabled (`fieldset disabled={busy}`).
- BR-03: Invalid authentication attempts display a rate-limiting warning after repeated failures.

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Are dedicated E2E test account credentials provisioned in local database for automated full sign-in? | Required for end-to-end happy path without mocking | Test sign-in happy path with synthetic env variables `TEST_USER_EMAIL` / `TEST_USER_PASSWORD`, falling back to network mock if not seeded. |
| Q-02 | Should Google SSO sign-in callback be mocked for automated E2E tests? | Third-party Google popup requires interactive manual login and CAPTCHA bypass | Mock backend Google token exchange endpoint `/api/auth/google` for automated E2E SSO test. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| E-1 | frontend/tests/e2e/auth.spec.ts:8 | should display login page and brand elements | REUSE | R-01 | Reused as-is; covers heading, brand logo, and portal title. |
| E-2 | frontend/tests/e2e/auth.spec.ts:15 | should display password sign-in form | UPDATE | R-02 | Existing test only asserts generic `.sso-actions`. Update to specifically assert email input, password input, and submit button attributes. |
| E-3 | frontend/tests/e2e/auth.spec.ts:19 | should display Google SSO sign-in option | UPDATE | R-03 | Existing test only asserts text regex. Update to wait for Google SSO iframe button rendering and assert successful initialization without error banner. |
| E-4 | frontend/tests/e2e/auth.spec.ts:23 | should support expanding trouble signing in help section | REUSE | R-04 | Reused as-is; covers clicking summary and checking expanded advice text. |
| E-5 | frontend/tests/e2e/auth.spec.ts:30 | should display footer with portal name | REUSE | R-05 | Reused as-is; covers footer visibility and portal name. |

**Impacted shared code:** None (no POM currently; tests use direct page locators).  
**Manual coverage found:** None recorded in repository.  
**Summary:** NEW 9 · UPDATE 2 · REUSE 3 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning (EP) & Boundary Value Analysis (BVA)
| Field | Valid Partition | Invalid Partition | Literal Test Value | Expected Outcome |
|---|---|---|---|---|
| Email | Well-formed company email | Missing '@' | `invalid.user-company.com` | HTML5 validation blocks submission |
| Email | Standard email | Empty string | `""` | HTML5 required validation triggers, field focused |
| Email | Max length (320 chars) | Exceeds max length | `310_chars@domain.com` | Input length capped by `maxLength=320` |
| Password | Non-empty password string | Empty string | `""` | HTML5 required validation triggers |
| Password | Correct user password | Incorrect password | `InvalidPassword123!` | Alert `role="alert"` displayed |

### Decision Table
| Conditions / Rules | Case 1 (Empty Form) | Case 2 (Invalid Format) | Case 3 (Bad Credentials) | Case 4 (Valid Credentials) |
|---|---|---|---|---|
| Email provided | N | Y | Y | Y |
| Email format valid | N/A | N | Y | Y |
| Password provided | N | Y | Y | Y |
| Credentials match store | N/A | N/A | N | Y |
| **Action: Submit allowed** | ✖ | ✖ | ✔ | ✔ |
| **Outcome** | Focus email | HTML5 error | Display alert | Navigate to dashboard |

### State Transition
- **Help Section:** Collapsed (`details` closed) --[click summary]--> Expanded (`details` open) --[click summary]--> Collapsed (`details` closed).
- **Submit Button:** Idle ("Sign in with email", enabled) --[submit]--> Submitting ("Please wait…", disabled) --[response]--> Idle ("Sign in with email", enabled).

## 4a. UI Verification
| TC | Step / screen | Checked (role + name, label, or text) | Observed | Result |
|---|---|---|---|---|
| TC-001 | Sign-in page | heading "Sign in to your organization" | heading "Sign in to your organization" [level=1] | ✔ |
| TC-001 | Sign-in page | link "GridHub home" | link "GridHub home", href: /login | ✔ |
| TC-001 | Sign-in page | portal badge & intro copy | "GridHub", "Enterprise work management", "Your projects..." | ✔ |
| TC-002 | Email form | textbox "Company email" | visible, placeholder "you@company.com", type "email" | ✔ |
| TC-002 | Email form | textbox "Password" | visible, type "password", autoComplete "current-password" | ✔ |
| TC-002 | Email form | button "Sign in with email" | visible, enabled | ✔ |
| TC-003 | Google SSO | button "Continue with Google. Opens in new tab" | visible, contains "Continue with Google", iframe loaded | ✔ |
| TC-004 | Help section | generic "Trouble signing in?" | visible, cursor=pointer | ✔ |
| TC-004 | Help section | help advice text | "Use the Google account provided by your organization..." | ✔ |
| TC-005 | Footer | contentinfo | visible, contains "GridHub" & "Enterprise work management" | ✔ |
| TC-007 | Sign-in failure | alert (role="alert") | "Email or password is incorrect. After repeated failed attempts, wait 15 minutes before trying again." | ✔ |

## 5. Scenarios

### TC-001 [REUSE] should display login page and brand elements
- **Decision:** REUSE → frontend/tests/e2e/auth.spec.ts:8 "should display login page and brand elements"
- **Requirements:** R-01 · **Technique:** use-case
- **Priority:** P1 · **Tags:** @smoke @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** none
- **Steps:**
  1. Open the login page (`/login`)
  2. Verify the brand header and main heading
- **Expected:**
  - Heading "Sign in to your organization" is visible
  - Brand header contains "GridHub" and link to "/login"

### TC-002 [UPDATE] should display password sign-in form with email and password inputs
- **Decision:** UPDATE → frontend/tests/e2e/auth.spec.ts:15 "should display password sign-in form"
- **Requirements:** R-02 · **Technique:** use-case / UI check
- **Priority:** P1 · **Tags:** @smoke @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** none
- **Changes:** Replace superficial `.sso-actions` locator check with explicit assertions for `textbox "Company email"` (with placeholder `you@company.com`), `textbox "Password"` (with `type="password"`), and `button "Sign in with email"`.
- **Steps:**
  1. Open the login page (`/login`)
  2. Verify the email textbox exists with placeholder "you@company.com"
  3. Verify the password textbox exists and has type "password"
  4. Verify the submit button with accessible name "Sign in with email" is visible
- **Expected:**
  - Email textbox is visible and enabled
  - Password textbox is visible and masked
  - "Sign in with email" button is visible and enabled

### TC-003 [UPDATE] should display Google SSO sign-in option with rendered GIS button
- **Decision:** UPDATE → frontend/tests/e2e/auth.spec.ts:19 "should display Google SSO sign-in option"
- **Requirements:** R-03 · **Technique:** use-case / integration
- **Priority:** P2 · **Tags:** @regression @auth @sso · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** backend ApiGateway reachable
- **Data:** none
- **Changes:** Wait for GIS iframe initialization to eliminate "Loading Google sign-in…" status, and assert button container and iframe presence without error notice banner.
- **Steps:**
  1. Open the login page (`/login`)
  2. Wait for Google sign-in container to finish loading
  3. Verify Google sign-in button or iframe is visible
  4. Verify no error alert banner is displayed
- **Expected:**
  - Google SSO button is rendered with text "Continue with Google"
  - No error toast or failure notice is shown

### TC-004 [REUSE] should support expanding trouble signing in help section
- **Decision:** REUSE → frontend/tests/e2e/auth.spec.ts:23 "should support expanding trouble signing in help section"
- **Requirements:** R-04 · **Technique:** use-case
- **Priority:** P2 · **Tags:** @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** none
- **Steps:**
  1. Open the login page (`/login`)
  2. Click on the "Trouble signing in?" summary
  3. Verify help text is visible
- **Expected:**
  - Text "Use the Google account provided by your organization" is visible

### TC-005 [REUSE] should display footer with portal name
- **Decision:** REUSE → frontend/tests/e2e/auth.spec.ts:30 "should display footer with portal name"
- **Requirements:** R-05 · **Technique:** UI check
- **Priority:** P2 · **Tags:** @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** none
- **Steps:**
  1. Open the login page (`/login`)
  2. Verify the footer is visible and contains "GridHub"
- **Expected:**
  - Footer contains portal title "GridHub" and badge "Enterprise work management"

### TC-006 [NEW] should prevent submission and focus email field when submitting empty form
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (Form validation suite)
- **Requirements:** R-07 · **Technique:** boundary value analysis / HTML5 validation
- **Priority:** P2 · **Tags:** @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** email = ""; password = ""
- **Steps:**
  1. Open the login page (`/login`)
  2. Leave email and password inputs empty
  3. Click "Sign in with email" button
- **Expected:**
  - Form submission is prevented by browser validation
  - Company email textbox remains active/focused
  - No network request to `/api/auth/password/login` is dispatched

### TC-007 [NEW] should display alert message when submitting invalid email or password
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (Authentication negative path)
- **Requirements:** R-06 · **Technique:** error guessing / negative path
- **Priority:** P1 · **Tags:** @smoke @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** email = "invalid.user@example.com"; password = "InvalidPassword123!"
- **Steps:**
  1. Open the login page (`/login`)
  2. Enter "invalid.user@example.com" in Company email textbox
  3. Enter "InvalidPassword123!" in Password textbox
  4. Click "Sign in with email" button
- **Expected:**
  - An element with `role="alert"` appears
  - Alert displays text: "Email or password is incorrect. After repeated failed attempts, wait 15 minutes before trying again."
  - Form fields remain populated with entered values

### TC-008 [NEW] should reject invalid email syntax without '@' symbol
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (Field format validation)
- **Requirements:** R-07 · **Technique:** equivalence partitioning
- **Priority:** P2 · **Tags:** @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** email = "notanemail"; password = "ValidPassword123!"
- **Steps:**
  1. Open the login page (`/login`)
  2. Enter "notanemail" in Company email textbox
  3. Enter "ValidPassword123!" in Password textbox
  4. Click "Sign in with email" button
- **Expected:**
  - HTML5 email validation rejects submission
  - Email input matches `:invalid` CSS pseudo-class
  - No network request is dispatched

### TC-009 [NEW] should redirect unauthenticated user from root URL to login page
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (Route guard)
- **Requirements:** R-08 · **Technique:** state transition / routing
- **Priority:** P1 · **Tags:** @smoke @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** no active session cookie
- **Data:** none
- **Steps:**
  1. Navigate directly to `/`
  2. Wait for route resolution
- **Expected:**
  - Browser is redirected to `/login`
  - Page URL pathname equals `/login`
  - Login card heading "Sign in to your organization" is visible

### TC-010 [NEW] should support collapsing help section when clicked again
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (UI interaction)
- **Requirements:** R-04 · **Technique:** state transition
- **Priority:** P3 · **Tags:** @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** none
- **Steps:**
  1. Open the login page (`/login`)
  2. Click "Trouble signing in?" summary to expand
  3. Verify help text is visible
  4. Click "Trouble signing in?" summary again to collapse
- **Expected:**
  - The `<details>` element loses its `open` attribute
  - Help text is no longer visible

### TC-011 [NEW] should mask entered password characters
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (Security UI check)
- **Requirements:** R-02 · **Technique:** security / UI check
- **Priority:** P2 · **Tags:** @regression @security @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** password = "SecretPassword123!"
- **Steps:**
  1. Open the login page (`/login`)
  2. Enter "SecretPassword123!" in Password textbox
  3. Inspect the password input element type attribute
- **Expected:**
  - Input attribute `type` equals "password"
  - Password text is not rendered as clear text

### TC-012 [NEW] should disable form fieldset and show busy text during email sign-in submission
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (Resilience / concurrency)
- **Requirements:** R-02, BR-02 · **Technique:** state transition
- **Priority:** P2 · **Tags:** @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** route delayed response via route interception
- **Data:** email = "test@example.com"; password = "Password123!"
- **Steps:**
  1. Intercept `/api/auth/password/login` with a simulated 1-second delay
  2. Fill email and password fields
  3. Click "Sign in with email"
  4. Inspect fieldset and submit button while request is in flight
- **Expected:**
  - The `<fieldset>` element is disabled
  - The submit button displays text "Please wait…"
  - Fieldset is re-enabled once request completes

### TC-013 [NEW] should display error notification if Google SSO configuration endpoint fails
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (Resilience / fallback)
- **Requirements:** R-03 · **Technique:** error guessing / resilience
- **Priority:** P2 · **Tags:** @regression @auth @sso · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** none
- **Steps:**
  1. Intercept `/api/auth/google/configuration` and return HTTP 500 error
  2. Navigate to `/login`
  3. Wait for Google sign-in failure handling
- **Expected:**
  - An error notice or alert message is shown indicating Google sign-in is unavailable
  - Email/password sign-in remains interactive and functional

### TC-014 [NEW] should display error notice when landing with login error query parameter
- **Decision:** NEW → frontend/tests/e2e/auth.spec.ts (Integration error handling)
- **Requirements:** R-09 · **Technique:** use-case / error handling
- **Priority:** P2 · **Tags:** @regression @auth · **Automation:** auto
- **Role:** unauthenticated visitor · **Preconditions:** none
- **Data:** query parameter `?error=account_locked`
- **Steps:**
  1. Navigate to `/login?error=account_locked`
  2. Observe displayed notices
  3. Inspect current URL
- **Expected:**
  - Error notice banner/toast is displayed with appropriate message
  - The `error` query parameter is stripped from the URL without triggering a page reload

## 6. Coverage and Traceability
### Dimensions Checklist
| # | Dimension | Status | Scenario IDs / Justification |
|---|---|---|---|
| D1 | Functional | Covered | TC-001, TC-002, TC-003, TC-004, TC-005, TC-007, TC-009 |
| D2 | Data | Covered | TC-006 (empty), TC-007 (invalid), TC-008 (bad format) |
| D3 | UI behaviour | Covered | TC-002, TC-003, TC-004, TC-010 (collapse), TC-012 (busy state) |
| D4 | Cross-browser | Covered | Project config defines Chromium, Firefox, WebKit |
| D5 | Accessibility | Covered | TC-001 (heading hierarchy), TC-002 (labels), TC-007 (role="alert") |
| D6 | API integration | Covered | TC-007 (`/api/auth/password/login`), TC-013 (`/api/auth/google/configuration`) |
| D7 | Security | Covered | TC-007 (rate-limit notice), TC-009 (auth guard), TC-011 (masked password) |
| D8 | Integration | Covered | TC-003 (Google Identity Services), TC-014 (URL error params) |
| D9 | Resilience | Covered | TC-012 (in-flight state), TC-013 (API 500 failure handling) |
| D10 | Concurrency | Covered | TC-012 (button disabled during submit prevents double-submit) |
| D11 | Compliance & audit | Covered | TC-001 (Access managed disclaimer), TC-004 (admin contact instructions) |
| D12 | Localisation | N/A | English only in current release |
| D13 | Performance | N/A | Dedicated performance testing out of scope for E2E suite |
| D14 | Regression | Covered | TC-001, TC-002, TC-003, TC-004, TC-005 (covers all existing tests in `auth.spec.ts`) |
| D15 | Migration | N/A | No system migration involved |

### Requirements Traceability Matrix (RTM)
| Requirement | Scenarios | Priority | Automation | Status |
|---|---|---|---|---|
| R-01 (Branding & Layout) | TC-001 | P1 | auto | Covered |
| R-02 (Password Sign-in Form) | TC-002, TC-011, TC-012 | P1, P2 | auto | Covered |
| R-03 (Google SSO Button) | TC-003, TC-013 | P2 | auto | Covered |
| R-04 (Trouble Signing In Help) | TC-004, TC-010 | P2, P3 | auto | Covered |
| R-05 (Footer) | TC-005 | P2 | auto | Covered |
| R-06 (Authentication Failure Alert) | TC-007 | P1 | auto | Covered |
| R-07 (Input Validation) | TC-006, TC-008 | P2 | auto | Covered |
| R-08 (Route Protection / Redirect) | TC-009 | P1 | auto | Covered |
| R-09 (Query Error Handling) | TC-014 | P2 | auto | Covered |

## 7. Gaps and Risks
### Gap Report
1. **Requirements with no scenario:** None. All identified requirements have corresponding scenarios.
2. **Scenarios with no requirement:** None. All scenarios map to requirements R-01 through R-09.
3. **Duplicates of existing coverage:** None. Existing coverage in `frontend/tests/e2e/auth.spec.ts` is either updated (TC-002, TC-003) or reused (TC-001, TC-004, TC-005).

### Top Risks and Mitigations
1. **Risk:** Third-party Google Identity Services script CDN latency or outage causing flaky tests.  
   **Mitigation:** Wait explicitly for `iframe` or container state; test resilience with mock configuration response (TC-013).
2. **Risk:** Self-signed SSL certificate on dev HTTPS causing connection failures in headless browsers.  
   **Mitigation:** Maintain `ignoreHTTPSErrors: true` in `playwright.config.ts`.
3. **Risk:** Repeated authentication failures triggering rate limiting / IP lockouts in local backend.  
   **Mitigation:** Restrict negative credential tests to dedicated test partitions and maintain low failure iterations.
