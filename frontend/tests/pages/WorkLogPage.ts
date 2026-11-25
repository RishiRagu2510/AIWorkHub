import { Locator, Page } from '@playwright/test';

export class WorkLogPage {
  readonly page: Page;
  readonly form: Locator;
  readonly issueSelect: Locator;
  readonly durationInput: Locator;
  readonly descriptionTextarea: Locator;
  readonly billableCheckbox: Locator;
  readonly submitButton: Locator;
  readonly successBanner: Locator;
  readonly targetTotal: Locator;
  readonly errorMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.form = page.locator('form.time-form');
    this.issueSelect = page.locator('form.time-form select');
    this.durationInput = page.locator('form.time-form input[name="duration"]');
    this.descriptionTextarea = page.locator('textarea#worklog-description');
    this.billableCheckbox = page.locator('form.time-form input[name="billable"]');
    this.submitButton = page.locator('form.time-form button.primary');
    this.successBanner = page.locator('.logwork-success-banner');
    this.targetTotal = page.locator('.target-total-val');
    this.errorMessage = page.locator(
      '.time-form [role="alert"], .time-form .error, .time-form .error-state',
    );
  }

  async goto(url: string = '/timesheets/log-work') {
    await this.page.waitForLoadState('domcontentloaded').catch(() => {});
    await this.page.goto(url, { waitUntil: 'domcontentloaded' });
    await this.form.waitFor({ state: 'visible', timeout: 15_000 });
  }

  async selectFirstIssue() {
    await this.issueSelect.waitFor({ state: 'visible', timeout: 10_000 });
    const validOption = this.issueSelect.locator('option:not([value=""]):not([disabled])').first();
    await validOption.waitFor({ state: 'attached', timeout: 10_000 });
    const val = await validOption.getAttribute('value');
    if (val) {
      await this.issueSelect.selectOption(val);
    }
  }

  async setHours(hours: string) {
    await this.durationInput.fill(hours);
  }

  async setDescription(text: string) {
    await this.descriptionTextarea.fill(text);
  }

  async submit() {
    await this.submitButton.click();
  }
}
