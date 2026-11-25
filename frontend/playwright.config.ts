import { defineConfig, devices } from '@playwright/test';

/**
 * Playwright E2E configuration for AIWorkHub frontend.
 * https://playwright.dev/docs/test-configuration
 */
export default defineConfig({
  testDir: './tests/e2e',
  timeout: 45_000,
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : undefined,
  reporter: [
    ['html', { open: 'never' }],
    ['list'],
  ],
  use: {
    baseURL: 'https://localhost:5173',
    ignoreHTTPSErrors: true,
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    headless: false,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
    {
      name: 'firefox',
      use: { ...devices['Desktop Firefox'] },
    },
    {
      name: 'webkit',
      use: { ...devices['Desktop Safari'] },
    },
  ],
  webServer: [
    {
      command: 'npm run dev:landing',
      url: 'https://localhost:5171',
      ignoreHTTPSErrors: true,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
    {
      command: 'npm run dev:hub',
      url: 'https://localhost:5172',
      ignoreHTTPSErrors: true,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
    {
      command: 'npm run dev:gridhub',
      url: 'https://localhost:5173',
      ignoreHTTPSErrors: true,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
    {
      command: 'npm run dev:persona',
      url: 'https://localhost:5174',
      ignoreHTTPSErrors: true,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
    {
      command: 'npm run dev:chronos',
      url: 'https://localhost:5175',
      ignoreHTTPSErrors: true,
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
});
