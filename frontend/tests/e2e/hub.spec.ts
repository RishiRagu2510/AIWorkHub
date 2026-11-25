/**
 * Test Plan: test-plans/hub-portal.plan.md
 * Feature: Hub Portal (AIWorkHub Central Portal & Knowledge)
 * Target environment: local (https://localhost:5172)
 */
import { test, expect } from '@playwright/test';

test.describe('Hub Portal (AIWorkHub)', () => {
  test.use({ baseURL: 'https://localhost:5172' });

  // TC-001 [NEW] (R-01)
  test('should display AIWorkHub login page branding and copy', async ({ page }) => {
    // 1. Navigate to https://localhost:5172/login
    await page.goto('/login');

    // 2. Verify link "AIWorkHub home" is visible with href="/login"
    const homeLink = page.getByRole('link', { name: 'AIWorkHub home' });
    await expect(homeLink).toBeVisible();
    await expect(homeLink).toHaveAttribute('href', '/login');

    // 3. Verify heading "Sign in to your organization" is visible
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();

    // 4. Verify intro text contains "Your unified launchpad for all workspaces.Connected in one place."
    await expect(page.getByText(/your unified launchpad for all workspaces/i)).toBeVisible();
    await expect(page.getByText(/connected in one place/i)).toBeVisible();
  });

  // TC-002 [NEW] (R-02)
  test('should redirect unauthenticated user from root URL to login page', async ({ page }) => {
    // 1. Navigate to https://localhost:5172/
    await page.goto('/');

    // 2. Verify redirect to /login
    await expect(page).toHaveURL(/\/login$/);
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();
  });

  // TC-003 [NEW] (R-03)
  test('should redirect unauthenticated user from /home to login page', async ({ page }) => {
    // 1. Navigate to /home
    await page.goto('/home');

    // 2. Verify redirect to /login
    await expect(page).toHaveURL(/\/login$/);
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();
  });

  // TC-004 [NEW] (R-04)
  test('should redirect unauthenticated user from /knowledge to login page', async ({ page }) => {
    // 1. Navigate to /knowledge
    await page.goto('/knowledge');

    // 2. Verify redirect to /login
    await expect(page).toHaveURL(/\/login$/);
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();
  });

  // TC-005 [NEW] (R-05)
  test('should display email and password inputs with correct placeholders', async ({ page }) => {
    await page.goto('/login');

    // 1. Verify textbox "Company email" has placeholder "you@company.com" and attribute type="email"
    const emailInput = page.getByRole('textbox', { name: /company email/i });
    await expect(emailInput).toBeVisible();
    await expect(emailInput).toBeEnabled();
    await expect(emailInput).toHaveAttribute('placeholder', 'you@company.com');
    await expect(emailInput).toHaveAttribute('type', 'email');

    // 2. Verify password textbox has attribute type="password"
    const passwordInput = page.getByLabel(/^password$/i);
    await expect(passwordInput).toBeVisible();
    await expect(passwordInput).toBeEnabled();
    await expect(passwordInput).toHaveAttribute('type', 'password');

    // 3. Verify button "Sign in with email" is visible and enabled
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });
    await expect(submitBtn).toBeVisible();
    await expect(submitBtn).toBeEnabled();
  });

  // TC-006 [NEW] (R-06)
  test('should render Google SSO button without error notice', async ({ page }) => {
    await page.goto('/login');

    // 1. Wait for Google Identity Services initialization
    await expect(page.getByRole('status', { name: /loading google sign-in/i })).not.toBeVisible({
      timeout: 15_000,
    });

    // 2. Verify Google SSO button is rendered
    await expect(page.getByText(/continue with Google/i).first()).toBeVisible({ timeout: 15_000 });

    // 3. Verify no alert banner is displayed
    await expect(page.locator('.google-identity-signin [role="alert"]')).not.toBeVisible();
  });

  // TC-007 [NEW] (R-07)
  test('should expand help section when clicking Trouble signing in', async ({ page }) => {
    await page.goto('/login');

    // 1. Verify disclosure is initially closed
    const details = page.locator('.sso-help details');
    await expect(details).toBeVisible();
    await expect(details).not.toHaveAttribute('open', '');

    // 2. Click summary
    await details.locator('summary').click();

    // 3. Verify expanded guidance is visible
    await expect(details).toHaveAttribute('open', '');
    await expect(
      page.getByText(/Use the Google account provided by your organization/i),
    ).toBeVisible();
  });

  // TC-008 [NEW] (R-08)
  test('should display footer branding with AIWorkHub and AIWorkHub Central Portal', async ({
    page,
  }) => {
    await page.goto('/login');

    // 1. Locate footer with class .sso-footer
    const footer = page.locator('footer.sso-footer');
    await expect(footer).toBeVisible();

    // 2. Verify branding text inside footer
    await expect(footer).toContainText('AIWorkHub');
    await expect(footer).toContainText('AIWorkHub Central Portal');
  });

  // TC-009 [NEW] (R-09)
  test('should display alert message when submitting invalid email or password', async ({
    page,
  }) => {
    await page.goto('/login');

    const emailInput = page.getByRole('textbox', { name: /company email/i });
    const passwordInput = page.getByLabel(/^password$/i);
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });

    // 1. Enter invalid email and password
    const testEmail = 'invalid.hub@example.com';
    await emailInput.fill(testEmail);
    await passwordInput.fill('InvalidPassword123!');

    // 2. Click "Sign in with email" button
    await submitBtn.click();

    // 3. Verify alert
    const alert = page.getByRole('alert');
    await expect(alert).toBeVisible();
    await expect(alert).toHaveText(/Email or password is incorrect|Too many attempts/);
    await expect(emailInput).toHaveValue(testEmail);
    await expect(passwordInput).toHaveValue('InvalidPassword123!');
  });

  // TC-010 [NEW] (R-10)
  test('should prevent submission and focus email field when submitting empty form', async ({
    page,
  }) => {
    let requestDispatched = false;
    await page.route('**/api/auth/password/login', (route) => {
      requestDispatched = true;
      return route.abort();
    });

    await page.goto('/login');

    // 1. Leave email and password inputs empty
    const emailInput = page.getByRole('textbox', { name: /company email/i });
    const passwordInput = page.getByLabel(/^password$/i);
    await expect(emailInput).toHaveValue('');
    await expect(passwordInput).toHaveValue('');

    // 2. Click "Sign in with email" button
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });
    await submitBtn.click();

    // 3. Expected: submission blocked, email focused, no network request
    expect(requestDispatched).toBe(false);
    await expect(emailInput).toBeFocused();
  });

  // TC-011 [NEW] (R-11)
  test('should reject invalid email syntax without @ symbol', async ({ page }) => {
    let requestDispatched = false;
    await page.route('**/api/auth/password/login', (route) => {
      requestDispatched = true;
      return route.abort();
    });

    await page.goto('/login');

    const emailInput = page.getByRole('textbox', { name: /company email/i });
    const passwordInput = page.getByLabel(/^password$/i);
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });

    // 1. Enter text without '@' symbol
    await emailInput.fill('invalid-email-no-at');
    await passwordInput.fill('ValidPassword123!');

    // 2. Click submit button
    await submitBtn.click();

    // 3. Form validation should block dispatch
    expect(requestDispatched).toBe(false);
    await expect(emailInput).toBeFocused();
  });

  // TC-012 [NEW] (R-12)
  test('should mask entered password characters', async ({ page }) => {
    await page.goto('/login');

    const passwordInput = page.getByLabel(/^password$/i);
    await expect(passwordInput).toHaveAttribute('type', 'password');

    await passwordInput.fill('SecretPassword');
    await expect(passwordInput).toHaveValue('SecretPassword');
  });
});
