import { type Page, type Locator, expect } from '@playwright/test';

export class ProjectsPage {
  readonly page: Page;
  readonly baseUrl: string;

  // Header & Controls
  readonly heading: Locator;
  readonly newProjectBtn: Locator;
  readonly projectsCount: Locator;
  readonly activeBadge: Locator;

  // Search & Filters
  readonly searchInput: Locator;
  readonly clearSearchBtn: Locator;
  readonly clearFiltersBtn: Locator;
  readonly filterSummary: Locator;
  readonly listViewBtn: Locator;
  readonly hierarchyViewBtn: Locator;
  readonly hierarchyNotice: Locator;

  // Data Table
  readonly table: Locator;
  readonly rows: Locator;
  readonly emptyCell: Locator;

  // Create Project Dialog
  readonly createDialog: Locator;
  readonly accountInput: Locator;
  readonly keyInput: Locator;
  readonly keyError: Locator;
  readonly projectTypeContainer: Locator;
  readonly displayNameInput: Locator;
  readonly descriptionInput: Locator;
  readonly createSubmitBtn: Locator;
  readonly createCancelBtn: Locator;
  readonly createCloseIcon: Locator;

  // Manage (Edit / Move / Delete) Dialog
  readonly manageDialog: Locator;
  readonly manageHeading: Locator;
  readonly editDescriptionInput: Locator;
  readonly editProjectTypeContainer: Locator;
  readonly manageSaveBtn: Locator;
  readonly manageDeleteConfirmBtn: Locator;
  readonly manageCancelBtn: Locator;

  // Global Notice
  readonly noticeBanner: Locator;

  constructor(page: Page, baseUrl: string = 'https://localhost:5173') {
    this.page = page;
    this.baseUrl = baseUrl;

    this.heading = page.getByRole('heading', { name: 'Projects', level: 1 });
    this.newProjectBtn = page.getByRole('button', { name: /New project/i });
    this.projectsCount = page.locator('.projects-stats-ribbon .projects-count');
    this.activeBadge = page.locator('.projects-stats-ribbon .projects-status-badge');

    this.searchInput = page.locator('input[name="projectSearch"]');
    this.clearSearchBtn = page.locator('button[aria-label="Clear search text"]');
    this.clearFiltersBtn = page.locator('button.project-clear-filters-btn');
    this.filterSummary = page.locator('.project-filter-summary');
    this.listViewBtn = page.locator('.view-toggle-btn:has-text("List")');
    this.hierarchyViewBtn = page.locator('.view-toggle-btn:has-text("Hierarchy")');
    this.hierarchyNotice = page.getByText(/Projects are grouped beneath their accessible parents/i);

    this.table = page.locator('table');
    this.rows = page.locator('table tbody tr');
    this.emptyCell = page.locator('.empty-cell, .empty, .empty-state');

    // Dialogs
    this.createDialog = page
      .locator('dialog.project-create-dialog')
      .filter({ has: page.locator('#project-create-title') });
    this.accountInput = this.createDialog.locator('input[name="name"]');
    this.keyInput = this.createDialog.locator('input[name="key"]');
    this.keyError = this.createDialog.locator('#project-key-error');
    this.projectTypeContainer = this.createDialog.locator(
      '.project-type-wrapper .custom-select-container',
    );
    this.displayNameInput = this.createDialog.locator('input[name="displayName"]');
    this.descriptionInput = this.createDialog.locator('textarea[name="description"]');
    this.createSubmitBtn = this.createDialog.locator('button.btn-create');
    this.createCancelBtn = this.createDialog.locator('button.btn-cancel');
    this.createCloseIcon = this.createDialog.locator('button.dialog-close-btn');

    this.manageDialog = page
      .locator('dialog.project-create-dialog')
      .filter({ has: page.locator('#manage-project-title') });
    this.manageHeading = this.manageDialog.locator('#manage-project-title');
    this.editDescriptionInput = this.manageDialog.locator('textarea[name="description"]');
    this.editProjectTypeContainer = this.manageDialog.locator('.custom-select-container');
    this.manageSaveBtn = this.manageDialog.locator('button.primary:has-text("Save project")');
    this.manageDeleteConfirmBtn = this.manageDialog.locator(
      'button.danger:has-text("Delete project")',
    );
    this.manageCancelBtn = this.manageDialog.locator('.form-footer button:has-text("Cancel")');

    this.noticeBanner = page.locator('.snackbar, .notice');
  }

  async goto() {
    await this.page.goto(`${this.baseUrl}/projects`);
    await this.page.waitForLoadState('domcontentloaded');
    await expect(this.heading).toBeVisible({ timeout: 20_000 });
    if (await this.clearFiltersBtn.isEnabled().catch(() => false)) {
      await this.clearFiltersBtn.click().catch(() => {});
    } else if (await this.clearSearchBtn.isVisible().catch(() => false)) {
      await this.clearSearchBtn.click().catch(() => {});
    }
  }

  async openNewProjectDialog() {
    await this.newProjectBtn.click();
    await expect(this.createDialog).toBeVisible();
  }

  async fillAccount(name: string) {
    await this.accountInput.fill(name);
  }

  async fillKey(key: string) {
    await this.keyInput.fill(key);
  }

  async selectProjectType(type: 'SOFTWARE' | 'SERVICE' | 'BUSINESS' | 'BANKING') {
    const select = this.createDialog.locator('select[name="projectType"]');
    await select.selectOption(type, { force: true });
  }

  async fillDescription(desc: string) {
    await this.descriptionInput.fill(desc);
  }

  async submitCreate() {
    await this.createSubmitBtn.click();
  }

  async cancelCreate(discardUnsaved = false) {
    await this.createCancelBtn.click();
    if (discardUnsaved) {
      const discardBtn = this.page.locator('dialog button.danger:has-text("Discard changes")');
      if (await discardBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
        await discardBtn.click();
      }
    }
    await expect(this.createDialog).toBeHidden();
  }

  getRowByKey(key: string): Locator {
    return this.rows.filter({ has: this.page.locator(`strong:has-text("${key}")`) });
  }

  async search(query: string) {
    await this.searchInput.fill(query);
  }

  async clearSearch() {
    if (await this.clearSearchBtn.isVisible()) {
      await this.clearSearchBtn.click();
    } else {
      await this.searchInput.fill('');
    }
  }

  async openEditDialog(key: string) {
    const row = this.getRowByKey(key);
    await row.locator('button:has-text("Edit")').click();
    await expect(this.manageDialog).toBeVisible();
  }

  async updateDescription(desc: string) {
    await this.editDescriptionInput.fill(desc);
  }

  async selectEditProjectType(type: 'SOFTWARE' | 'SERVICE' | 'BUSINESS' | 'BANKING') {
    const select = this.manageDialog.locator('select[name="projectType"]');
    await select.selectOption(type, { force: true });
  }

  async saveEdit() {
    await this.manageSaveBtn.click();
    await expect(this.manageDialog).toBeHidden();
  }

  async openDeleteDialog(key: string) {
    const row = this.getRowByKey(key);
    await row.locator('button.project-delete-action').click();
    await expect(this.manageDialog).toBeVisible();
  }

  async confirmDelete() {
    await this.manageDeleteConfirmBtn.click();
    await expect(this.manageDialog).toBeHidden();
  }
}
