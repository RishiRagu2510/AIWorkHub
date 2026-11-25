import { Locator, Page } from '@playwright/test';

export class IssueDetailsPage {
  readonly page: Page;
  readonly titleHeading: Locator;
  readonly issueKeyEyebrow: Locator;
  readonly editButton: Locator;
  readonly deleteButton: Locator;
  readonly deleteDialog: Locator;
  readonly confirmDeleteButton: Locator;
  readonly metadataList: Locator;
  readonly transitionForm: Locator;
  readonly transitionButton: Locator;
  readonly commentTextarea: Locator;
  readonly commentSubmitButton: Locator;
  readonly commentsList: Locator;
  readonly snackbar: Locator;

  constructor(page: Page) {
    this.page = page;
    this.titleHeading = page.locator('.page-header h1, h1');
    this.issueKeyEyebrow = page.locator('.page-header .eyebrow');
    this.editButton = page.locator('.action-btn-edit');
    this.deleteButton = page.locator('.action-btn-delete');
    this.deleteDialog = page.getByRole('dialog');
    this.confirmDeleteButton = page
      .getByRole('dialog')
      .locator('button.danger, button:has-text("Delete")');
    this.metadataList = page.locator('aside.metadata dl');
    this.transitionForm = page.locator('form.transition-form');
    this.transitionButton = page.locator('form.transition-form button.primary');
    this.commentTextarea = page.locator('form.comment-form textarea');
    this.commentSubmitButton = page.locator('form.comment-form button.primary');
    this.commentsList = page.locator('ul.comments');
    this.snackbar = page.locator('.snackbar, [role="status"]');
  }

  async goto(issueId: string | number) {
    await this.page.goto(`/issues/${issueId}`);
    await this.titleHeading.waitFor({ state: 'visible', timeout: 15_000 });
  }

  async transitionStatus(statusLabel?: string) {
    await this.transitionForm.waitFor({ state: 'visible', timeout: 10_000 });
    if (statusLabel) {
      const trigger = this.transitionForm.locator('.custom-select-trigger');
      await trigger.click();
      const listbox = this.page.getByRole('listbox');
      await listbox.waitFor({ state: 'visible', timeout: 5000 });
      await listbox.getByRole('option', { name: statusLabel }).click();
    }
    await this.transitionButton.click();

    // If confirmation dialog appears for non-standard transition, confirm it
    const confirmBtn = this.page.getByRole('button', { name: /confirm transition/i });
    if (await confirmBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await confirmBtn.click();
    }
  }

  async addComment(text: string) {
    await this.commentTextarea.fill(text);
    await this.commentSubmitButton.click();
  }

  async deleteIssue() {
    await this.deleteButton.click();
    await this.deleteDialog.waitFor({ state: 'visible', timeout: 5000 });
    await this.confirmDeleteButton.click();
  }
}
