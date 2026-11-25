import { Locator, Page } from '@playwright/test';

export class IssueEditorPage {
  readonly page: Page;
  readonly form: Locator;
  readonly heading: Locator;
  readonly projectSelectTrigger: Locator;
  readonly issueTypeSelectTrigger: Locator;
  readonly summaryInput: Locator;
  readonly descriptionTextarea: Locator;
  readonly prioritySelectTrigger: Locator;
  readonly submitButton: Locator;
  readonly cancelButton: Locator;
  readonly validationAlert: Locator;

  constructor(page: Page) {
    this.page = page;
    this.form = page.locator('form.issue-form');
    this.heading = page.getByRole('heading', { level: 2 });
    this.projectSelectTrigger = page
      .locator('.field-wrapper')
      .filter({ hasText: /Project/i })
      .locator('.custom-select-trigger');
    this.issueTypeSelectTrigger = page
      .locator('.custom-select-container')
      .filter({ hasText: /Issue type/i })
      .locator('.custom-select-trigger');
    this.summaryInput = page.locator('input[name="summary"]');
    this.descriptionTextarea = page.locator('textarea#issue-description');
    this.prioritySelectTrigger = page
      .locator('.custom-select-container')
      .filter({ hasText: /Priority/i })
      .locator('.custom-select-trigger');
    this.submitButton = page.locator('form.issue-form button.primary');
    this.cancelButton = page.locator('form.issue-form a.button', { hasText: 'Cancel' });
    this.validationAlert = page.locator('.form-submit-feedback, [role="alert"]');
  }

  async goto(url: string = '/issues/create') {
    await this.page.waitForLoadState('domcontentloaded').catch(() => {});
    await this.page.goto(url, { waitUntil: 'domcontentloaded' });

    // If draft recovery dialog appears, discard draft to start fresh
    const discardDraftBtn = this.page.getByRole('button', { name: /discard draft/i });
    try {
      await discardDraftBtn.waitFor({ state: 'visible', timeout: 3000 });
      await discardDraftBtn.click();
    } catch {
      // Draft prompt did not appear
    }

    await this.form.waitFor({ state: 'visible', timeout: 15_000 });
  }

  async selectFirstProject() {
    await this.projectSelectTrigger.click();
    const listbox = this.page.getByRole('listbox');
    await listbox.waitFor({ state: 'visible', timeout: 5000 });

    const options = listbox.getByRole('option');
    const count = await options.count();
    if (count > 1) {
      await options.nth(1).click();
    } else if (count === 1) {
      await options.first().click();
    }

    // Wait for the project workflow to finish loading
    await this.page
      .locator('p[role="status"]:has-text("Loading project workflow")')
      .waitFor({ state: 'detached', timeout: 8000 })
      .catch(() => {});
  }

  async setSummary(text: string) {
    await this.summaryInput.fill(text);
  }

  async setDescription(text: string) {
    await this.descriptionTextarea.fill(text);
  }

  async setPriority(priorityLabel: string) {
    await this.prioritySelectTrigger.click();
    const listbox = this.page.getByRole('listbox');
    await listbox.waitFor({ state: 'visible', timeout: 5000 });
    await listbox.getByRole('option', { name: priorityLabel, exact: true }).click();
  }

  async submit() {
    await this.submitButton.click();
  }
}
