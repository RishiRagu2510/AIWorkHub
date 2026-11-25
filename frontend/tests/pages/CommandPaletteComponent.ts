import { Page, Locator, expect } from '@playwright/test';

export class CommandPaletteComponent {
  readonly page: Page;
  readonly triggerButton: Locator;
  readonly dialog: Locator;
  readonly searchbox: Locator;
  readonly escDismissBadge: Locator;

  constructor(page: Page) {
    this.page = page;
    this.triggerButton = page.getByRole('button', { name: 'Quick search' });
    this.dialog = page.getByRole('dialog', { name: 'Global search' });
    this.searchbox = this.dialog.getByRole('searchbox', { name: 'Search' });
    this.escDismissBadge = page.locator('kbd.search-palette-esc');
  }

  async open() {
    await this.triggerButton.click();
    await expect(this.dialog).toBeVisible();
    await expect(this.searchbox).toBeVisible();
  }

  async dismiss() {
    await this.escDismissBadge.click();
    await expect(this.dialog).not.toBeVisible();
  }

  async expectAction(actionName: string) {
    const actionBtn = this.dialog.getByRole('button', { name: actionName });
    await expect(actionBtn).toBeVisible();
  }
}
