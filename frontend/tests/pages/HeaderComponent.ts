import { Page, Locator, expect } from '@playwright/test';

export class HeaderComponent {
  readonly page: Page;
  readonly banner: Locator;
  readonly darkModeToggle: Locator;
  readonly notificationsButton: Locator;
  readonly openPortalsButton: Locator;
  readonly userProfileButton: Locator;
  readonly signOutButton: Locator;

  constructor(page: Page) {
    this.page = page;
    this.banner = page.getByRole('banner');
    this.darkModeToggle = this.banner.getByRole('button', {
      name: /switch to dark mode/i,
    });
    this.notificationsButton = this.banner.getByRole('button', {
      name: /notifications/i,
    });
    this.openPortalsButton = this.banner.getByRole('button', {
      name: /open portals/i,
    });
    this.userProfileButton = this.banner.locator('[aria-label^="User profile:"]');
    this.signOutButton = this.banner.getByRole('button', { name: /sign out/i });
  }

  async expectBranding(portalName: string) {
    const brandLink = this.banner.getByRole('link', { name: portalName, exact: false });
    await expect(brandLink.first()).toBeVisible();
  }

  async openUserProfile() {
    await this.userProfileButton.click();
  }

  async expectUserProfile(name: string, email: string, role = 'ADMIN') {
    await expect(this.banner.getByText(name)).toBeVisible();
    await expect(this.banner.getByText(email)).toBeVisible();
    await expect(this.banner.getByText(role, { exact: true })).toBeVisible();
  }

  async signOut() {
    if (!(await this.signOutButton.isVisible().catch(() => false))) {
      await this.openUserProfile();
    }
    await this.signOutButton.click();
    await expect(this.page).toHaveURL(/\/login$/);
  }
}
