/**
 * Test Plan: test-plans/launchpad-authenticated.plan.md
 * Feature: LaunchPad Portal (Authenticated Workspace Launcher & Organization Plan)
 * Target environment: local (https://localhost:5171)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, HeaderComponent } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('LaunchPad Portal (Authenticated)', () => {
  test.describe.configure({ mode: 'serial', retries: 2 });
  test.use({ baseURL: 'https://localhost:5171' });

  let page: Page;
  let loginPage: LoginPage;
  let header: HeaderComponent;

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    header = new HeaderComponent(page);

    await loginPage.goto('https://localhost:5171/login');
    await loginPage.signInWithRetry(
      TEST_USER.email,
      TEST_USER.password,
      /https:\/\/localhost:5171\/?$/,
    );
  });

  test.afterAll(async () => {
    await page?.close();
  });

  // TC-001 [NEW] (R-01)
  test('should sign in successfully with dedicated test user and establish session', async () => {
    await expect(page).toHaveURL(/https:\/\/localhost:5171\/?$/);
    const profileTrigger = page.locator('[aria-label^="User profile:"]');
    await expect(profileTrigger).toBeVisible();
    await expect(profileTrigger).toHaveText(/QA/);
  });

  // TC-002 [NEW] (R-02)
  test('should display header branding, controls, and user profile avatar', async () => {
    await header.expectBranding('LaunchPad');
    await expect(header.darkModeToggle).toBeVisible();
    await expect(header.notificationsButton).toBeVisible();
    await expect(header.openPortalsButton).toBeVisible();
  });

  // TC-003 [NEW] (R-03)
  test('should render authenticated welcome heading and subtitle description', async () => {
    await expect(
      page.getByRole('heading', { name: /Welcome, QA Automated Tester/i }),
    ).toBeVisible();
    await expect(page.getByText(/Choose a workspace portal to manage sprints/i)).toBeVisible();
  });

  // TC-004 [NEW] (R-04)
  test('should display all 4 primary workspace portal launch tiles', async () => {
    const portalsNav = page.getByRole('navigation', { name: 'Portals' });
    await expect(portalsNav).toBeVisible();
    await expect(portalsNav.getByRole('link', { name: /GridHub/i })).toBeVisible();
    await expect(portalsNav.getByRole('link', { name: /Persona/i })).toBeVisible();
    await expect(portalsNav.getByRole('link', { name: /Chronos/i })).toBeVisible();
    await expect(portalsNav.getByRole('link', { name: /AIWorkHub/i })).toBeVisible();
  });

  // TC-005 [NEW] (R-05)
  test('should verify external target URLs for all 4 portal launch tiles', async () => {
    const portalsNav = page.getByRole('navigation', { name: 'Portals' });
    await expect(portalsNav.getByRole('link', { name: /GridHub/i })).toHaveAttribute(
      'href',
      'https://localhost:5173/dashboard',
    );
    await expect(portalsNav.getByRole('link', { name: /Persona/i })).toHaveAttribute(
      'href',
      'https://localhost:5174/profile',
    );
    await expect(portalsNav.getByRole('link', { name: /Chronos/i })).toHaveAttribute(
      'href',
      'https://localhost:5175/timesheets/my',
    );
    await expect(portalsNav.getByRole('link', { name: /AIWorkHub/i })).toHaveAttribute(
      'href',
      'https://localhost:5172/home',
    );
  });

  // TC-006 [NEW] (R-06)
  test('should render plan overview section on home page', async () => {
    const planRegion = page.getByRole('region', { name: 'Your plan' });
    await expect(planRegion).toBeVisible();
    await expect(planRegion.getByText('AIWorkHub Plan')).toBeVisible();
    await expect(planRegion.getByText(/4 of 4 portals · 1 of 1 tool included/i)).toBeVisible();

    const manageLink = planRegion.getByRole('link', { name: 'Manage plan' });
    await expect(manageLink).toBeVisible();
    await expect(manageLink).toHaveAttribute('href', '/plan');
  });

  // TC-007 [NEW] (R-07)
  test('should navigate to plan management page and display plan metrics', async () => {
    await page.goto('https://localhost:5171/plan');
    await expect(page.getByRole('heading', { name: 'Your plan' })).toBeVisible();
    await expect(page.getByText('Enterprise Workspace Plan')).toBeVisible();
    await expect(page.getByText('Enabled Capabilities')).toBeVisible();
    await expect(page.getByText(/5\s*\/\s*5/)).toBeVisible();

    const backLink = page.getByRole('link', { name: /All portals/i });
    await expect(backLink).toBeVisible();
    await expect(backLink).toHaveAttribute('href', '/');
  });

  // TC-008 [NEW] (R-08)
  test('should render active portals list on plan management page', async () => {
    // Ensure on /plan
    if (!page.url().includes('/plan')) {
      await page.goto('https://localhost:5171/plan');
    }
    const portalsRegion = page.getByRole('region', { name: 'Portals' });
    await expect(portalsRegion).toBeVisible();
    await expect(portalsRegion.getByText(/4 of 4 included/i)).toBeVisible();
    await expect(portalsRegion.getByText('GridHub')).toBeVisible();
    await expect(portalsRegion.getByText('Persona')).toBeVisible();
    await expect(portalsRegion.getByText('Chronos')).toBeVisible();
    await expect(portalsRegion.getByText('AIWorkHub')).toBeVisible();
  });

  // TC-009 [NEW] (R-09)
  test('should render active tools list and access requests status', async () => {
    // Ensure on /plan
    if (!page.url().includes('/plan')) {
      await page.goto('https://localhost:5171/plan');
    }
    const toolsRegion = page.getByRole('region', { name: 'Tools' });
    await expect(toolsRegion).toBeVisible();
    await expect(toolsRegion.getByText(/1 of 1 included/i)).toBeVisible();
    await expect(toolsRegion.getByText('Athenna AI chat')).toBeVisible();

    const requestsRegion = page.getByRole('region', {
      name: /Requests from your company/i,
    });
    await expect(requestsRegion).toBeVisible();
    await expect(
      requestsRegion
        .getByText(/No open requests/i)
        .or(requestsRegion.locator('.plan-requests-list')),
    ).toBeVisible();
  });

  // TC-010 [NEW] (R-10)
  test('should open user profile menu, inspect user details, and verify sign out button', async () => {
    await header.openUserProfile();
    await header.expectUserProfile('QA Automated Tester', 'qa.test@griddynamics.com', 'ADMIN');
    await expect(header.banner.getByText('Grid Dynamics')).toBeVisible();
    await expect(header.signOutButton).toBeVisible();
  });
});
