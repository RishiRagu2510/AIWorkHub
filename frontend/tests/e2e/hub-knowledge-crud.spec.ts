/**
 * Test Plan: test-plans/hub-knowledge-crud.plan.md
 * Feature: Hub Portal (Knowledge Base Documents Mutation Lifecycle)
 * Target environment: local (https://localhost:5172)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, KnowledgePage } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('Hub Knowledge Base Lifecycle (CRUD & Mutations)', () => {
  test.describe.configure({ mode: 'serial', retries: 0 });
  test.use({ baseURL: 'https://localhost:5172' });

  let page: Page;
  let loginPage: LoginPage;
  let knowledgePage: KnowledgePage;

  const timestamp = Date.now();
  let docTitle = '';
  let fileName = '';
  let docContent = '';

  test.beforeAll(async ({ browser }, testInfo) => {
    const proj = testInfo.project.name.toLowerCase();
    docTitle = `E2E-Doc-${proj}-${timestamp}`;
    fileName = `e2e-guide-${proj}-${timestamp}.txt`;
    docContent = `Playwright QA Ingestion Protocol for ${proj} - Vectorized Document Content.`;

    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    knowledgePage = new KnowledgePage(page);

    await loginPage.goto('https://localhost:5172/login');
    await loginPage.signInWithRetry(
      TEST_USER.email,
      TEST_USER.password,
      /https:\/\/localhost:5172\/(home|knowledge)/,
    );
    await expect(page.getByRole('banner')).toBeVisible({
      timeout: 15_000,
    });
    await knowledgePage.goto();
  });

  test.afterAll(async () => {
    // Best-effort cleanup for this worker's document if still present
    try {
      if (page && !page.isClosed()) {
        const row = knowledgePage.getRowByTitle(docTitle);
        if (await row.isVisible()) {
          await knowledgePage.deleteDocument(docTitle);
        }
      }
    } catch {}
    await page?.close();
  });

  // HK-CRUD-01 [NEW] (R-01)
  test('HK-CRUD-01: should disable upload button when no file is selected', async () => {
    await knowledgePage.goto();

    // Verify upload panel and form are visible
    await expect(knowledgePage.uploadPanel).toBeVisible();
    await expect(knowledgePage.uploadForm).toBeVisible();

    // No file selected -> upload button disabled
    await expect(knowledgePage.uploadButton).toBeDisabled();
  });

  // HK-CRUD-02 [NEW] (R-02)
  test('HK-CRUD-02: should require at least one role when Specific Roles is selected', async () => {
    await knowledgePage.goto();

    // Stage file
    await knowledgePage.stageFile(fileName, docContent);

    // Switch to Specific Roles without selecting any role
    await knowledgePage.rolesAudienceBtn.click();
    await expect(knowledgePage.rolesAudienceBtn).toHaveClass(/selected/);

    // Error hint should appear and upload button should be disabled
    await expect(knowledgePage.fieldErrorHint).toBeVisible();
    await expect(knowledgePage.fieldErrorHint).toContainText('Please choose at least one role');
    await expect(knowledgePage.uploadButton).toBeDisabled();
  });

  // HK-CRUD-03 [NEW] (R-03)
  test('HK-CRUD-03: should enable upload button when role checkbox is checked', async () => {
    // Select Developers role
    const devCheckbox = knowledgePage.rolesChecklistCard
      .locator('.role-checkbox-item', { hasText: 'Developers' })
      .locator('input[type="checkbox"]');
    await devCheckbox.check();
    await expect(devCheckbox).toBeChecked();

    // Error hint should disappear and upload button should become enabled
    await expect(knowledgePage.fieldErrorHint).toBeHidden();
    await expect(knowledgePage.uploadButton).toBeEnabled();

    // Switch back to Everyone for primary ingestion test
    await knowledgePage.selectAudienceEveryone();
    await expect(knowledgePage.uploadButton).toBeEnabled();
  });

  // HK-CRUD-04 [NEW] (R-04)
  test('HK-CRUD-04: should upload and ingest new document with Everyone audience', async () => {
    // Fill custom document title
    await knowledgePage.setTitle(docTitle);

    // Submit upload
    await knowledgePage.submitUpload();
  });

  // HK-CRUD-05 [NEW] (R-05)
  test('HK-CRUD-05: should display success notification and reset form upon ingestion', async () => {
    // Verify success banner appears
    await expect(knowledgePage.formNotice).toBeVisible({ timeout: 15_000 });
    await expect(knowledgePage.formNotice).toContainText(
      /was added\. Athenna can answer from it now/i,
    );

    // Title input should be cleared
    await expect(knowledgePage.titleInput).toHaveValue('');
  });

  // HK-CRUD-06 [NEW] (R-06)
  test('HK-CRUD-06: should render newly ingested document in table with badges', async () => {
    // Locate the newly created document row
    const row = knowledgePage.getRowByTitle(docTitle);
    await expect(row).toBeVisible({ timeout: 10_000 });

    // Verify title block
    await expect(row.locator('.doc-name')).toHaveText(docTitle);

    // Verify audience pill
    await expect(row.locator('.audience-pill-tag')).toHaveText('Everyone');

    // Verify version tag
    await expect(row.locator('.version-tag')).toHaveText('v1');
  });

  // HK-CRUD-07 [NEW] (R-07)
  test('HK-CRUD-07: should reflect incremented document count in header metric', async () => {
    // Verify count-number is greater than 0
    await expect(knowledgePage.docCounter).toBeVisible();
    const countText = await knowledgePage.docCounter.innerText();
    const count = parseInt(countText.trim(), 10);
    expect(count).toBeGreaterThanOrEqual(1);
  });

  // HK-CRUD-08 [NEW] (R-08)
  test('HK-CRUD-08: should open preview modal on row click and render extracted text', async () => {
    // Open preview by clicking the document row
    await knowledgePage.openPreview(docTitle);

    // Verify preview modal elements
    await expect(knowledgePage.previewModal).toBeVisible();
    await expect(knowledgePage.previewPre).toContainText(docContent);
  });

  // HK-CRUD-09 [NEW] (R-09)
  test('HK-CRUD-09: should dismiss document preview modal via Done Reading button', async () => {
    // Click Done Reading
    await knowledgePage.closePreview();

    // Verify modal is dismissed
    await expect(knowledgePage.previewModal).toBeHidden();
  });

  // HK-CRUD-10 [NEW] (R-10)
  test('HK-CRUD-10: should delete document via inline confirmation and purge from table', async () => {
    // Delete the document
    await knowledgePage.deleteDocument(docTitle);

    // Verify removal notice appears
    await expect(knowledgePage.formNotice).toBeVisible({ timeout: 10_000 });
    await expect(knowledgePage.formNotice).toContainText(/was removed/i);

    // Verify row is no longer in table
    const row = knowledgePage.getRowByTitle(docTitle);
    await expect(row).toBeHidden({ timeout: 10_000 });
  });
});
