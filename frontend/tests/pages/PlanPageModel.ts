import { type Page, type Locator, expect } from '@playwright/test';

export class PlanPageModel {
  readonly page: Page;
  readonly baseUrl: string;

  // Header & Hero Banner
  readonly heading: Locator;
  readonly heroBanner: Locator;
  readonly heroBadge: Locator;
  readonly enabledMetricNumber: Locator;
  readonly pendingMetric: Locator;
  readonly pendingMetricNumber: Locator;

  // Sections
  readonly portalsSection: Locator;
  readonly toolsSection: Locator;
  readonly requestsSection: Locator;
  readonly requestsHeaderCount: Locator;
  readonly emptyRequestsCard: Locator;
  readonly requestsList: Locator;

  constructor(page: Page, baseUrl: string = 'https://localhost:5171') {
    this.page = page;
    this.baseUrl = baseUrl;

    this.heading = page.getByRole('heading', { name: 'Your plan', level: 1 });
    this.heroBanner = page.locator('.plan-hero-banner');
    this.heroBadge = page.locator('.plan-hero-badge');
    this.enabledMetricNumber = page
      .locator('.plan-hero-metrics .plan-metric-pill')
      .first()
      .locator('.metric-number');
    this.pendingMetric = page.locator('.plan-hero-metrics .plan-metric-pill.pending-metric');
    this.pendingMetricNumber = this.pendingMetric.locator('.metric-number');

    this.portalsSection = page.locator('section[aria-labelledby="plan-portals"]');
    this.toolsSection = page.locator('section[aria-labelledby="plan-tools"]');
    this.requestsSection = page.locator('section[aria-labelledby="plan-requests"]');
    this.requestsHeaderCount = this.requestsSection.locator('.plan-section-count');
    this.emptyRequestsCard = this.requestsSection.locator('.plan-empty-card');
    this.requestsList = page.locator('.plan-requests-list');
  }

  async goto() {
    if (!this.page.url().includes('/plan')) {
      await this.page.goto(`${this.baseUrl}/plan`);
    }
    await this.page.waitForLoadState('domcontentloaded');
    await expect(this.heading).toBeVisible({ timeout: 20_000 });
  }

  getItemRow(name: string): Locator {
    return this.page
      .locator('.plan-section:not([aria-labelledby="plan-requests"]) li.plan-item')
      .filter({ has: this.page.locator(`strong:has-text("${name}")`) });
  }

  getStatusBadge(name: string): Locator {
    return this.getItemRow(name).locator('.plan-status');
  }

  getCancelButton(name: string): Locator {
    return this.getItemRow(name).locator('button.plan-cancel-button');
  }

  getCancelPrompt(name: string): Locator {
    return this.getItemRow(name).locator('.plan-cancel-confirm');
  }

  getCancelSendButton(name: string): Locator {
    return this.getItemRow(name).locator('button.plan-cancel-send');
  }

  getCancelKeepButton(name: string): Locator {
    return this.getItemRow(name).locator('button.plan-cancel-keep');
  }

  getPendingCancelBadge(name: string): Locator {
    return this.getItemRow(name).locator('.plan-cancel-pending');
  }

  getRequestItem(name: string): Locator {
    return this.requestsList
      .locator('li.plan-request-item')
      .filter({ has: this.page.locator(`strong:has-text("${name}")`) });
  }

  getWithdrawButton(name: string): Locator {
    return this.getRequestItem(name).locator('button.plan-request-cancel');
  }

  async openCancellationPrompt(name: string) {
    const btn = this.getCancelButton(name);
    await btn.click();
    await expect(this.getCancelPrompt(name)).toBeVisible();
  }

  async dismissCancellationPrompt(name: string) {
    const keepBtn = this.getCancelKeepButton(name);
    await keepBtn.click();
    await expect(this.getCancelPrompt(name)).toBeHidden();
    await expect(this.getCancelButton(name)).toBeVisible();
  }

  async submitCancellation(name: string) {
    if (!(await this.getCancelPrompt(name).isVisible())) {
      await this.openCancellationPrompt(name);
    }
    const sendBtn = this.getCancelSendButton(name);
    await sendBtn.click();
    await expect(this.getPendingCancelBadge(name)).toBeVisible({ timeout: 10_000 });
  }

  async withdrawCancellation(requestTitle: string, itemName: string) {
    const withdrawBtn = this.getWithdrawButton(requestTitle);
    await withdrawBtn.click();
    await expect(this.getPendingCancelBadge(itemName)).toBeHidden({ timeout: 10_000 });
    await expect(this.getCancelButton(itemName)).toBeVisible({ timeout: 10_000 });
  }
}
