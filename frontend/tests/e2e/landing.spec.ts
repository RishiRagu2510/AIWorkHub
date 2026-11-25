/**
 * Test Plan: test-plans/landing-portal.plan.md
 * Feature: Landing Portal (LaunchPad & Email Verification)
 * Target environment: local (https://localhost:5171)
 */
import { test, expect } from '@playwright/test';

test.describe('Landing Portal (LaunchPad)', () => {
  test.use({ baseURL: 'https://localhost:5171' });

  // TC-001 [NEW] (R-01)
  test('should display LaunchPad login page branding and tagline', async ({ page }) => {
    // 1. Navigate to /login
    await page.goto('/login');

    // 2. Verify link "LaunchPad home" is visible with href="/login"
    const homeLink = page.getByRole('link', { name: 'LaunchPad home' });
    await expect(homeLink).toBeVisible();
    await expect(homeLink).toHaveAttribute('href', '/login');

    // 3. Verify heading "Sign in to your organization" is visible
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();

    // 4. Verify intro text contains "Sign in once, then open the portals your company has"
    await expect(
      page.getByText(/sign in once, then open the portals your company has/i),
    ).toBeVisible();
  });

  // TC-002 [NEW] (R-02)
  test('should redirect unauthenticated user from root URL to login page', async ({ page }) => {
    // 1. Navigate to /
    await page.goto('/');

    // 2. Verify redirect to /login
    await expect(page).toHaveURL(/\/login$/);
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();
  });

  // TC-003 [NEW] (R-03)
  test('should display email and password inputs with correct placeholders', async ({ page }) => {
    await page.goto('/login');

    // 1. Verify textbox "Company email"
    const emailInput = page.getByRole('textbox', { name: /company email/i });
    await expect(emailInput).toBeVisible();
    await expect(emailInput).toBeEnabled();
    await expect(emailInput).toHaveAttribute('placeholder', 'you@company.com');
    await expect(emailInput).toHaveAttribute('type', 'email');

    // 2. Verify password textbox
    const passwordInput = page.getByLabel(/^password$/i);
    await expect(passwordInput).toBeVisible();
    await expect(passwordInput).toBeEnabled();
    await expect(passwordInput).toHaveAttribute('type', 'password');

    // 3. Verify submit button
    const submitBtn = page.getByRole('button', { name: /sign in with email/i });
    await expect(submitBtn).toBeVisible();
    await expect(submitBtn).toBeEnabled();
  });

  // TC-004 [NEW] (R-04)
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

  // TC-005 [NEW] (R-05)
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

  // TC-006 [NEW] (R-06)
  test('should display footer branding with LaunchPad and AIWorkHub', async ({ page }) => {
    await page.goto('/login');

    // 1. Locate footer
    const footer = page.locator('footer.sso-footer');
    await expect(footer).toBeVisible();

    // 2. Verify branding text inside footer
    await expect(footer).toContainText('LaunchPad');
    await expect(footer).toContainText('AIWorkHub');
  });

  // TC-007 [NEW] (R-07)
  test('should display alert on verify-email when accessed without token', async ({ page }) => {
    // 1. Navigate to /verify-email directly without hash
    await page.goto('/verify-email');

    // 2. Verify heading
    await expect(
      page.getByRole('heading', { name: /verify email and set password/i }),
    ).toBeVisible();

    // 3. Verify alert with guidance
    const alert = page.getByRole('alert');
    await expect(alert).toBeVisible();
    await expect(alert).toContainText(/open the verification link from your email/i);

    // 4. Verify password inputs are not present
    await expect(page.getByLabel(/new password/i)).not.toBeVisible();
  });

  // TC-008 [NEW] (R-08)
  test('should display password setup form on verify-email when token hash is provided', async ({
    page,
  }) => {
    // 1. Navigate with hash token
    await page.goto('/verify-email#token=valid-test-token-12345');

    // 2. Verify New Password input
    const newPassword = page.getByLabel(/^new password$/i);
    await expect(newPassword).toBeVisible();
    await expect(newPassword).toHaveAttribute('type', 'password');
    await expect(newPassword).toHaveAttribute('minlength', '12');
    await expect(newPassword).toHaveAttribute('maxlength', '128');

    // 3. Verify Confirm Password input
    const confirmPassword = page.getByLabel(/^confirm password$/i);
    await expect(confirmPassword).toBeVisible();
    await expect(confirmPassword).toHaveAttribute('type', 'password');
    await expect(confirmPassword).toHaveAttribute('minlength', '12');
    await expect(confirmPassword).toHaveAttribute('maxlength', '128');

    // 4. Verify submit button
    const submitBtn = page.getByRole('button', { name: /verify and set password/i });
    await expect(submitBtn).toBeVisible();
    await expect(submitBtn).toBeEnabled();
  });

  // TC-009 [NEW] (R-09)
  test('should sanitize token hash from address bar after mount', async ({ page }) => {
    // 1. Navigate with token hash
    await page.goto('/verify-email#token=sensitive-secret-token-xyz');

    // 2. Wait for page load
    await expect(
      page.getByRole('heading', { name: /verify email and set password/i }),
    ).toBeVisible();

    // 3. Verify hash is sanitized from window location
    const hash = await page.evaluate(() => window.location.hash);
    expect(hash).toBe('');
    await expect(page).toHaveURL(/https:\/\/localhost:5171\/verify-email$/);
  });

  // TC-010 [NEW] (R-10)
  test('should display error when password and confirm password do not match', async ({ page }) => {
    await page.goto('/verify-email#token=test-mismatch-token');

    // 1. Fill mismatched passwords
    await page.getByLabel(/^new password$/i).fill('SecurePass1234!');
    await page.getByLabel(/^confirm password$/i).fill('DifferentPass5678!');

    // 2. Submit form
    await page.getByRole('button', { name: /verify and set password/i }).click();

    // 3. Verify error alert
    const errorAlert = page.getByRole('alert');
    await expect(errorAlert).toBeVisible();
    await expect(errorAlert).toContainText(/passwords do not match/i);
  });

  // TC-011 [NEW] (R-11)
  test('should display error when password contains only whitespace', async ({ page }) => {
    await page.goto('/verify-email#token=test-whitespace-token');

    // 1. Fill whitespace-only passwords (12 spaces)
    await page.getByLabel(/^new password$/i).fill('            ');
    await page.getByLabel(/^confirm password$/i).fill('            ');

    // 2. Submit form
    await page.getByRole('button', { name: /verify and set password/i }).click();

    // 3. Verify error alert
    const errorAlert = page.getByRole('alert');
    await expect(errorAlert).toBeVisible();
    await expect(errorAlert).toContainText(/choose a password containing more than spaces/i);
  });

  // TC-012 [NEW] (R-12)
  test('should navigate back to login page from Return to sign in link', async ({ page }) => {
    await page.goto('/verify-email');

    // 1. Verify link exists
    const returnLink = page.getByRole('link', { name: /return to sign in/i });
    await expect(returnLink).toBeVisible();

    // 2. Click link and verify navigation
    await returnLink.click();
    await expect(page).toHaveURL(/https:\/\/localhost:5171\/login$/);
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();
  });
});
