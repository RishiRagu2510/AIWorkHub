# Agent Memory
Root causes and preventive rules learned while working on this project.

## Preventive Rules
- When asserting user profile popover details (e.g. user name or email) that also appear in page description lists (`<dd>`), scope assertions to `page.getByRole('banner')` (or `page.locator('.profile-dropdown')`) to prevent strict mode locator collisions (source: 2026-11-18, pw-healer).
- When asserting numeric counter badges rendered across split sibling spans (e.g. `<span class="count-number">2</span><span class="count-label">indexed docs</span>`), do not use a single combined regex with `getByText`; instead, assert the label span (`getByText('indexed docs')`) or the parent heading (`getByRole('heading', { name: /Indexed Documents/i })`) to prevent locator timeouts (source: 2026-11-18, pw-healer).
- In EF Core multi-context microservices sharing common entity models (e.g. `PersonaDbContext` and `IdentityTenantDbContext`), do not blindly run `ApplyConfigurationsFromAssembly` across external assemblies; instead, register cross-domain entity configurations explicitly to prevent duplicate table mapping collisions (source: 2026-11-18, pw-healer).
- When asserting pagination summary text (e.g. in employee directories), use regex `/Showing \d+ of \d+ employee/i` to handle singular vs plural differences (`employee` vs `employees`) dynamically depending on filter results (source: 2026-11-18, pw-generator).
- When targeting column headers or text cells that match substrings in action labels (e.g. `columnheader` "Employee" vs checkbox "Select all employees"), specify `{ exact: true }` to prevent strict mode locator collisions (source: 2026-11-18, pw-generator).
- When multiple test suites concurrently submit invalid credentials across portals against IdentityTenant, backend IP rate limiting can trigger ("Too many attempts. Please wait a minute before trying again."). Test assertions must match either standard error or rate-limited error with regex (`/Email or password is incorrect|Too many attempts/`) and use portal-specific test email addresses (source: 2026-11-18, pw-healer).
- On `/verify-email`, `window.location.hash` is read only upon initial component mount and cleaned immediately via `history.replaceState`; tests verifying token-present flows must navigate directly to the URL with the hash on initial page load (source: 2026-11-18, pw-planner).
- When asserting in-flight/busy button state transitions where the button text changes dynamically (e.g. "Sign in with email" -> "Please wait…"), locate the submit button via a stable locator (e.g. `locator('button[type="submit"]')`) rather than `getByRole('button', { name })` since the accessible name changes upon re-render (source: 2026-11-17, pw-generator).
- Playwright's `toBeDisabled()` locator assertion expects form controls; for disabled container fieldsets, assert `toHaveAttribute('disabled', '')` on the `<fieldset>` and `toBeDisabled()` on child form controls (source: 2026-11-17, pw-generator).
- In login tests, wait for Google Identity Services iframe or button container to resolve loading state before asserting Google SSO presence (source: 2026-11-17, pw-planner).
- Frontend dev server must run with host network access to connect to the backend API Gateway proxy on port 5204 (source: 2026-11-17, pw-project-discovery).
- Ensure backend services (ApiGateway: 5204, IdentityTenant: 5233) are listening before testing auth endpoints (source: 2026-11-17, pw-project-discovery).
- GridHub redirects unauthenticated root navigation (/) to /login (source: 2026-11-17, pw-project-discovery).
- Vite with `server.https` and `xfwd: true` forwards real originating HTTPS scheme dynamically without hardcoding headers (source: 2026-11-17, pw-project-discovery).
- When configuring trusted proxies in ASP.NET Core, define `ReverseProxy:KnownProxies` with loopback IPs (`127.0.0.1`, `::1`) in `appsettings.Development.json` to preserve production settings (source: 2026-11-17, pw-project-discovery).

## What Worked
- 42/42 Playwright E2E tests passed across Chromium, Firefox, and WebKit for auth-sso.plan.md (14 tests per browser).
- Playwright MCP successfully navigated to https://localhost:5173/login, expanded the help details disclosure, and triggered the live credential failure alert for verification.
- Starting Vite dev server via `npm run dev:gridhub` with proxy capability enabled communication between frontend and ApiGateway.
- Serving Vite over HTTPS (`https://localhost:5173`) with `xfwd: true` dynamically forwarded `X-Forwarded-Proto: https` to ApiGateway.
- Verified `/api/auth/google/configuration` returned 200 OK over HTTPS, and browser accepted `SameSite=None; Secure` cookies.
- Google SSO button in iframe successfully loaded and rendered on login page over HTTPS.
- 15/15 Playwright E2E tests passed across Chromium, Firefox, and WebKit with `ignoreHTTPSErrors: true`.

## What Failed
- Running Vite dev server inside an isolated network sandbox caused `AggregateError [EPERM]` on upstream proxy calls, surfacing as 502 Bad Gateway in the browser.
- Navigating to `/api/auth/google/configuration` over HTTP without forwarded proto header failed with 500 Internal Server Error due to antiforgery cookie SSL requirement.
