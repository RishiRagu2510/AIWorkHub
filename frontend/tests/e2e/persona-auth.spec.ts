/**
 * Test Plan: test-plans/persona-authenticated.plan.md
 * Feature: Persona Portal (Authenticated Employee Directory, Profiles, Org Chart & Leave)
 * Target environment: local (https://localhost:5174)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, HeaderComponent } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('Persona Portal (Authenticated)', () => {
  test.describe.configure({ mode: 'serial', retries: 2 });
  test.use({ baseURL: 'https://localhost:5174' });

  let page: Page;
  let loginPage: LoginPage;
  let header: HeaderComponent;

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    header = new HeaderComponent(page);

    await loginPage.goto('https://localhost:5174/login');
    await loginPage.signInWithRetry(
      TEST_USER.email,
      TEST_USER.password,
      /https:\/\/localhost:5174\/(profile|employees)/,
    );
  });

  test.afterAll(async () => {
    await page?.close();
  });

  // TC-001 [NEW] (R-01)
  test('should sign in successfully with dedicated test user and establish session', async () => {
    await expect(page).not.toHaveURL(/\/login$/);
    const profileTrigger = page.locator('[aria-label^="User profile:"]');
    await expect(profileTrigger).toBeVisible();
    await expect(profileTrigger).toHaveText(/QA/);
  });

  // TC-002 [NEW] (R-02)
  test('should display header branding, quick search, dark mode toggle, and notification bell', async () => {
    await page.goto('/employees');

    // Quick search trigger
    await expect(
      page.getByRole('button', { name: /search people, directory, employees/i }),
    ).toBeVisible();

    // Dark mode toggle
    await expect(header.darkModeToggle).toBeVisible();

    // Notification bell
    await expect(header.notificationsButton).toBeVisible();

    // User profile badge with accessible title
    await expect(header.userProfileButton).toBeVisible();
  });

  // TC-003 [NEW] (R-03)
  test('should render employee navigation links in sidebar', async () => {
    const nav = page.getByRole('navigation', { name: 'Employee navigation' });
    await expect(nav.getByRole('link', { name: 'My profile' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Leave & WFH' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Organization chart' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Employee management' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Organization setup' })).toBeVisible();
    await expect(nav.getByRole('link', { name: 'Employee reports' })).toBeVisible();
  });

  // TC-004 [NEW] (R-04)
  test('should navigate to employee directory and display employee count and data table', async () => {
    await page.goto('/employees');

    await expect(page.getByRole('heading', { name: 'Employee directory', level: 1 })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/Showing \d+ of \d+ employees/i)).toBeVisible();
    await expect(page.getByRole('button', { name: 'Download CSV' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Spreadsheet View' })).toBeVisible();

    const table = page.getByRole('table');
    await expect(table).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Employee', exact: true })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Department' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Grade' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Status' })).toBeVisible();
  });

  // TC-005 [NEW] (R-05)
  test('should filter employee directory by keyword in searchbox', async () => {
    await page.goto('/employees');

    const searchBox = page.getByRole('searchbox', { name: 'Search' });
    await expect(searchBox).toBeVisible({ timeout: 15_000 });

    // Search for Akash
    await searchBox.fill('Akash');
    const table = page.getByRole('table');
    await expect(table.getByText('Akash Baskar')).toBeVisible();
    await expect(page.getByText(/Showing 1 of \d+ employee/i)).toBeVisible();

    // Clear search and verify full directory restored
    await searchBox.clear();
    await expect(page.getByText(/Showing \d+ of \d+ employees/i)).toBeVisible();
  });

  // TC-006 [NEW] (R-06)
  test('should render employee directory filter dropdowns', async () => {
    await page.goto('/employees');

    // Department combobox
    const deptCombobox = page.getByRole('combobox', { name: 'department' });
    await expect(deptCombobox).toBeAttached();
    await expect(deptCombobox.getByRole('option', { name: 'All departments' })).toBeAttached();

    // Grade combobox
    const gradeCombobox = page.getByRole('combobox', { name: 'grade' });
    await expect(gradeCombobox).toBeAttached();
    await expect(gradeCombobox.getByRole('option', { name: 'All grades' })).toBeAttached();

    // Status combobox
    const statusCombobox = page.getByRole('combobox', { name: 'Status' });
    await expect(statusCombobox).toBeAttached();
    await expect(statusCombobox.getByRole('option', { name: 'All Statuses' })).toBeAttached();
  });

  // TC-007 [NEW] (R-07)
  test('should navigate to individual employee profile and render details', async () => {
    await page.goto('/employees/26');

    await expect(page.getByRole('heading', { name: 'Akash Baskar' })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText('ACTIVE', { exact: true })).toBeVisible();
    await expect(page.getByText('# EMP-022')).toBeVisible();
    await expect(page.getByText('T1 · Junior Software Engineer', { exact: true })).toBeVisible();
    await expect(page.getByText('Developer', { exact: true })).toBeVisible();
    await expect(page.getByRole('region', { name: 'Company timeline' })).toBeVisible();
  });

  // TC-008 [NEW] (R-08)
  test('should navigate to organization chart and display hierarchy tree', async () => {
    await page.goto('/employees/org-chart');

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText(/\d+\s*employees\s*·\s*\d+\s*reporting managers/i)).toBeVisible();
    await expect(page.getByRole('region', { name: 'Full reporting tree' })).toBeVisible();
    await expect(page.getByText('Sivachandran Kannan')).toBeVisible();
    await expect(page.getByRole('button', { name: 'Fit to screen' })).toBeVisible();
  });

  // TC-009 [NEW] (R-09)
  test('should navigate to Leave & WFH dashboard and display balance cards', async () => {
    await page.goto('/employees/leave');

    await expect(page.getByRole('heading', { name: 'Leave & WFH', level: 1 })).toBeVisible({ timeout: 15_000 });
    await expect(page.getByText('Organization leave policy · 2026')).toBeVisible();

    // Balance cards
    await expect(page.getByRole('heading', { name: 'Floating holidays', level: 2 })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Sick leave', level: 2 })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Earned leave', level: 2 })).toBeVisible();

    // New request controls
    await expect(page.getByRole('combobox', { name: 'Request type' })).toBeAttached();
    await expect(page.getByRole('textbox', { name: 'Reason' })).toBeVisible();
  });

  // TC-010 [NEW] (R-10)
  test('should navigate to organization setup catalog and display metrics', async () => {
    await page.goto('/employees/setup');

    await expect(page.getByRole('heading', { name: 'Organization setup', level: 1 })).toBeVisible({ timeout: 15_000 });

    // Summary metric buttons
    await expect(page.getByRole('button', { name: /Departments\s*4/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Roles\s*6/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Permissions\s*74/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Grades\s*9/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /Job Titles\s*9/i })).toBeVisible();

    // Setup categories tab navigation
    const setupNav = page.getByRole('navigation', { name: 'Setup categories' });
    await expect(setupNav).toBeVisible();
    await expect(setupNav.getByRole('button', { name: /Departments/i })).toBeVisible();
  });
});
