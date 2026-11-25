/**
 * Test Plan: test-plans/persona-absence-crud.plan.md
 * Feature: Persona Portal (Leave & WFH Mutation Lifecycle)
 * Target environment: local (https://localhost:5174)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, AbsencePage } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('Persona Leave & WFH Lifecycle (CRUD & Mutations)', () => {
  test.describe.configure({ mode: 'serial', retries: 0 });
  test.use({ baseURL: 'https://localhost:5174' });

  let page: Page;
  let loginPage: LoginPage;
  let absencePage: AbsencePage;

  const timestamp = Date.now();
  let wfhReason = '';
  let leaveReason = '';
  let casualDate = '';
  let wfhDate = '';

  test.beforeAll(async ({ browser }, testInfo) => {
    const proj = testInfo.project.name.toLowerCase();
    const offset = proj.includes('firefox') ? 1 : proj.includes('webkit') ? 2 : 0;
    casualDate = `2026-12-0${1 + offset}`;
    wfhDate = `2026-12-1${5 + offset}`;
    wfhReason = `E2E-WFH-${proj}-${timestamp}`;
    leaveReason = `E2E-LEAVE-${proj}-${timestamp}`;

    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    absencePage = new AbsencePage(page);

    await loginPage.goto('https://localhost:5174/login');
    await loginPage.signInWithRetry(
      TEST_USER.email,
      TEST_USER.password,
      /https:\/\/localhost:5174\/(profile|employees)/,
    );
    await expect(page.locator('[aria-label^="User profile:"]')).toBeVisible({ timeout: 15_000 });
    await absencePage.goto();
  });

  test.afterAll(async () => {
    // Best-effort cleanup for any remaining pending requests
    try {
      if (page && !page.isClosed()) {
        const cancelButtons = page.locator('.absence-table button.absence-btn-cancel');
        const count = await cancelButtons.count();
        for (let i = 0; i < count; i++) {
          await cancelButtons
            .first()
            .click()
            .catch(() => {});
          await page.waitForTimeout(500);
        }
      }
    } catch {}
    await page?.close();
  });

  // PR-CRUD-01 [NEW] (R-01)
  test('PR-CRUD-01: should prevent submission when required fields are missing', async () => {
    await absencePage.goto();

    // Verify form is present and required fields are empty
    await expect(absencePage.requestForm).toBeVisible();

    // Attempt to submit with empty dates and reason
    await absencePage.submit();

    // Native required validation flags invalid input
    await expect(absencePage.reasonTextarea).toHaveValue('');
    const isReasonValid = await absencePage.reasonTextarea.evaluate((el: HTMLTextAreaElement) =>
      el.checkValidity(),
    );
    expect(isReasonValid).toBe(false);
  });

  // PR-CRUD-02 [NEW] (R-02)
  test('PR-CRUD-02: should enforce leave quota and disable submit on exceeded balance', async () => {
    await absencePage.goto();

    // Select Floating holidays
    await absencePage.selectType('CASUAL');

    // Request entire month of Dec 2026 (23 working days > 15 days allowance)
    await absencePage.setDate('Start date', '2026-12-01');
    await absencePage.setDate('End date', '2026-12-31');
    await absencePage.reasonTextarea.fill('Exceeded quota test request');

    // Exceeded warning banner should appear
    await expect(absencePage.exceededAlert).toBeVisible({ timeout: 5000 });
    await expect(absencePage.exceededAlert).toContainText(/exceeds your available/i);

    // Duration pill should reflect exceeded state
    await expect(absencePage.durationPill).toHaveClass(/exceeded/);

    // Submit button should be disabled
    await expect(absencePage.submitButton).toBeDisabled();
  });

  // PR-CRUD-03 [NEW] (R-03)
  test('PR-CRUD-03: should submit work from home request with unique reason', async () => {
    await absencePage.goto();

    // Switch to WFH
    await absencePage.fillForm({
      kind: 'WFH',
      startDate: wfhDate,
      endDate: wfhDate,
      reason: wfhReason,
    });

    // Warning should not appear for WFH
    await expect(absencePage.exceededAlert).toBeHidden();
    await expect(absencePage.durationPill).toHaveClass(/valid/);
    await expect(absencePage.submitButton).toBeEnabled();

    // Submit request
    await absencePage.submit();
  });

  // PR-CRUD-04 [NEW] (R-04)
  test('PR-CRUD-04: should display success banner and reset form upon submission', async () => {
    // Verify success banner appears
    await expect(absencePage.successNotice).toBeVisible({ timeout: 10_000 });
    await expect(absencePage.successNotice).toContainText('Request sent to your manager.');

    // Form inputs should reset
    await expect(absencePage.reasonTextarea).toHaveValue('');
  });

  // PR-CRUD-05 [NEW] (R-05)
  test('PR-CRUD-05: should render new request in table with PENDING badge and WFH kind chip', async () => {
    // Locate the newly created WFH request row
    const row = absencePage.getRequestRowByReason(wfhReason);
    await expect(row).toBeVisible({ timeout: 10_000 });

    // Verify kind chip
    await expect(row.locator('.absence-kind-chip')).toContainText('Work from home');

    // Verify status is Pending
    await expect(row.locator('.absence-status-cell')).toContainText(/pending/i);

    // Verify duration is 1 day
    await expect(row.locator('.absence-days-cell')).toContainText('1 day');
  });

  // PR-CRUD-06 [NEW] (R-06)
  test('PR-CRUD-06: should submit 1-day floating holiday within available allowance', async () => {
    // Fill 1 working day within available allowance
    await absencePage.fillForm({
      kind: 'CASUAL',
      startDate: casualDate,
      endDate: casualDate,
      reason: leaveReason,
    });

    await expect(absencePage.durationPill).toHaveClass(/valid/);
    await expect(absencePage.submitButton).toBeEnabled();

    // Submit request
    await absencePage.submit();

    // Verify dispatch success
    await expect(absencePage.successNotice).toBeVisible({ timeout: 10_000 });
    await expect(absencePage.successNotice).toContainText('Request sent to your manager.');
  });

  // PR-CRUD-07 [NEW] (R-07)
  test('PR-CRUD-07: should reflect pending reservation on floating holiday balance card', async () => {
    // Verify the casual leave balance card reflects at least 1 pending day (waiting for RTK query refetch)
    await expect(absencePage.casualCard).toBeVisible();
    const pendingPill = absencePage.casualCard.locator('.absence-stat-pill.pending');
    await expect(pendingPill).toContainText(/[1-9]\d* pending/, { timeout: 10_000 });
  });

  // PR-CRUD-08 [NEW] (R-08)
  test('PR-CRUD-08: should trigger cancellation on pending floating holiday request', async () => {
    // Find the row for the casual leave request
    const row = absencePage.getRequestRowByReason(leaveReason);
    await expect(row).toBeVisible();

    // Verify Cancel button is present on the pending request
    const cancelBtn = row.locator('button.absence-btn-cancel');
    await expect(cancelBtn).toBeVisible();

    // Click Cancel request
    await cancelBtn.click();
  });

  // PR-CRUD-09 [NEW] (R-09)
  test('PR-CRUD-09: should verify cancellation notice and updated CANCELLED status badge', async () => {
    // Verify cancellation notice
    await expect(absencePage.successNotice).toBeVisible({ timeout: 10_000 });
    await expect(absencePage.successNotice).toContainText('Request cancelled.');

    // Verify row status transitioned to Cancelled
    const row = absencePage.getRequestRowByReason(leaveReason);
    await expect(row).toBeVisible();
    await expect(row.locator('.absence-status-cell')).toContainText(/cancelled/i);

    // Also cancel the pending WFH request so no active pending requests remain
    const wfhRow = absencePage.getRequestRowByReason(wfhReason);
    const wfhCancelBtn = wfhRow.locator('button.absence-btn-cancel');
    if (await wfhCancelBtn.isVisible()) {
      await wfhCancelBtn.click();
      await expect(wfhRow.locator('.absence-status-cell')).toContainText(/cancelled/i);
    }
  });

  // PR-CRUD-10 [NEW] (R-10)
  test('PR-CRUD-10: should verify restored available balance on floating holiday card', async () => {
    // Verify balance card stat is rendered with available days
    await expect(absencePage.casualCard).toBeVisible();
    const pendingPill = absencePage.casualCard.locator('.absence-stat-pill.pending');
    await expect(pendingPill).toBeVisible();

    const availableStat = absencePage.casualCard.locator('.absence-card-stat');
    await expect(availableStat).toContainText(/days available/i);
    await expect(availableStat).toContainText(/(?:1[2-5]) days available/, { timeout: 10_000 });
  });
});
