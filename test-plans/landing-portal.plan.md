---
plan_id: landing-portal
title: Landing Portal (LaunchPad & Email Verification) — Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5171, file:frontend/src/LandingApp.jsx, file:frontend/src/pages/LaunchPad.jsx, file:frontend/src/pages/VerifyEmail.jsx, file:frontend/src/pages/Login.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: e922c2ea93a91ad2
---

# Landing Portal (LaunchPad & Email Verification) — Test Plan

## 1. Overview
- **Objective:** Verify end-to-end functionality for the Landing Portal (`@gridhub/landing` on `https://localhost:5171`), including the LaunchPad entry experience, public authentication, email verification password setup at `/verify-email`, unauthenticated navigation guards, and error states.
- **In scope:**
  - LaunchPad public login page: branding, copy ("Sign in once, then open the portals your company has"), email/password inputs, Google SSO button rendering, and footer.
  - Route protection: unauthenticated navigation to `/` and protected child routes redirects to `/login`.
  - Email verification workflow (`/verify-email`):
    - Missing token alert and fallback guidance when visited without token hash.
    - Password setup form rendering when visited with `#token=...` hash.
    - URL hash sanitation (`replaceState`) to remove token from address bar.
    - Client-side validation: password mismatch check ("Passwords do not match.") and whitespace-only check ("Choose a password containing more than spaces.").
    - Navigation link back to `/login`.
- **Out of scope:**
  - Third-party Google OAuth popup consent and external SMS/MFA verification.
  - Production SMTP email delivery pipelines.
  - Multi-portal launch routing to authenticated external subdomains in live production.
- **Assumptions:**
  - Landing dev server runs over local HTTPS on `https://localhost:5171` with dev SSL certificates.
  - Playwright browser context runs with `ignoreHTTPSErrors: true`.
  - Backend API Gateway (`http://localhost:5204`) and IdentityTenant (`http://localhost:5233`) remain accessible for proxied `/api` requests.
- **Preconditions / test data needs:**
  - Landing dev server listening on `https://localhost:5171`.
  - Synthetic token string (e.g. `test-verification-token-12345`) for token-present verification tests.
- **Change summary:** NEW 12 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Display LaunchPad portal branding, header "LaunchPad home", heading "Sign in to your organization", and tagline "Sign in once, then open the portals your company has" | UI / Branding | LandingApp.jsx, Login.jsx | Verified live via Playwright MCP |
| R-02 | Redirect unauthenticated root access (`/`) to `/login` | Navigation / Guard | LandingApp.jsx | ProtectedRoute wrapper |
| R-03 | Provide company email textbox (with placeholder `you@company.com`) and password textbox with accessible labels | Functional / UI | PasswordSignIn.jsx | Form fields |
| R-04 | Render Google Identity Services (GIS) button ("Continue with Google. Opens in new tab") without error alert | Integration / SSO | GoogleSignIn.jsx | Verified live via Playwright MCP |
| R-05 | Provide expandable "Trouble signing in?" disclosure showing organization administrator guidance | UI / Usability | Login.jsx | `<details>` / `<summary>` |
| R-06 | Display footer containing "LaunchPad" and "AIWorkHub" brand labels | UI / Branding | Login.jsx | `<footer className="sso-footer">` |
| R-07 | Display alert "Open the verification link from your email..." when visiting `/verify-email` without token | Error handling | VerifyEmail.jsx | Missing token state |
| R-08 | Display password setup form when visiting `/verify-email` with `#token=...` hash parameter | Functional / UI | VerifyEmail.jsx | Inputs: `new password`, `confirm password` |
| R-09 | Remove token hash from window URL address bar via `history.replaceState` upon loading `/verify-email` | Security / Privacy | VerifyEmail.jsx | Prevents token retention in history |
| R-10 | Reject mismatched passwords with inline error message "Passwords do not match." | Validation | VerifyEmail.jsx | Client validation before API call |
| R-11 | Reject whitespace-only passwords with error "Choose a password containing more than spaces." | Validation | VerifyEmail.jsx | Client validation before API call |
| R-12 | Provide "Return to sign in" link on `/verify-email` navigating to `/login` | Navigation | VerifyEmail.jsx | `<Link to="/login">` |

## 3. Open Questions
| ID | Question | Why it matters | Safe default if unanswered |
|---|---|---|---|
| Q-01 | Should mock API be used for successful email verification submission? | Real verification tokens require active database token creation | Test client-side validations directly; mock backend POST `/api/auth/verify` to test completion state. |

## 3a. Existing Coverage Impact
| # | Existing test (file:line) | Title / ID | Decision | Requirement | Change / reason |
|---|---|---|---|---|---|
| - | None | - | - | - | No existing tests for Landing portal (`@gridhub/landing`). Existing tests in `auth.spec.ts` target GridHub portal (`@gridhub/gridhub`). |

**Impacted shared code:** None. New spec will be authored in `frontend/tests/e2e/landing.spec.ts`.  
**Summary:** NEW 12 · UPDATE 0 · REUSE 0 · RETIRE 0

## 4. Test Design
### Equivalence Partitioning (EP) & Boundary Value Analysis (BVA)
| Field / State | Valid Partition | Invalid Partition | Literal Test Value | Expected Outcome |
|---|---|---|---|---|
| Token presence | Token provided in hash | No hash or empty token | `#token=valid-test-token-12345` vs empty | Shows form vs shows alert |
| Password confirmation | Identical passwords | Non-matching passwords | `NewSecretPass123!` vs `DifferentPass123!` | "Passwords do not match." alert |
| Password content | Alphanumeric passphrase | Whitespace-only string | `"            "` | "Choose a password containing more than spaces." |
| Password length | 12–128 characters | Less than 12 characters | `Min12CharPass!` | Form validation attribute `minLength=12` |

## 4a. UI Verification
| TC | Step / screen | Checked (role + name, label, or text) | Observed | Result |
|---|---|---|---|---|
| TC-001 | Login page | link "LaunchPad home", heading "Sign in to your organization", paragraph "Sign in once, then open the portals your company has" | Visible, text exact | ✔ |
| TC-002 | Root `/` | Navigation to `/` | Redirects to `/login` | ✔ |
| TC-003 | Form inputs | textbox "Company email", textbox "Password", button "Sign in with email" | Visible, enabled | ✔ |
| TC-004 | Google SSO | button "Continue with Google. Opens in new tab" | Rendered inside GIS iframe | ✔ |
| TC-005 | Disclosure | group "Trouble signing in?" | Expands on click, shows admin contact guidance | ✔ |
| TC-006 | Footer | generic "LaunchPad", generic "AIWorkHub" | Visible in contentinfo | ✔ |
| TC-007 | `/verify-email` | alert "Open the verification link from your email..." | Visible when visiting without hash | ✔ |
| TC-008 | `/verify-email#token=...` | textbox "New password", textbox "Confirm password", button "Verify and set password" | Visible when visiting with hash token | ✔ |
| TC-009 | URL hash | `window.location.hash` after mount | Empty string (cleaned) | ✔ |
| TC-010 | Submit mismatch | text "Passwords do not match." | Error message displayed | ✔ |
| TC-011 | Submit spaces | text "Choose a password containing more than spaces." | Error message displayed | ✔ |
| TC-012 | Navigation link | link "Return to sign in" | Visible with `href="/login"` | ✔ |

## 5. Scenarios

### TC-001 [NEW] should display LaunchPad login page branding and tagline
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-01 · **Technique:** UI verification
- **Priority:** P1 · **Tags:** @smoke @regression @landing · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** Landing portal running on https://localhost:5171
- **Steps:**
  1. Navigate to https://localhost:5171/login
  2. Verify link "LaunchPad home" is visible with `href="/login"`
  3. Verify heading "Sign in to your organization" is visible
  4. Verify intro text contains "Sign in once, then open the portals your company has"
- **Expected:**
  - LaunchPad portal title and branding elements are visible.

### TC-002 [NEW] should redirect unauthenticated user from root URL to login page
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-02 · **Technique:** state transition / route guard
- **Priority:** P1 · **Tags:** @smoke @regression @landing · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** user has no active session cookie
- **Steps:**
  1. Navigate to https://localhost:5171/
  2. Wait for navigation to complete
- **Expected:**
  - Current URL path is `/login`.
  - Login form is visible.

### TC-003 [NEW] should display email and password inputs with correct placeholders
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-03 · **Technique:** equivalence partitioning
- **Priority:** P1 · **Tags:** @regression @landing · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5171/login
- **Steps:**
  1. Verify textbox "Company email" has placeholder "you@company.com" and attribute `type="email"`
  2. Verify password textbox has attribute `type="password"`
  3. Verify button "Sign in with email" is visible and enabled
- **Expected:**
  - Form controls conform to specification.

### TC-004 [NEW] should render Google SSO button without error notice
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-04 · **Technique:** integration check
- **Priority:** P2 · **Tags:** @regression @landing · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5171/login
- **Steps:**
  1. Wait for Google Identity Services initialization
  2. Verify button "Continue with Google. Opens in new tab" is visible
  3. Verify no alert banner is rendered in `.google-identity-signin`
- **Expected:**
  - Google button rendered successfully in active iframe.

### TC-005 [NEW] should expand help section when clicking Trouble signing in
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-05 · **Technique:** UI disclosure
- **Priority:** P2 · **Tags:** @regression @landing · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5171/login
- **Steps:**
  1. Click on "Trouble signing in?" summary element
  2. Verify expanded advice text is visible
- **Expected:**
  - Details disclosure opens and displays guidance text.

### TC-006 [NEW] should display footer branding with LaunchPad and AIWorkHub
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-06 · **Technique:** UI verification
- **Priority:** P3 · **Tags:** @regression @landing · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5171/login
- **Steps:**
  1. Locate footer with class `.sso-footer`
  2. Verify "LaunchPad" and "AIWorkHub" are visible inside footer
- **Expected:**
  - Footer branding displays properly.

### TC-007 [NEW] should display alert on verify-email when accessed without token
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-07 · **Technique:** error guessing
- **Priority:** P1 · **Tags:** @smoke @regression @landing · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** user navigates directly to `/verify-email` without hash
- **Steps:**
  1. Navigate to https://localhost:5171/verify-email
  2. Verify heading "Verify email and set password" is visible
  3. Verify alert with text "Open the verification link from your email..." is displayed
  4. Verify password inputs are not displayed
- **Expected:**
  - Missing token error state prevents form access.

### TC-008 [NEW] should display password setup form on verify-email when token hash is provided
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-08 · **Technique:** boundary value analysis
- **Priority:** P1 · **Tags:** @regression @landing · **Automation:** auto
- **Role:** invited user · **Preconditions:** user opens verification link with token hash
- **Steps:**
  1. Navigate to https://localhost:5171/verify-email#token=valid-test-token-12345
  2. Verify textbox "New password" is visible and has attributes `type="password"`, `minLength=12`, `maxLength=128`
  3. Verify textbox "Confirm password" is visible and has attributes `type="password"`, `minLength=12`, `maxLength=128`
  4. Verify button "Verify and set password" is visible
- **Expected:**
  - Password setup form is presented to the user.

### TC-009 [NEW] should sanitize token hash from address bar after mount
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-09 · **Technique:** security check
- **Priority:** P2 · **Tags:** @security @regression @landing · **Automation:** auto
- **Role:** invited user · **Preconditions:** navigated to `/verify-email#token=valid-test-token-12345`
- **Steps:**
  1. Check `window.location.hash` after page load
- **Expected:**
  - `window.location.hash` is empty string (`""`).
  - URL displayed in browser is `/verify-email`.

### TC-010 [NEW] should display error when password and confirm password do not match
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-10 · **Technique:** equivalence partitioning (invalid)
- **Priority:** P2 · **Tags:** @regression @landing · **Automation:** auto
- **Role:** invited user · **Preconditions:** password setup form visible on `/verify-email`
- **Steps:**
  1. Enter "SecurePassword123!" in "New password"
  2. Enter "DifferentPass456!" in "Confirm password"
  3. Click "Verify and set password"
- **Expected:**
  - Alert paragraph displays text "Passwords do not match."
  - Form submission is blocked.

### TC-011 [NEW] should display error when password contains only whitespace
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-11 · **Technique:** boundary value analysis (whitespace)
- **Priority:** P2 · **Tags:** @regression @landing · **Automation:** auto
- **Role:** invited user · **Preconditions:** password setup form visible on `/verify-email`
- **Steps:**
  1. Enter 12 spaces in "New password"
  2. Enter 12 spaces in "Confirm password"
  3. Click "Verify and set password"
- **Expected:**
  - Alert paragraph displays text "Choose a password containing more than spaces."

### TC-012 [NEW] should navigate back to login page from Return to sign in link
- **Decision:** NEW → frontend/tests/e2e/landing.spec.ts
- **Requirements:** R-12 · **Technique:** navigation flow
- **Priority:** P2 · **Tags:** @regression @landing · **Automation:** auto
- **Role:** anonymous visitor · **Preconditions:** on https://localhost:5171/verify-email
- **Steps:**
  1. Click link "Return to sign in"
  2. Wait for navigation
- **Expected:**
  - URL is `/login`.
  - Login page elements are visible.

## 6. Coverage and Traceability
### Requirements Traceability Matrix (RTM)
| Requirement ID | Scenario | Priority | Technique | Decision |
|---|---|---|---|---|
| R-01 | TC-001 | P1 | UI verification | NEW → frontend/tests/e2e/landing.spec.ts |
| R-02 | TC-002 | P1 | Route guard | NEW → frontend/tests/e2e/landing.spec.ts |
| R-03 | TC-003 | P1 | Equivalence partitioning | NEW → frontend/tests/e2e/landing.spec.ts |
| R-04 | TC-004 | P2 | Integration check | NEW → frontend/tests/e2e/landing.spec.ts |
| R-05 | TC-005 | P2 | UI disclosure | NEW → frontend/tests/e2e/landing.spec.ts |
| R-06 | TC-006 | P3 | UI verification | NEW → frontend/tests/e2e/landing.spec.ts |
| R-07 | TC-007 | P1 | Error guessing | NEW → frontend/tests/e2e/landing.spec.ts |
| R-08 | TC-008 | P1 | Boundary value analysis | NEW → frontend/tests/e2e/landing.spec.ts |
| R-09 | TC-009 | P2 | Security check | NEW → frontend/tests/e2e/landing.spec.ts |
| R-10 | TC-010 | P2 | Equivalence partitioning | NEW → frontend/tests/e2e/landing.spec.ts |
| R-11 | TC-011 | P2 | Boundary value analysis | NEW → frontend/tests/e2e/landing.spec.ts |
| R-12 | TC-012 | P2 | Navigation flow | NEW → frontend/tests/e2e/landing.spec.ts |

## 7. Gaps and Risks
- **Top Risks:**
  1. Multi-portal launch cards (LaunchPad grid) require active session authentication.  
     *Mitigation:* Public portal landing and verification flows are fully isolated in this plan; authenticated LaunchPad portal picker is planned as a separate suite.
  2. Google Identity Services CDN script latency in test runner.  
     *Mitigation:* Web-first assertion with generous timeout for GIS ready-state.
- **Gaps:** Backend verification API token exchange requires mock or seeded DB token for full end-to-end token consumption.
