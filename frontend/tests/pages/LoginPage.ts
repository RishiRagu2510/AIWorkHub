import { Page, Locator, expect } from '@playwright/test';

export class LoginPage {
  readonly page: Page;
  readonly emailInput: Locator;
  readonly passwordInput: Locator;
  readonly submitButton: Locator;
  readonly googleSsoButton: Locator;
  readonly troubleSigningInButton: Locator;
  readonly alertMessage: Locator;

  constructor(page: Page) {
    this.page = page;
    this.emailInput = page.getByRole('textbox', { name: /company email/i });
    this.passwordInput = page.getByLabel(/^password$/i);
    this.submitButton = page.getByRole('button', { name: /sign in with email/i });
    this.googleSsoButton = page.getByRole('button', {
      name: /continue with google/i,
    });
    this.troubleSigningInButton = page.getByText(/trouble signing in/i);
    this.alertMessage = page.getByRole('alert');
  }

  async goto(url: string) {
    await this.page.goto(url);
    await expect(this.emailInput).toBeVisible({ timeout: 10_000 });
  }

  async fillCredentials(email: string, pass: string) {
    await this.emailInput.fill(email);
    await this.passwordInput.fill(pass);
  }

  async submit() {
    await this.submitButton.click();
  }

  async signIn(email: string, pass: string) {
    await this.fillCredentials(email, pass);
    await this.submit();
  }

  async signInWithRetry(email: string, pass: string, targetUrlPattern: RegExp, maxRetries = 3) {
    await this.fillCredentials(email, pass);
    await this.submit();

    for (let i = 0; i < maxRetries; i++) {
      if (targetUrlPattern.test(this.page.url())) {
        return;
      }

      // Check if redirect occurs within 4s
      const redirected = await this.page
        .waitForURL(targetUrlPattern, { timeout: 4000 })
        .then(() => true)
        .catch(() => false);
      if (redirected) {
        await this.page.waitForLoadState('domcontentloaded').catch(() => {});
        return;
      }

      // If not redirected, check if backend rate-limited the attempt
      const isAlert = await this.alertMessage.isVisible({ timeout: 3000 }).catch(() => false);
      if (isAlert) {
        const msg = await this.alertMessage.innerText().catch(() => '');
        if (msg.includes('Too many attempts')) {
          await this.page.waitForTimeout(16_000);
          await this.submit();
          continue;
        }
      }
      break;
    }

    await expect(this.page).toHaveURL(targetUrlPattern, { timeout: 25_000 });
    await this.page.waitForLoadState('domcontentloaded').catch(() => {});
  }
}
