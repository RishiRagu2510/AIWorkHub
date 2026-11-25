import { Page, Locator, expect } from '@playwright/test';

export class PortalSwitcherComponent {
  readonly page: Page;
  readonly triggerButton: Locator;
  readonly portalsNav: Locator;

  constructor(page: Page) {
    this.page = page;
    this.triggerButton = page.getByRole('button', { name: /open portals/i });
    this.portalsNav = page.getByRole('navigation', { name: 'Portals' });
  }

  async open() {
    await this.triggerButton.click();
    await expect(this.portalsNav).toBeVisible();
  }

  async close() {
    await this.triggerButton.click();
    await expect(this.portalsNav).not.toBeVisible();
  }

  async expectPortalLink(portalNameRegex: RegExp, targetUrl: string) {
    const link = this.portalsNav.getByRole('link', { name: portalNameRegex });
    await expect(link).toBeVisible();
    await expect(link).toHaveAttribute('href', targetUrl);
  }
}
