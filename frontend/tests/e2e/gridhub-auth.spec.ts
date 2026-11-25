/**
 * Test Plan: test-plans/gridhub-authenticated.plan.md
 * Feature: GridHub Workspace (Authenticated Dashboard, Projects & Issues)
 * Target environment: local (https://localhost:5173)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, HeaderComponent } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('GridHub Workspace (Authenticated)', () => {
  test.describe.configure({ mode: 'serial', retries: 2 });
  test.use({ baseURL: 'https://localhost:5173' });

  let page: Page;
  let loginPage: LoginPage;
  let header: HeaderComponent;

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    header = new HeaderComponent(page);

    await loginPage.goto('https://localhost:5173/login');
    await loginPage.signInWithRetry(TEST_USER.email, TEST_USER.password, /\/dashboard$/);
  });

  test.afterAll(async () => {
    await page?.close();
  });

  // TC-001 [NEW] (R-01)
  test('should sign in successfully with dedicated test user and redirect to dashboard', async () => {
    await expect(page).toHaveURL(/\/dashboard$/);
    await expect(page.getByRole('heading', { name: /welcome back, qa!/i })).toBeVisible();
  });

  // TC-002 [NEW] (R-02)
  test('should display workspace metrics and summary cards on dashboard', async () => {
    await page.goto('/dashboard');

    // 1. Verify welcome greeting
    await expect(page.getByRole('heading', { name: /welcome back, qa!/i })).toBeVisible();

    // 2. Verify metric cards
    await expect(page.getByRole('link', { name: /explore projects/i })).toBeVisible();
    await expect(page.getByText(/total tracked issues/i)).toBeVisible();
    await expect(page.getByRole('link', { name: /completed issues/i })).toBeVisible();

    // 3. Verify action links & buttons
    await expect(page.getByRole('link', { name: 'Create issue', exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: /daily standup digest/i })).toBeVisible();
  });

  // TC-003 [NEW] (R-03)
  test('should render primary navigation links in sidebar', async () => {
    const sidebar = page.getByRole('navigation', { name: /main navigation/i });
    await expect(sidebar.getByRole('link', { name: 'Dashboard' })).toBeVisible();
    await expect(sidebar.getByRole('link', { name: 'Accounts & hierarchy' })).toBeVisible();
    await expect(sidebar.getByRole('link', { name: 'Projects' })).toBeVisible();
    await expect(sidebar.getByRole('link', { name: 'My work' })).toBeVisible();
    await expect(sidebar.getByRole('link', { name: 'Issues' })).toBeVisible();
    await expect(sidebar.getByRole('link', { name: 'Audit logs' })).toBeVisible();
  });

  // TC-004 [NEW] (R-04)
  test('should display recent projects portfolio in sidebar', async () => {
    const recentRegion = page.getByRole('region', { name: /recent projects/i });
    await expect(recentRegion).toBeVisible();

    // Portfolio project links
    await expect(recentRegion.getByRole('link', { name: /Google/i })).toBeVisible();
    await expect(recentRegion.getByRole('link', { name: /Visa/i })).toBeVisible();
    await expect(recentRegion.getByRole('link', { name: /Bench/i })).toBeVisible();
  });

  // TC-005 [NEW] (R-05)
  test('should navigate to projects directory and display data table', async () => {
    await page.goto('/projects');

    // Ensure filters are cleared if any were active
    const clearFiltersBtn = page.getByRole('button', { name: 'Clear filters' });
    if (await clearFiltersBtn.isEnabled({ timeout: 2000 }).catch(() => false)) {
      await clearFiltersBtn.click();
    }

    // 1. Verify heading
    await expect(page.getByRole('heading', { name: 'All projects' })).toBeVisible();

    // 2. Verify data table headers
    const table = page.getByRole('table');
    await expect(table).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Account' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Project' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Type' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Status' })).toBeVisible();

    // 3. Verify seeded projects rendered in table rows
    await expect(table.getByRole('cell', { name: 'Bench', exact: true })).toBeVisible({
      timeout: 10_000,
    });
    await expect(table.getByRole('cell', { name: 'Visa', exact: true })).toBeVisible();
    await expect(table.getByRole('cell', { name: 'Google', exact: true })).toBeVisible();
  });

  // TC-006 [NEW] (R-06)
  test('should filter projects by keyword in searchbox', async () => {
    await page.goto('/projects');

    const searchBox = page.getByRole('searchbox', { name: /search projects/i });
    await expect(searchBox).toBeVisible();

    // Ensure clean state
    const clearFiltersBtn = page.getByRole('button', { name: 'Clear filters' });
    if (await clearFiltersBtn.isEnabled({ timeout: 1000 }).catch(() => false)) {
      await clearFiltersBtn.click();
    }

    // 1. Filter by "Visa"
    await searchBox.fill('Visa');
    const table = page.getByRole('table');
    await expect(table.getByRole('cell', { name: 'Visa', exact: true })).toBeVisible({
      timeout: 5000,
    });
    await expect(table.getByRole('cell', { name: 'Bench', exact: true })).not.toBeVisible();

    // 2. Clear search text so subsequent tests have full list
    const clearTextBtn = page.getByRole('button', { name: /clear search text/i });
    if (await clearTextBtn.isVisible({ timeout: 1000 }).catch(() => false)) {
      await clearTextBtn.click();
    } else {
      await searchBox.fill('');
    }
    await expect(table.getByRole('cell', { name: 'Bench', exact: true })).toBeVisible({
      timeout: 5000,
    });
  });

  // TC-007 [NEW] (R-07)
  test('should display issues queue in list view with seeded tasks', async () => {
    await page.goto('/issues');

    // 1. Verify heading
    await expect(page.getByRole('heading', { name: 'Issues', exact: true })).toBeVisible();

    // 2. Verify seeded issues are listed
    await expect(page.getByText('FLP360-1').first()).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('FLP360-2').first()).toBeVisible();
    await expect(page.getByText('RAPID-3').first()).toBeVisible();
  });

  // TC-008 [NEW] (R-08)
  test('should switch issues view to Kanban board with status columns', async () => {
    await page.goto('/issues');

    // 1. Switch to Board view
    const boardBtn = page.getByRole('button', { name: 'Board view' });
    await expect(boardBtn).toBeVisible();
    await boardBtn.click();

    // 2. Verify URL contains board view
    await expect(page).toHaveURL(/View=board/i);

    // 3. Verify status columns
    await expect(page.getByRole('region', { name: 'To Do' })).toBeVisible({ timeout: 10_000 });
    await expect(page.getByRole('region', { name: 'Done' })).toBeVisible();

    // 4. Verify card present under TO DO
    await expect(page.getByText('FLP360-1').first()).toBeVisible();
  });

  // TC-009 [NEW] (R-09)
  test('should display issue detail pane when selecting an issue', async () => {
    await page.goto('/issues');

    // 1. Ensure in List view
    const listBtn = page.getByRole('button', { name: 'List view' });
    await listBtn.click();

    // 2. Click issue FLP360-1
    const issueItem = page.getByRole('button', {
      name: /login page throws 500 error on password reset request/i,
    });
    await expect(issueItem).toBeVisible({ timeout: 10_000 });
    await issueItem.click();

    // 3. Detail verification
    await expect(
      page.getByRole('heading', {
        name: /login page throws 500 error on password reset request/i,
      }),
    ).toBeVisible({ timeout: 10_000 });
    await expect(page.getByText('Vetri Selvam Pari').first()).toBeVisible();
  });

  // TC-010 [NEW] (R-10)
  test('should sign out user and redirect to login page', async () => {
    await page.goto('/dashboard');
    await header.signOut();
    await expect(
      page.getByRole('heading', { name: /sign in to your organization/i }),
    ).toBeVisible();
  });
});
