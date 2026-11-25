/**
 * Test Plan: test-plans/gridhub-projects-crud.plan.md
 * Feature: GridHub Projects — Lifecycle, CRUD, Hierarchy & Governance
 * Target environment: local (https://localhost:5173/projects)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, ProjectsPage } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('GridHub Projects Lifecycle (CRUD, Hierarchy & Governance)', () => {
  test.describe.configure({ mode: 'serial', retries: 2 });
  test.use({ baseURL: 'https://localhost:5173' });

  let page: Page;
  let loginPage: LoginPage;
  let projectsPage: ProjectsPage;

  const timestamp = Date.now();
  let projectKey = '';
  let accountName = '';
  let initialDesc = '';

  test.beforeAll(async ({ browser }, testInfo) => {
    const proj = testInfo.project.name.toLowerCase();
    projectKey = `P${proj[0].toUpperCase()}${timestamp.toString().slice(-6)}`;
    accountName = `E2E-Acc-${proj}-${timestamp}`;
    initialDesc = `E2E automated test project for GridHub projects lifecycle (${proj}).`;

    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    projectsPage = new ProjectsPage(page);

    await loginPage.goto('https://localhost:5173/login');
    await loginPage.signInWithRetry(
      TEST_USER.email,
      TEST_USER.password,
      /https:\/\/localhost:5173\/(dashboard|projects)/,
    );
    await expect(page.getByRole('banner')).toBeVisible({ timeout: 15_000 });
    await projectsPage.goto();
  });

  test.afterAll(async () => {
    try {
      if (page && !page.isClosed()) {
        const row = projectsPage.getRowByKey(projectKey);
        if (await row.isVisible({ timeout: 2000 }).catch(() => false)) {
          await projectsPage.openDeleteDialog(projectKey);
          await projectsPage.confirmDelete();
        }
      }
    } catch {
      // Best-effort cleanup
    } finally {
      await page?.close();
    }
  });

  // GH-PROJ-01 [NEW] (R-01, R-02)
  test('GH-PROJ-01: should prevent submission when required fields are empty', async () => {
    await projectsPage.openNewProjectDialog();
    await projectsPage.accountInput.fill('');
    await projectsPage.keyInput.fill('');
    await projectsPage.submitCreate();

    // Verify modal remains open and focused/error visible
    await expect(projectsPage.createDialog).toBeVisible();
    await projectsPage.cancelCreate(true);
  });

  // GH-PROJ-02 [NEW] (R-02)
  test('GH-PROJ-02: should reject invalid project key syntax starting with digits', async () => {
    await projectsPage.openNewProjectDialog();
    await projectsPage.fillAccount(accountName);
    await projectsPage.fillKey('123-INVALID');
    await projectsPage.submitCreate();

    await expect(projectsPage.keyError).toBeVisible();
    await expect(projectsPage.keyError).toContainText(/starting with a letter/i);

    await projectsPage.cancelCreate(true);
  });

  // GH-PROJ-03 [NEW] (R-01, R-03)
  test('GH-PROJ-03: should create new standalone project with valid metadata', async () => {
    await projectsPage.openNewProjectDialog();
    await projectsPage.fillAccount(accountName);
    await projectsPage.fillKey(projectKey);
    await projectsPage.selectProjectType('SOFTWARE');
    await projectsPage.fillDescription(initialDesc);
    await projectsPage.submitCreate();

    await expect(projectsPage.createDialog).toBeHidden({ timeout: 10_000 });
    await expect(projectsPage.noticeBanner).toContainText(
      new RegExp(`Project “${projectKey}” created`),
      {
        timeout: 10_000,
      },
    );
  });

  // GH-PROJ-04 [NEW] (R-04)
  test('GH-PROJ-04: should render newly created project in data table with badges', async () => {
    if (await projectsPage.clearSearchBtn.isVisible().catch(() => false)) {
      await projectsPage.clearSearch();
    }
    const row = projectsPage.getRowByKey(projectKey);
    await expect(row).toBeVisible({ timeout: 15_000 });

    // Inspect project key link
    const keyLink = row.locator('a.project-overview-link');
    await expect(keyLink).toContainText(projectKey);

    // Inspect account avatar initials badge
    const accountAbbr = row.locator('abbr.account-short');
    await expect(accountAbbr).toBeVisible();

    // Inspect project type badge
    const typeBadge = row.locator('.project-type');
    await expect(typeBadge).toContainText(/software/i);

    // Inspect status badge
    const statusBadge = row.locator('.badge');
    await expect(statusBadge).toContainText(/active/i);
  });

  // GH-PROJ-05 [NEW] (R-05)
  test('GH-PROJ-05: should reflect active project metrics in header stats ribbon', async () => {
    await expect(projectsPage.projectsCount).toBeVisible();
    await expect(projectsPage.projectsCount).toContainText(/\d+ projects?/i);
    await expect(projectsPage.activeBadge).toContainText(/\d+ active/i);
  });

  // GH-PROJ-06 [NEW] (R-06)
  test('GH-PROJ-06: should filter directory table by unique project key in search input', async () => {
    await projectsPage.search(projectKey);
    await expect(projectsPage.filterSummary).toContainText(/Showing 1 of \d+ projects/i, {
      timeout: 5000,
    });

    const matchingRow = projectsPage.getRowByKey(projectKey);
    await expect(matchingRow).toBeVisible();

    // Clear search and verify full table restores
    await projectsPage.clearSearch();
    await expect(projectsPage.filterSummary).not.toContainText('Showing 1 of', {
      timeout: 5000,
    });
  });

  // GH-PROJ-07 [NEW] (R-07)
  test('GH-PROJ-07: should toggle between List and Hierarchy view modes', async () => {
    await projectsPage.hierarchyViewBtn.click();
    await expect(projectsPage.hierarchyViewBtn).toHaveClass(/active/);
    await expect(projectsPage.hierarchyNotice).toBeVisible();

    await projectsPage.listViewBtn.click();
    await expect(projectsPage.listViewBtn).toHaveClass(/active/);
    await expect(projectsPage.hierarchyNotice).toBeHidden();
  });

  // GH-PROJ-08 [NEW] (R-08)
  test('GH-PROJ-08: should edit project metadata and update type to Banking', async () => {
    await projectsPage.openEditDialog(projectKey);
    await expect(projectsPage.manageHeading).toContainText(`Edit ${projectKey}`);

    const updatedDesc = `Updated banking governance description (${projectKey}).`;
    await projectsPage.updateDescription(updatedDesc);
    await projectsPage.selectEditProjectType('BANKING');
    await projectsPage.saveEdit();

    await expect(projectsPage.noticeBanner).toContainText(/Project updated/i, {
      timeout: 10_000,
    });

    const row = projectsPage.getRowByKey(projectKey);
    await expect(row.locator('.project-type')).toContainText(/banking/i);
    await expect(row.locator('.cell-description').first()).toContainText(updatedDesc);
  });

  // GH-PROJ-09 [NEW] (R-09)
  test('GH-PROJ-09: should delete the project via confirmation modal', async () => {
    await projectsPage.openDeleteDialog(projectKey);
    await expect(projectsPage.manageHeading).toContainText(`Delete ${projectKey}`);

    await projectsPage.confirmDelete();
    await expect(projectsPage.noticeBanner).toContainText(/Project deleted/i, {
      timeout: 10_000,
    });
  });

  // GH-PROJ-10 [NEW] (R-10)
  test('GH-PROJ-10: should verify deleted project is purged from table and search results', async () => {
    await projectsPage.search(projectKey);
    const row = projectsPage.getRowByKey(projectKey);
    await expect(row).toBeHidden();
    await expect(page.getByText(/No projects match your filters/i)).toBeVisible({ timeout: 5000 });
    await projectsPage.clearSearch();
  });
});
