/**
 * Test Plan: test-plans/hub-authenticated.plan.md
 * Feature: Hub Portal (Authenticated Central Copilot, Knowledge Base & Cross-Portal Launchpad)
 * Target environment: local (https://localhost:5172)
 */
import { test, expect, Page } from '@playwright/test';
import {
  LoginPage,
  HeaderComponent,
  CommandPaletteComponent,
  PortalSwitcherComponent,
} from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('Hub Portal (Authenticated)', () => {
  test.describe.configure({ mode: 'serial', retries: 2 });
  test.use({ baseURL: 'https://localhost:5172' });

  let page: Page;
  let loginPage: LoginPage;
  let header: HeaderComponent;
  let commandPalette: CommandPaletteComponent;
  let portalSwitcher: PortalSwitcherComponent;

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    header = new HeaderComponent(page);
    commandPalette = new CommandPaletteComponent(page);
    portalSwitcher = new PortalSwitcherComponent(page);

    await loginPage.goto('https://localhost:5172/login');
    await loginPage.signInWithRetry(
      TEST_USER.email,
      TEST_USER.password,
      /https:\/\/localhost:5172\/home/,
    );
  });

  test.afterAll(async () => {
    await page?.close();
  });

  // TC-001 [NEW] (R-01)
  test('should sign in successfully with dedicated test user and establish session', async () => {
    await expect(page).toHaveURL(/https:\/\/localhost:5172\/home/);
    await expect(header.userProfileButton).toBeVisible();
    await expect(header.userProfileButton).toHaveText(/QA/);
  });

  // TC-002 [NEW] (R-02)
  test('should display header branding, navigation links, and user profile avatar', async () => {
    await header.expectBranding('AIWorkHub');
    await expect(header.banner.getByRole('link', { name: 'AI Chat' })).toBeVisible();
    await expect(header.banner.getByRole('link', { name: 'Knowledge Base' })).toBeVisible();
    await expect(commandPalette.triggerButton).toBeVisible();
    await expect(header.darkModeToggle).toBeVisible();
    await expect(header.notificationsButton).toBeVisible();
    await expect(header.openPortalsButton).toBeVisible();
  });

  // TC-003 [NEW] (R-03)
  test('should render Athenna copilot welcome banner and RBAC status', async () => {
    await expect(
      page.getByRole('heading', { name: /Welcome back, QA Automated Tester/i }),
    ).toBeVisible();
    await expect(page.getByText('Athenna Autonomous Workspace Agent')).toBeVisible();
    await expect(page.getByText('Online & Ready')).toBeVisible();
    await expect(page.getByText('RBAC: ADMIN', { exact: true })).toBeVisible();
  });

  // TC-004 [NEW] (R-04)
  test('should display workspace summary overview cards in sidebar', async () => {
    await expect(page.getByRole('heading', { name: 'Daily Updated Issues' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Chat History' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Issue Completion' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Daily Work Log' })).toBeVisible();
  });

  // TC-005 [NEW] (R-05)
  test('should display quick action prompt cards for cross-portal queries', async () => {
    await expect(page.getByRole('button', { name: /GridHub Sprint Health/i })).toBeVisible();
    await expect(
      page.getByRole('button', { name: /Persona All Employees in Excel/i }),
    ).toBeVisible();
    await expect(
      page.getByRole('button', { name: /GridHub Issues Report in Excel/i }),
    ).toBeVisible();
    await expect(
      page.getByRole('button', { name: /Persona Leave Requests to Decide/i }),
    ).toBeVisible();
  });

  // TC-006 [NEW] (R-06)
  test('should render Athenna copilot prompt input box with model selector', async () => {
    const promptBox = page.getByRole('textbox', {
      name: /Ask Athenna across GridHub, Persona, and Chronos/i,
    });
    await expect(promptBox).toBeVisible();
    await expect(page.getByRole('button', { name: /Gemini Flash/i })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Attach files' })).toBeVisible();
    await expect(page.getByRole('button', { name: 'Send prompt' })).toBeVisible();
  });

  // TC-007 [NEW] (R-07)
  test('should open and dismiss Global Quick Search dialog with ESC key', async () => {
    await commandPalette.open();
    await commandPalette.expectAction('Create new issue');
    await commandPalette.expectAction('Browse all issues');
    await commandPalette.dismiss();
  });

  // TC-008 [NEW] (R-08)
  test('should open cross-portal launchpad popover and verify links', async () => {
    await portalSwitcher.open();
    await portalSwitcher.expectPortalLink(/GridHub/i, 'https://localhost:5173/dashboard');
    await portalSwitcher.expectPortalLink(/Persona/i, 'https://localhost:5174/profile');
    await portalSwitcher.expectPortalLink(/Chronos/i, 'https://localhost:5175/timesheets/my');
    await portalSwitcher.close();
  });

  // TC-009 [NEW] (R-09)
  test('should navigate to Company Knowledge Base and display indexed documents table', async () => {
    await page.goto('https://localhost:5172/knowledge');
    await expect(page.getByRole('heading', { name: 'Company Knowledge Base' })).toBeVisible();
    await expect(page.getByText('indexed docs')).toBeVisible();
    await expect(page.getByRole('heading', { name: /Indexed Documents/i })).toBeVisible();

    const table = page.getByRole('table');
    await expect(table).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Document' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Audience' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Version' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Last Updated' })).toBeVisible();
    await expect(table.getByRole('columnheader', { name: 'Action' })).toBeVisible();
    await expect(table.getByText('company policy handbook')).toBeVisible();
    await expect(table.getByText('gridhub project guide')).toBeVisible();
  });

  // TC-010 [NEW] (R-10)
  test('should open document text viewer modal, inspect contents, and dismiss reader', async () => {
    if (!page.url().includes('/knowledge')) {
      await page.goto('https://localhost:5172/knowledge');
    }
    const policyRow = page.getByRole('table').getByText('company policy handbook');
    await policyRow.click();

    const modalHeading = page.getByRole('heading', { name: 'company policy handbook' });
    await expect(modalHeading).toBeVisible();
    await expect(page.getByText(/Audience:\s*Everyone/i)).toBeVisible();
    await expect(
      page.getByText(/AIWORKHUB ENTERPRISE COMPANY POLICY & EMPLOYEE HANDBOOK/i),
    ).toBeVisible();

    const doneBtn = page.getByRole('button', { name: 'Done Reading' });
    await expect(doneBtn).toBeVisible();
    await doneBtn.click();
    await expect(doneBtn).not.toBeVisible();
  });
});
