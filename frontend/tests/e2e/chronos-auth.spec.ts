/**
 * Test Plan: test-plans/chronos-authenticated.plan.md
 * Feature: Chronos Portal (Authenticated Timesheets, Work Logs & Period Reviews)
 * Target environment: local (https://localhost:5175)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, HeaderComponent } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('Chronos Portal (Authenticated)', () => {
  test.describe.configure({ mode: 'serial', retries: 2 });
  test.use({ baseURL: 'https://localhost:5175' });

  let page: Page;
  let loginPage: LoginPage;
  let header: HeaderComponent;

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    header = new HeaderComponent(page);

    await loginPage.goto('https://localhost:5175/login');
    await loginPage.signInWithRetry(TEST_USER.email, TEST_USER.password, /\/timesheets\/my$/);
  });

  test.afterAll(async () => {
    await page?.close();
  });

  // TC-001 [NEW] (R-01)
  test('should sign in successfully with dedicated test user and redirect to /timesheets/my', async () => {
    await expect(page).toHaveURL(/\/timesheets\/my$/);
    const profileTrigger = page.locator('[aria-label^="User profile:"]');
    await expect(profileTrigger).toBeVisible();
    await expect(profileTrigger).toHaveText(/QA/);
  });

  // TC-002 [NEW] (R-02)
  test('should display header branding, quick search, dark mode toggle, and notification bell', async () => {
    await page.goto('/timesheets/my');

    // Quick search trigger
    await expect(page.getByRole('button', { name: 'Quick search' })).toBeVisible();

    // Dark mode toggle
    await expect(header.darkModeToggle).toBeVisible();

    // Notification bell
    await expect(header.notificationsButton).toBeVisible();

    // User profile avatar trigger with full accessible label
    await expect(page.locator('[aria-label="User profile: QA Automated Tester"]')).toBeVisible();
  });

  // TC-003 [NEW] (R-03)
  test('should render timesheet navigation links in sidebar', async () => {
    const nav = page.getByRole('navigation', { name: 'Timesheet navigation' });
    await expect(nav.getByRole('link', { name: 'My timesheet' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Log work' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Timesheet history' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Team timesheets' })).toBeVisible();
  });

  // TC-004 [NEW] (R-04)
  test('should navigate weekly periods using date controls', async () => {
    await page.goto('/timesheets/my');

    const prevBtn = page.getByRole('button', { name: 'Previous week' });
    const currentBtn = page.getByRole('button', { name: 'Current week' });
    const nextBtn = page.getByRole('button', { name: 'Next week' });

    await expect(prevBtn).toBeEnabled();
    await expect(currentBtn).toBeEnabled();
    await expect(nextBtn).toBeEnabled();

    // Period range text (e.g. 2026-11-16 to 2026-11-22)
    const rangeText = page.getByText(/\d{4}-\d{2}-\d{2} to \d{4}-\d{2}-\d{2}/);
    await expect(rangeText).toBeVisible();
    const initialRange = await rangeText.innerText();

    // Click next week
    await nextBtn.click();
    await expect(rangeText).not.toHaveText(initialRange);

    // Return to current week
    await currentBtn.click();
    await expect(rangeText).toHaveText(initialRange);
  });

  // TC-005 [NEW] (R-05)
  test('should render timesheet filter dropdown controls', async () => {
    await page.goto('/timesheets/my');

    // Project combobox
    const projectCombobox = page.getByRole('combobox', { name: 'Project' });
    await expect(projectCombobox).toBeVisible();
    await expect(projectCombobox.getByRole('option', { name: 'All projects' })).toBeAttached();

    // Issue ID spinbutton
    await expect(page.getByRole('spinbutton', { name: 'Issue ID' })).toBeVisible();

    // Billable combobox
    const billableCombobox = page.getByRole('combobox', { name: 'Billable' });
    await expect(billableCombobox).toBeVisible();
    await expect(
      billableCombobox.getByRole('option', { name: 'All (Billable & Non-billable)' }),
    ).toBeAttached();

    // Status combobox
    const statusCombobox = page.getByRole('combobox', { name: 'Status' });
    await expect(statusCombobox).toBeVisible();
    await expect(statusCombobox.getByRole('option', { name: 'All statuses' })).toBeAttached();
  });

  // TC-006 [NEW] (R-06)
  test('should display summary metric cards and daily breakdown', async () => {
    await page.goto('/timesheets/my');

    // Summary metric cards
    await expect(page.getByText('Total hours', { exact: true })).toBeVisible();
    await expect(page.getByText('Billable hours', { exact: true })).toBeVisible();
    await expect(page.getByText('Non-billable hours', { exact: true })).toBeVisible();

    // Mon–Sun daily hour cards
    await expect(page.getByText('Mon', { exact: true })).toBeVisible();
    await expect(page.getByText('Tue', { exact: true })).toBeVisible();
    await expect(page.getByText('Wed', { exact: true })).toBeVisible();
    await expect(page.getByText('Thu', { exact: true })).toBeVisible();
    await expect(page.getByText('Fri', { exact: true })).toBeVisible();
    await expect(page.getByText('Sat', { exact: true })).toBeVisible();
    await expect(page.getByText('Sun', { exact: true })).toBeVisible();
  });

  // TC-007 [NEW] (R-07)
  test('should switch layout between Table, Hierarchy, and Calendar views', async () => {
    await page.goto('/timesheets/my');

    // 1. Hierarchy view
    await page.getByRole('button', { name: 'Hierarchy view' }).click();
    await expect(page.getByRole('heading', { name: /Project & task breakdown/i })).toBeVisible();

    // 2. Weekly calendar view
    await page.getByRole('button', { name: 'Weekly calendar view' }).click();
    await expect(page.getByRole('heading', { name: /Weekly calendar view/i })).toBeVisible();

    // 3. Table view
    await page.getByRole('button', { name: 'Table view' }).click();
    await expect(page.getByRole('heading', { name: /Time by user & project/i })).toBeVisible();
  });

  // TC-008 [NEW] (R-08)
  test('should navigate to Log work page and verify form elements', async () => {
    await page.goto('/timesheets/log-work');

    await expect(page.getByRole('heading', { name: 'Log work', level: 1 })).toBeVisible();
    await expect(page.getByRole('combobox', { name: 'Issue' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Hours' })).toBeVisible();
    await expect(page.getByRole('textbox', { name: 'Description' })).toBeVisible();
    await expect(page.getByRole('checkbox', { name: 'Billable' })).toBeChecked();
    await expect(page.getByRole('button', { name: 'Save work log' })).toBeVisible();
  });

  // TC-009 [NEW] (R-09)
  test('should navigate to Timesheet history and display period records', async () => {
    await page.goto('/timesheets/history');

    await expect(page.getByRole('heading', { name: 'Timesheet history', level: 1 })).toBeVisible();
    await expect(page.getByText('Approved periods')).toBeVisible();
    await expect(page.getByText('Pending review')).toBeVisible();
    await expect(page.getByText('Draft / Rejected')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Export Payroll CSV' })).toBeVisible();
  });

  // TC-010 [NEW] (R-10)
  test('should navigate to Team timesheets and display team totals table', async () => {
    await page.goto('/timesheets/team');

    await expect(page.getByRole('heading', { name: 'Team timesheets', level: 1 })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Team totals' })).toBeVisible();

    const teamTable = page.getByRole('table').filter({ hasText: /Total hours/i });
    await expect(teamTable).toBeVisible();
    await expect(teamTable.getByRole('columnheader', { name: 'User' })).toBeVisible();
    await expect(teamTable.getByRole('columnheader', { name: 'Total hours' })).toBeVisible();
    await expect(
      teamTable.getByRole('columnheader', { name: 'Billable hours', exact: true }),
    ).toBeVisible();
    await expect(
      teamTable.getByRole('columnheader', { name: 'Non-billable hours', exact: true }),
    ).toBeVisible();
  });
});
