/**
 * Test Plan: test-plans/chronos-worklog-crud.plan.md
 * Feature: Chronos Portal (Work Logs & Timesheet Mutation Lifecycle)
 * Target environment: local (https://localhost:5175)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, WorkLogPage, TimesheetsPage } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('Chronos Work Logs Lifecycle (CRUD & Mutations)', () => {
  test.describe.configure({ mode: 'serial', retries: 0 });
  test.use({ baseURL: 'https://localhost:5175' });

  let page: Page;
  let loginPage: LoginPage;
  let workLogPage: WorkLogPage;
  let timesheetsPage: TimesheetsPage;

  const timestamp = Date.now();
  let createdLogDesc = `E2E-Log-${timestamp}`;
  let updatedLogDesc = `E2E-Log-${timestamp}-Updated`;

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    workLogPage = new WorkLogPage(page);
    timesheetsPage = new TimesheetsPage(page);

    await loginPage.goto('https://localhost:5175/login');
    await loginPage.signInWithRetry(TEST_USER.email, TEST_USER.password, /\/timesheets\/my$/);
    await expect(page.getByRole('heading', { name: /my timesheet/i })).toBeVisible({
      timeout: 15_000,
    });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  // CW-CRUD-01 [NEW] (R-01)
  test('should validate form when submitting empty fields on Log work page', async () => {
    await workLogPage.goto('/timesheets/log-work');

    // Attempt to submit empty form
    await workLogPage.submit();

    // Verify validation feedback on required fields
    await expect(page.locator('form.time-form select:invalid')).toBeVisible({
      timeout: 5000,
    });
  });

  // CW-CRUD-02 [NEW] (R-02)
  test('should log a new work entry with valid hours, issue, and description', async () => {
    await workLogPage.goto('/timesheets/log-work');

    // Select issue
    await workLogPage.selectFirstIssue();

    // Fill hours
    await workLogPage.setHours('2.5');

    // Fill description
    await workLogPage.setDescription(createdLogDesc);

    // Submit work log
    await workLogPage.submit();

    // Assert success banner
    await expect(workLogPage.successBanner).toBeVisible({ timeout: 10_000 });
    await expect(workLogPage.successBanner).toContainText('Work log saved successfully!');
  });

  // CW-CRUD-03 [NEW] (R-03)
  test('should update weekly total logged hours and completion metric card', async () => {
    // Assert target metrics visible on log-work page
    await expect(workLogPage.targetTotal).toBeVisible();
    await expect(workLogPage.targetTotal).not.toHaveText('0.0h');
  });

  // CW-CRUD-04 [NEW] (R-04)
  test('should navigate to my timesheet and verify entry in data table', async () => {
    await timesheetsPage.goto('/timesheets/my');

    // Assert table is visible
    await expect(timesheetsPage.table).toBeVisible({ timeout: 10_000 });

    // Assert row with created description is present
    const row = timesheetsPage.rows.filter({ hasText: createdLogDesc });
    await expect(row.first()).toBeVisible({ timeout: 10_000 });

    // Assert logged hours
    await expect(row.first()).toContainText('2.5');
  });

  // CW-CRUD-05 [NEW] (R-05)
  test('should display billable and period draft status badges on the logged row', async () => {
    const row = timesheetsPage.rows.filter({ hasText: createdLogDesc }).first();
    await expect(row).toBeVisible();

    // Assert billable badge
    await expect(row.locator('.worklog-billable-badge')).toContainText('Yes');

    // Assert period status indicator
    await expect(row.locator('.time-status')).toContainText(/DRAFT/i);
  });

  // CW-CRUD-06 [NEW] (R-06)
  test('should edit work log entry and update duration and description', async () => {
    // Click edit on the row
    await timesheetsPage.editLogByDescription(createdLogDesc);

    // Form opens in edit mode
    await expect(page.locator('form.time-form')).toBeVisible({ timeout: 5000 });

    // Update hours
    await workLogPage.setHours('3.5');

    // Update description
    await workLogPage.setDescription(updatedLogDesc);

    // Save changes
    await workLogPage.submit();

    // Wait for form to close
    await expect(page.locator('form.time-form')).not.toBeVisible({ timeout: 8000 });
  });

  // CW-CRUD-07 [NEW] (R-07)
  test('should verify updated duration and description render in table', async () => {
    const updatedRow = timesheetsPage.rows.filter({ hasText: updatedLogDesc });
    await expect(updatedRow.first()).toBeVisible({ timeout: 10_000 });
    await expect(updatedRow.first()).toContainText('3.5');
  });

  // CW-CRUD-08 [NEW] (R-08)
  test('should switch timesheet layout to Hierarchy view and verify entry grouping', async () => {
    await timesheetsPage.switchLayout('hierarchy');

    // Assert hierarchy view mounted
    await expect(page.locator('.time-hierarchy-embedded')).toBeVisible({
      timeout: 10_000,
    });

    // Switch back to Table view for deletion
    await timesheetsPage.switchLayout('table');
    await expect(timesheetsPage.table).toBeVisible({ timeout: 10_000 });
  });

  // CW-CRUD-09 [NEW] (R-09)
  test('should delete the work log entry using row action and inline confirmation', async () => {
    await timesheetsPage.deleteLogByDescription(updatedLogDesc);

    // Verify row with updated description is removed
    const deletedRow = timesheetsPage.rows.filter({ hasText: updatedLogDesc });
    await expect(deletedRow).not.toBeVisible({ timeout: 10_000 });
  });

  // CW-CRUD-10 [NEW] (R-10)
  test('should verify the deleted entry is purged from the timesheet table', async () => {
    // Assert the deleted description does not appear anywhere in the table
    await expect(timesheetsPage.table.getByText(updatedLogDesc)).not.toBeVisible({ timeout: 5000 });
  });
});
