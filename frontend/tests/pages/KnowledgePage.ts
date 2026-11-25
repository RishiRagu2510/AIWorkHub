import { type Page, type Locator, expect } from '@playwright/test';

export class KnowledgePage {
  readonly page: Page;
  readonly baseUrl: string;

  // Header & Counters
  readonly heading: Locator;
  readonly docCounter: Locator;

  // Upload Panel & Controls
  readonly uploadPanel: Locator;
  readonly uploadForm: Locator;
  readonly fileInput: Locator;
  readonly dropzone: Locator;
  readonly dropzoneMainText: Locator;
  readonly titleInput: Locator;
  readonly everyoneAudienceBtn: Locator;
  readonly rolesAudienceBtn: Locator;
  readonly rolesChecklistCard: Locator;
  readonly fieldErrorHint: Locator;
  readonly uploadButton: Locator;
  readonly formNotice: Locator;

  // Documents Table & Empty State
  readonly listPanel: Locator;
  readonly docsTable: Locator;
  readonly docRows: Locator;
  readonly emptyState: Locator;

  // Document Preview Modal
  readonly previewBackdrop: Locator;
  readonly previewModal: Locator;
  readonly previewTitle: Locator;
  readonly previewMeta: Locator;
  readonly previewPre: Locator;
  readonly previewCloseButton: Locator;
  readonly previewCloseIcon: Locator;

  constructor(page: Page, baseUrl: string = 'https://localhost:5172') {
    this.page = page;
    this.baseUrl = baseUrl;

    this.heading = page.getByRole('heading', { name: /Company Knowledge Base/i });
    this.docCounter = page.locator('.knowledge-doc-counter .count-number');

    this.uploadPanel = page.locator('.knowledge-upload-panel');
    this.uploadForm = page.locator('form.knowledge-upload-form');
    this.fileInput = page.locator('input[type="file"]');
    this.dropzone = page.locator('.custom-dropzone');
    this.dropzoneMainText = page.locator('.dropzone-main-text');
    this.titleInput = page.locator('#doc-title-input');
    this.everyoneAudienceBtn = page.locator('button.audience-pill-btn:has-text("Everyone")');
    this.rolesAudienceBtn = page.locator('button.audience-pill-btn:has-text("Specific Roles")');
    this.rolesChecklistCard = page.locator('.roles-checklist-card');
    this.fieldErrorHint = page.locator('.field-error-hint');
    this.uploadButton = page.locator('button.upload-btn');
    this.formNotice = page.locator('.form-notice');

    this.listPanel = page.locator('.knowledge-list-panel');
    this.docsTable = page.locator('table.knowledge-data-table');
    this.docRows = page.locator('table.knowledge-data-table tbody tr');
    this.emptyState = page.locator('.knowledge-empty-state');

    this.previewBackdrop = page.locator('.doc-preview-backdrop');
    this.previewModal = page.locator('.doc-preview-modal');
    this.previewTitle = page.locator('.doc-preview-modal h3');
    this.previewMeta = page.locator('.doc-preview-modal .preview-meta');
    this.previewPre = page.locator('.doc-preview-modal pre');
    this.previewCloseButton = page.locator('button.preview-close-button');
    this.previewCloseIcon = page.locator('button.close-preview-btn');
  }

  async goto() {
    if (!this.page.url().includes('/knowledge')) {
      await this.page.goto(`${this.baseUrl}/knowledge`);
    }
    await this.page.waitForLoadState('domcontentloaded');
    await expect(this.heading).toBeVisible({ timeout: 20_000 });
  }

  async stageFile(name: string, content: string, mimeType: string = 'text/plain') {
    await this.fileInput.setInputFiles({
      name,
      mimeType,
      buffer: Buffer.from(content),
    });
    await expect(this.dropzoneMainText).toHaveText(name);
  }

  async setTitle(title: string) {
    await this.titleInput.fill(title);
  }

  async selectAudienceEveryone() {
    await this.everyoneAudienceBtn.click();
    await expect(this.everyoneAudienceBtn).toHaveClass(/selected/);
  }

  async selectAudienceRoles(roleLabels: string[] = []) {
    await this.rolesAudienceBtn.click();
    await expect(this.rolesAudienceBtn).toHaveClass(/selected/);
    for (const label of roleLabels) {
      const checkbox = this.rolesChecklistCard
        .locator('.role-checkbox-item', { hasText: label })
        .locator('input[type="checkbox"]');
      if (!(await checkbox.isChecked())) {
        await checkbox.check();
      }
    }
  }

  async submitUpload() {
    await this.uploadButton.click();
  }

  getRowByTitle(title: string): Locator {
    return this.docRows.filter({ hasText: title });
  }

  async openPreview(title: string) {
    const row = this.getRowByTitle(title);
    await expect(row).toBeVisible();
    await row.click();
    await expect(this.previewModal).toBeVisible({ timeout: 10_000 });
  }

  async closePreview() {
    await this.previewCloseButton.click();
    await expect(this.previewModal).toBeHidden({ timeout: 5000 });
  }

  async deleteDocument(title: string) {
    const row = this.getRowByTitle(title);
    await expect(row).toBeVisible();
    const trashBtn = row.locator('.table-action-delete');
    await trashBtn.click();

    const confirmBtn = row.locator('.confirm-delete-btn.yes');
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
  }
}
