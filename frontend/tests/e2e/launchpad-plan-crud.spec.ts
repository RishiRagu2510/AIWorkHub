/**
 * Test Plan: test-plans/launchpad-plan-crud.plan.md
 * Feature: LaunchPad Plan Management & Access Request Lifecycle
 * Target environment: local (https://localhost:5171/plan)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, PlanPageModel } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('LaunchPad Plan Management & Access Request Lifecycle', () => {
  test.describe.configure({ mode: 'serial', retries: 2 });
  test.use({ baseURL: 'https://localhost:5171' });

  let page: Page;
  let loginPage: LoginPage;
  let planPage: PlanPageModel;

  let targetItem = 'Chronos';
  let requestTitle = 'Cancel Chronos';

  test.beforeAll(async ({ browser }, testInfo) => {
    const proj = testInfo.project.name.toLowerCase();
    // Assign distinct item per browser worker to ensure conflict-free parallel execution
    if (proj === 'firefox') {
      targetItem = 'Persona';
    } else if (proj === 'webkit') {
      targetItem = 'Athenna AI chat';
    } else {
      targetItem = 'Chronos';
    }
    requestTitle = `Cancel ${targetItem}`;

    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    planPage = new PlanPageModel(page);

    await loginPage.goto('https://localhost:5171/login');
    await loginPage.signInWithRetry(
      TEST_USER.email,
      TEST_USER.password,
      /https:\/\/localhost:5171\/?(\?.*)?$/,
    );
    await expect(page.getByRole('banner')).toBeVisible({ timeout: 15_000 });
    await planPage.goto();
  });

  test.afterAll(async () => {
    try {
      if (page && !page.isClosed()) {
        const withdrawBtn = planPage.getWithdrawButton(requestTitle);
        if (await withdrawBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
          await withdrawBtn.click();
          await page.waitForTimeout(1000);
        }
      }
    } catch {
      // Best-effort cleanup
    } finally {
      await page?.close();
    }
  });

  // LP-PLAN-01 [NEW] (R-01)
  test('LP-PLAN-01: should render title, description, and Enterprise Plan hero banner', async () => {
    await expect(planPage.heading).toBeVisible();
    await expect(planPage.heroBanner).toBeVisible();
    await expect(planPage.heroBadge).toContainText(/Enterprise Workspace Plan/i);
    await expect(page.getByText('Active Features & Services')).toBeVisible();
  });

  // LP-PLAN-02 [NEW] (R-02)
  test('LP-PLAN-02: should display Enabled Capabilities ratio in hero banner', async () => {
    await expect(planPage.enabledMetricNumber).toBeVisible();
    await expect(planPage.enabledMetricNumber).toContainText(/5\s*\/\s*5/);
    await expect(page.locator('.metric-label:has-text("Enabled Capabilities")')).toBeVisible();
  });

  // LP-PLAN-03 [NEW] (R-03)
  test('LP-PLAN-03: should list all 4 primary portals with Included status badges', async () => {
    await expect(planPage.portalsSection).toBeVisible();
    const expectedPortals = ['GridHub', 'Persona', 'Chronos', 'AIWorkHub'];

    for (const portal of expectedPortals) {
      const row = planPage.getItemRow(portal);
      await expect(row).toBeVisible();
      await expect(planPage.getStatusBadge(portal)).toContainText(/included/i);
    }
  });

  // LP-PLAN-04 [NEW] (R-04)
  test('LP-PLAN-04: should list Athenna AI chat with Included status badge', async () => {
    await expect(planPage.toolsSection).toBeVisible();
    const toolRow = planPage.getItemRow('Athenna AI chat');
    await expect(toolRow).toBeVisible();
    await expect(planPage.getStatusBadge('Athenna AI chat')).toContainText(/included/i);
  });

  // LP-PLAN-05 [NEW] (R-05, R-06)
  test('LP-PLAN-05: should open and safely dismiss cancellation prompt', async () => {
    await planPage.openCancellationPrompt(targetItem);
    const prompt = planPage.getCancelPrompt(targetItem);
    await expect(prompt).toBeVisible();
    await expect(prompt).toContainText(new RegExp(`Ask to cancel ${targetItem}`, 'i'));

    await planPage.dismissCancellationPrompt(targetItem);
  });

  // LP-PLAN-06 [NEW] (R-07)
  test('LP-PLAN-06: should submit cancellation request', async () => {
    await planPage.submitCancellation(targetItem);
  });

  // LP-PLAN-07 [NEW] (R-08)
  test('LP-PLAN-07: should display Cancellation Under Review status on item row', async () => {
    const pendingBadge = planPage.getPendingCancelBadge(targetItem);
    await expect(pendingBadge).toBeVisible({ timeout: 10_000 });
    await expect(pendingBadge).toContainText(/cancellation under review/i);

    // Cancel plan button is replaced by status
    await expect(planPage.getCancelButton(targetItem)).toBeHidden();
  });

  // LP-PLAN-08 [NEW] (R-02, R-09)
  test('LP-PLAN-08: should display submitted request in Requests section and hero pill', async () => {
    await expect(planPage.requestsSection).toBeVisible();
    const reqItem = planPage.getRequestItem(requestTitle);
    await expect(reqItem).toBeVisible({ timeout: 10_000 });
    await expect(reqItem.locator('.plan-request-badge')).toContainText(
      /cancellation under review/i,
    );

    // Verify hero pending metric pill is visible and shows at least 1 request
    await expect(planPage.pendingMetric).toBeVisible();
    await expect(planPage.pendingMetricNumber).toContainText(/[1-9]\d*/);
  });

  // LP-PLAN-09 [NEW] (R-10)
  test('LP-PLAN-09: should withdraw the cancellation request', async () => {
    await planPage.withdrawCancellation(requestTitle, targetItem);
  });

  // LP-PLAN-10 [NEW] (R-08, R-10)
  test('LP-PLAN-10: should verify restoration of Cancel Plan button on item', async () => {
    const cancelBtn = planPage.getCancelButton(targetItem);
    await expect(cancelBtn).toBeVisible({ timeout: 10_000 });
    await expect(planPage.getPendingCancelBadge(targetItem)).toBeHidden();
    await expect(planPage.getRequestItem(requestTitle)).toBeHidden();
  });
});
