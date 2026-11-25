/**
 * Test Plan: test-plans/auth-sso.plan.md
 * Feature: Authentication & SSO (GridHub)
 * Target environment: local (https://localhost:5173)
 */
import { test, expect } from '@playwright/test';

test.describe('Authentication & Landing (GridHub)', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
  });

  // TC-001 [REUSE] (R-01)
  test('should display login page and brand elements', async ({ page }) => {
    // 1. Open the login page (/login)
    // 2. Verify the brand header and main heading
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();
    const brand = page.locator('header.sso-header .brand');
    await expect(brand).toBeVisible();
    await expect(brand).toContainText(/GridHub|AIWorkHub|Portal/i);
    await expect(page.getByRole('link', { name: 'GridHub home' })).toHaveAttribute(
      'href',
      '/login',
    );
  });

  // TC-002 [UPDATE] (R-02)
  test('should display password sign-in form with email and password inputs', async ({ page }) => {
    // 1. Open the login page (/login)
    // 2. Verify the email textbox exists with placeholder "you@company.com"
    const emailInput = page.getByRole('textbox', { name: /company email/i });
    await expect(emailInput).toBeVisible();
    await expect(emailInput).toBeEnabled();
    await expect(emailInput).toHaveAttribute('placeholder', 'you@company.com');

    // 3. Verify the password textbox exists and has type "password"
    const passwordInput = page.getByLabel(/^password$/i);
    await expect(passwordInput).toBeVisible();
    await expect(passwordInput).toBeEnabled();
    await expect(passwordInput).toHaveAttribute('type', 'password');

    // 4. Verify the submit button with accessible name "Sign in with email" is visible
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });
    await expect(submitBtn).toBeVisible();
    await expect(submitBtn).toBeEnabled();
  });

  // TC-003 [UPDATE] (R-03)
  test('should display Google SSO sign-in option with rendered GIS button', async ({ page }) => {
    // 1. Open the login page (/login)
    // 2. Wait for Google sign-in container to finish loading
    await expect(page.getByRole('status', { name: /loading google sign-in/i })).not.toBeVisible({
      timeout: 15_000,
    });

    // 3. Verify Google sign-in button or iframe is visible
    const googleBtnContainer = page.locator('.google-identity-signin');
    await expect(googleBtnContainer).toBeVisible();
    await expect(page.getByText(/continue with Google/i).first()).toBeVisible({ timeout: 15_000 });

    // 4. Verify no error alert banner is displayed
    await expect(page.locator('.google-identity-signin [role="alert"]')).not.toBeVisible();
  });

  // TC-004 [REUSE] (R-04)
  test('should support expanding trouble signing in help section', async ({ page }) => {
    // 1. Open the login page (/login)
    // 2. Click on the "Trouble signing in?" summary
    const details = page.locator('.sso-help details');
    await expect(details).toBeVisible();
    await details.locator('summary').click();

    // 3. Verify help text is visible
    await expect(
      page.getByText(/Use the Google account provided by your organization/i),
    ).toBeVisible();
  });

  // TC-005 [REUSE] (R-05)
  test('should display footer with portal name', async ({ page }) => {
    // 1. Open the login page (/login)
    // 2. Verify the footer is visible and contains "GridHub"
    const footer = page.locator('footer.sso-footer');
    await expect(footer).toBeVisible();
    await expect(footer).toContainText(/GridHub/i);
    await expect(footer).toContainText(/Enterprise work management|AIWorkHub/i);
  });

  // TC-006 [NEW] (R-07)
  test('should prevent submission and focus email field when submitting empty form', async ({
    page,
  }) => {
    let requestDispatched = false;
    await page.route('**/api/auth/password/login', (route) => {
      requestDispatched = true;
      return route.abort();
    });

    // 1. Open the login page (/login)
    // 2. Leave email and password inputs empty
    const emailInput = page.getByRole('textbox', { name: /company email/i });
    const passwordInput = page.getByLabel(/^password$/i);
    await expect(emailInput).toHaveValue('');
    await expect(passwordInput).toHaveValue('');

    // 3. Click "Sign in with email" button
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });
    await submitBtn.click();

    // Expected: submission blocked, email focused, no network request
    expect(requestDispatched).toBe(false);
    await expect(emailInput).toBeFocused();
  });

  // TC-007 [NEW] (R-06)
  test('should display alert message when submitting invalid email or password', async ({
    page,
  }) => {
    // 1. Open the login page (/login)
    const emailInput = page.getByRole('textbox', { name: /company email/i });
    const passwordInput = page.getByLabel(/^password$/i);
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });

    // 2. Enter "invalid.user@example.com" in Company email textbox
    await emailInput.fill('invalid.user@example.com');

    // 3. Enter "InvalidPassword123!" in Password textbox
    await passwordInput.fill('InvalidPassword123!');

    // 4. Click "Sign in with email" button
    await submitBtn.click();

    // Expected: alert with role="alert", expected error text, inputs retain values
    const alert = page.getByRole('alert');
    await expect(alert).toBeVisible();
    await expect(alert).toHaveText(/Email or password is incorrect|Too many attempts/);
    await expect(emailInput).toHaveValue('invalid.user@example.com');
    await expect(passwordInput).toHaveValue('InvalidPassword123!');
  });

  // TC-008 [NEW] (R-07)
  test("should reject invalid email syntax without '@' symbol", async ({ page }) => {
    let requestDispatched = false;
    await page.route('**/api/auth/password/login', (route) => {
      requestDispatched = true;
      return route.abort();
    });

    // 1. Open the login page (/login)
    const emailInput = page.getByRole('textbox', { name: /company email/i });
    const passwordInput = page.getByLabel(/^password$/i);
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });

    // 2. Enter "notanemail" in Company email textbox
    await emailInput.fill('notanemail');

    // 3. Enter "ValidPassword123!" in Password textbox
    await passwordInput.fill('ValidPassword123!');

    // 4. Click "Sign in with email" button
    await submitBtn.click();

    // Expected: HTML5 validation rejects, :invalid state, no request dispatched
    expect(requestDispatched).toBe(false);
    await expect(emailInput).toHaveJSProperty('validity.valid', false);
    await expect(emailInput).toHaveJSProperty('validity.typeMismatch', true);
  });

  // TC-009 [NEW] (R-08)
  test('should redirect unauthenticated user from root URL to login page', async ({ page }) => {
    // 1. Navigate directly to /
    await page.goto('/');

    // 2. Wait for route resolution
    // Expected: redirect to /login, heading visible
    await expect(page).toHaveURL(/\/login$/);
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();
  });

  // TC-010 [NEW] (R-04)
  test('should support collapsing help section when clicked again', async ({ page }) => {
    // 1. Open the login page (/login)
    const details = page.locator('.sso-help details');
    const summary = details.locator('summary');
    const helpText = page.getByText(/Use the Google account provided by your organization/i);

    // 2. Click "Trouble signing in?" summary to expand
    await summary.click();

    // 3. Verify help text is visible
    await expect(details).toHaveAttribute('open', '');
    await expect(helpText).toBeVisible();

    // 4. Click "Trouble signing in?" summary again to collapse
    await summary.click();

    // Expected: details loses open attribute, help text not visible
    await expect(details).not.toHaveAttribute('open', '');
    await expect(helpText).not.toBeVisible();
  });

  // TC-011 [NEW] (R-02)
  test('should mask entered password characters', async ({ page }) => {
    // 1. Open the login page (/login)
    const passwordInput = page.getByLabel(/^password$/i);

    // 2. Enter "SecretPassword123!" in Password textbox
    await passwordInput.fill('SecretPassword123!');

    // 3. Inspect the password input element type attribute
    // Expected: type="password", value preserved
    await expect(passwordInput).toHaveAttribute('type', 'password');
    await expect(passwordInput).toHaveValue('SecretPassword123!');
  });

  // TC-012 [NEW] (R-02, BR-02)
  test('should disable form fieldset and show busy text during email sign-in submission', async ({
    page,
  }) => {
    let unblockRoute: () => void = () => {};
    const routePromise = new Promise<void>((resolve) => {
      unblockRoute = resolve;
    });

    // 1. Intercept /api/auth/password/login with a delayed response
    await page.route('**/api/auth/password/login', async (route) => {
      await routePromise;
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Invalid credentials' }),
      });
    });

    const emailInput = page.getByRole('textbox', { name: /company email/i });
    const passwordInput = page.getByLabel(/^password$/i);
    const submitBtn = page.locator('.password-signin button[type="submit"]');
    const fieldset = page.locator('.password-signin fieldset');

    // 2. Fill email and password fields
    await emailInput.fill('test@example.com');
    await passwordInput.fill('Password123!');

    // 3. Click "Sign in with email"
    await submitBtn.click();

    // 4. Inspect fieldset and submit button while request is in flight
    // Expected: fieldset disabled, button shows "Please wait…"
    await expect(fieldset).toHaveAttribute('disabled', '');
    await expect(submitBtn).toBeDisabled();
    await expect(submitBtn).toHaveText('Please wait…');

    // Complete request and verify recovery
    unblockRoute();
    await expect(fieldset).not.toHaveAttribute('disabled');
    await expect(submitBtn).toBeEnabled();
    await expect(submitBtn).toHaveText('Sign in with email');
  });

  // TC-013 [NEW] (R-03)
  test('should display error notification if Google SSO configuration endpoint fails', async ({
    page,
  }) => {
    // 1. Intercept /api/auth/google/configuration and return HTTP 500 error
    await page.route('**/api/auth/google/configuration', (route) =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Configuration unavailable' }),
      }),
    );

    // 2. Navigate to /login
    await page.goto('/login');

    // 3. Wait for Google sign-in failure handling
    // Expected: error alert in Google sign-in container
    const googleAlert = page.locator('.google-identity-signin [role="alert"]');
    await expect(googleAlert).toBeVisible();

    // Expected: Email/password sign-in remains interactive
    const emailInput = page.getByRole('textbox', { name: /company email/i });
    const passwordInput = page.getByLabel(/^password$/i);
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });

    await expect(emailInput).toBeEnabled();
    await expect(passwordInput).toBeEnabled();
    await expect(submitBtn).toBeEnabled();
  });

  // TC-014 [NEW] (R-09)
  test('should display error notice when landing with login error query parameter', async ({
    page,
  }) => {
    // 1. Navigate to /login?error=account_locked
    await page.goto('/login?error=account_locked');

    // 2. Observe displayed notices
    // Expected: notice banner/toast is displayed
    const noticeAlert = page.locator('.notice, [role="alert"]');
    await expect(noticeAlert.first()).toBeVisible();

    // 3. Inspect current URL
    // Expected: ?error= stripped from URL without full page reload
    await expect(page).toHaveURL(/\/login$/);
  });
});
