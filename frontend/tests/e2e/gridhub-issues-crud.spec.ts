/**
 * Test Plan: test-plans/gridhub-issues-crud.plan.md
 * Feature: GridHub Issues Lifecycle (Full CRUD Mutations & State Machine)
 * Target environment: local (https://localhost:5173)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, IssueEditorPage, IssueDetailsPage } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('GridHub Issues Lifecycle (CRUD & Mutations)', () => {
  test.describe.configure({ mode: 'serial', retries: 0 });
  test.use({ baseURL: 'https://localhost:5173' });

  let page: Page;
  let loginPage: LoginPage;
  let editorPage: IssueEditorPage;
  let detailsPage: IssueDetailsPage;

  const timestamp = Date.now();
  let createdSummary = `E2E-Task-${timestamp}`;
  let updatedSummary = `E2E-Task-${timestamp}-Updated`;
  let issueUrl = '';

  test.beforeAll(async ({ browser }) => {
    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    editorPage = new IssueEditorPage(page);
    detailsPage = new IssueDetailsPage(page);

    await loginPage.goto('https://localhost:5173/login');
    await loginPage.signInWithRetry(TEST_USER.email, TEST_USER.password, /\/dashboard$/);
    await expect(page.getByRole('heading', { name: /welcome back, qa!/i })).toBeVisible({
      timeout: 15_000,
    });
  });

  test.afterAll(async () => {
    await page?.close();
  });

  // GH-CRUD-01 [NEW] (R-01)
  test('should validate form when attempting to submit empty fields', async () => {
    await page.waitForLoadState('domcontentloaded').catch(() => {});
    await editorPage.goto('/issues/create');

    // Attempt to submit empty form
    await editorPage.submit();

    // Verify validation alert appears
    await expect(editorPage.validationAlert.first()).toBeVisible({ timeout: 5000 });
  });

  // GH-CRUD-02 [NEW] (R-02)
  test('should create a new task issue with unique summary and redirect to detail page', async () => {
    await editorPage.goto('/issues/create');

    // Select Project (e.g. Forever Living Products or first available)
    await editorPage.selectFirstProject();

    // Fill Summary
    await editorPage.setSummary(createdSummary);

    // Fill Description
    await editorPage.setDescription(
      'Automated Playwright E2E mutation test issue verifying the complete CRUD lifecycle.',
    );

    // Set Priority to High
    await editorPage.setPriority('High');

    // Submit issue
    await editorPage.submit();

    // Assert redirection to issue detail page (/issues/:id)
    await expect(page).toHaveURL(/\/issues\/\d+$/, { timeout: 15_000 });
    issueUrl = page.url();

    // Assert success notice
    await expect(page.getByText('Issue created.')).toBeVisible({ timeout: 10_000 });
  });

  // GH-CRUD-03 [NEW] (R-03)
  test('should display issue details, metadata, and description correctly', async () => {
    if (page.url() !== issueUrl) {
      await page.goto(issueUrl);
    }
    await page.waitForLoadState('domcontentloaded');

    // Verify title matches created summary
    await expect(page.getByRole('heading', { level: 1 })).toContainText(createdSummary);

    // Verify metadata shows Priority High and Task
    await expect(detailsPage.metadataList).toContainText('High');
    await expect(detailsPage.metadataList).toContainText('Task');
    await expect(detailsPage.transitionForm).toContainText('Move to status');

    // Verify description body
    await expect(page.locator('.description-content')).toContainText(
      'Automated Playwright E2E mutation test issue',
    );
  });

  // GH-CRUD-04 [NEW] (R-04)
  test('should transition issue status and verify status update notification', async () => {
    if (page.url() !== issueUrl) {
      await page.goto(issueUrl);
    }
    await page.waitForLoadState('domcontentloaded');

    // Transition status to next available status
    await detailsPage.transitionStatus();

    // Assert status updated notice
    await expect(page.getByText('Issue status updated.')).toBeVisible({ timeout: 10_000 });
  });

  // GH-CRUD-05 [NEW] (R-05)
  test('should add a new comment and verify it appears in the timeline', async () => {
    if (page.url() !== issueUrl) {
      await page.goto(issueUrl);
    }
    await page.waitForLoadState('domcontentloaded');

    const commentText = `Playwright E2E comment logged at timestamp ${timestamp}`;
    await detailsPage.addComment(commentText);

    // Assert comment notice
    await expect(page.getByText('Comment added.')).toBeVisible({ timeout: 10_000 });

    // Assert comment appears in list
    await expect(detailsPage.commentsList).toContainText(commentText);
  });

  // GH-CRUD-06 [NEW] (R-06)
  test('should edit issue summary and verify updated heading', async () => {
    if (page.url() !== issueUrl) {
      await page.goto(issueUrl);
    }
    await page.waitForLoadState('domcontentloaded');

    // Click Edit button
    await detailsPage.editButton.click();
    await expect(page).toHaveURL(/\/edit$/);

    // Update summary
    await editorPage.setSummary(updatedSummary);

    // Save changes
    await editorPage.submit();

    // Assert redirection to details and success notification
    await expect(page).toHaveURL(/\/issues\/\d+$/);
    await expect(page.getByText('Issue updated.')).toBeVisible({ timeout: 10_000 });

    // Assert heading reflects updated summary
    await expect(page.getByRole('heading', { level: 1 })).toContainText(updatedSummary);
  });

  // GH-CRUD-07 [NEW] (R-07)
  test('should locate the updated issue in the issues table list view', async () => {
    await page.goto('/issues');

    // Ensure in List view
    const listBtn = page.getByRole('button', { name: 'List view' });
    if (await listBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await listBtn.click();
    }

    // Filter by unique summary in search box
    const searchBox = page.getByRole('searchbox', { name: /search issues/i });
    await searchBox.fill(createdSummary);

    // Assert row with updated summary is visible
    await expect(page.getByText(updatedSummary).first()).toBeVisible({ timeout: 10_000 });
  });

  // GH-CRUD-08 [NEW] (R-08)
  test('should render the issue card in the Kanban board view', async () => {
    await page.goto('/issues?View=board');

    // Assert Board view loaded
    await expect(page.getByRole('region', { name: 'To Do' })).toBeVisible({
      timeout: 10_000,
    });

    // Assert issue card is present on board
    await expect(page.getByText(updatedSummary).first()).toBeVisible({ timeout: 10_000 });
  });

  // GH-CRUD-09 [NEW] (R-09)
  test('should delete the issue and redirect back to issues list', async () => {
    await page.waitForLoadState('domcontentloaded');
    await page.goto(issueUrl);
    await page.waitForLoadState('domcontentloaded');

    // Trigger delete and confirm
    await detailsPage.deleteIssue();

    // Assert redirect back to /issues
    await expect(page).toHaveURL(/\/issues(?:\?.*)?$/);

    // Assert deleted notification
    await expect(page.getByText('Issue deleted.')).toBeVisible({ timeout: 10_000 });
  });

  // GH-CRUD-10 [NEW] (R-10)
  test('should verify the deleted issue no longer appears in search results', async () => {
    if (!page.url().includes('/issues')) {
      await page.goto('/issues');
    }
    await page.waitForLoadState('domcontentloaded');

    // Filter by deleted issue summary
    const searchBox = page.getByRole('searchbox', { name: /search issues/i });
    await searchBox.fill(createdSummary);

    // Assert the deleted issue is no longer found
    await expect(page.getByText(updatedSummary)).not.toBeVisible({ timeout: 5000 });
  });
});
