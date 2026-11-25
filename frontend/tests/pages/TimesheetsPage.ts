import { Locator, Page } from '@playwright/test';

export class TimesheetsPage {
  readonly page: Page;
  readonly table: Locator;
  readonly rows: Locator;
  readonly tableViewBtn: Locator;
  readonly hierarchyViewBtn: Locator;
  readonly calendarViewBtn: Locator;
  readonly inlineConfirm: Locator;
  readonly inlineConfirmDeleteBtn: Locator;

  constructor(page: Page) {
    this.page = page;
    this.table = page.locator('table.time-table');
    this.rows = page.locator('table.time-table tbody tr');
    this.tableViewBtn = page.getByRole('button', { name: 'Table view' });
    this.hierarchyViewBtn = page.getByRole('button', { name: 'Hierarchy view' });
    this.calendarViewBtn = page.getByRole('button', { name: /calendar view/i });
    this.inlineConfirm = page.locator('.time-confirm, [role="alert"]');
    this.inlineConfirmDeleteBtn = page.locator('.time-confirm button:has-text("Confirm delete")');
  }

  async goto(url: string = '/timesheets/my') {
    await this.page.waitForLoadState('domcontentloaded').catch(() => {});
    await this.page.goto(url, { waitUntil: 'domcontentloaded' });
    await this.page.waitForLoadState('domcontentloaded');
  }

  async switchLayout(mode: 'table' | 'hierarchy' | 'calendar') {
    if (mode === 'hierarchy') {
      await this.hierarchyViewBtn.click();
    } else if (mode === 'calendar') {
      await this.calendarViewBtn.click();
    } else {
      await this.tableViewBtn.click();
    }
  }

  async editLogByDescription(text: string) {
    const row = this.rows.filter({ hasText: text }).first();
    await row.locator('button.edit-action, button[aria-label="Edit"]').click();
  }

  async deleteLogByDescription(text: string) {
    const row = this.rows.filter({ hasText: text }).first();
    await row.locator('button.delete-action, button[aria-label="Delete"]').click();
    await this.inlineConfirm.waitFor({ state: 'visible', timeout: 5000 });
    await this.inlineConfirmDeleteBtn.click();
  }
}
