/**
 * Test Plan: test-plans/hub-athenna-chat.plan.md
 * Feature: Central Hub Athenna AI Copilot Workspace Chat
 * Target environment: local (https://localhost:5172/home)
 */
import { test, expect, Page } from '@playwright/test';
import { LoginPage, AthennaChatPage } from '../pages';

const TEST_USER = {
  email: 'qa.test@griddynamics.com',
  password: 'TestPassword123!',
};

test.describe('Central Hub Athenna AI Copilot Workspace Chat', () => {
  test.describe.configure({ mode: 'serial', retries: 2 });
  test.use({ baseURL: 'https://localhost:5172' });

  let page: Page;
  let loginPage: LoginPage;
  let chatPage: AthennaChatPage;

  test.beforeAll(async ({ browser }) => {
    test.setTimeout(90_000);
    page = await browser.newPage({ ignoreHTTPSErrors: true });
    loginPage = new LoginPage(page);
    chatPage = new AthennaChatPage(page);

    await loginPage.goto('https://localhost:5172/login');
    await loginPage.signInWithRetry(
      TEST_USER.email,
      TEST_USER.password,
      /https:\/\/localhost:5172\/home/,
    );
    await expect(page.getByRole('banner')).toBeVisible({ timeout: 25_000 });
    await chatPage.goto();
  });

  test.afterAll(async () => {
    await page?.close();
  });

  // HK-CHAT-01 [NEW] (R-01)
  test('HK-CHAT-01: should render personalized welcome, Online & Ready status, and RBAC pill', async () => {
    await expect(chatPage.greetingMain).toContainText(/Welcome back/i);
    await expect(chatPage.agentStatusPill).toBeVisible();
    await expect(chatPage.agentStatusPill).toContainText(/Online & Ready/i);
    await expect(chatPage.agentRbacPill).toBeVisible();
    await expect(chatPage.agentRbacPill).toContainText(/RBAC:\s*ADMIN/i);
  });

  // HK-CHAT-02 [NEW] (R-02)
  test('HK-CHAT-02: should render Daily Updated Issues and Daily Work Log metric cards', async () => {
    await expect(chatPage.dailyIssuesCard).toBeVisible();
    await expect(chatPage.dailyIssuesCard.locator('.agent-name')).toContainText(
      /Daily Updated Issues/i,
    );
    await expect(chatPage.dailyIssuesCard.locator('.agent-dept')).toContainText(/GridHub/i);

    await expect(chatPage.dailyWorkLogCard).toBeVisible();
    await expect(chatPage.dailyWorkLogCard.locator('.agent-name')).toContainText(/Daily Work Log/i);
    await expect(chatPage.dailyWorkLogCard.locator('.agent-dept')).toContainText(/Chronos/i);
  });

  // HK-CHAT-03 [NEW] (R-03)
  test('HK-CHAT-03: should display glowing AI orb, AI is ready heading, and multi-portal subtitle', async () => {
    await expect(chatPage.readyStage).toBeVisible();
    await expect(chatPage.glowingOrb).toBeVisible();
    await expect(chatPage.readyTitle).toHaveText(/AI is ready/i);
    await expect(chatPage.readySubtitle).toContainText(/GridHub, Persona, and Chronos/i);
  });

  // HK-CHAT-04 [NEW] (R-04)
  test('HK-CHAT-04: should render cross-portal starter prompt cards grid', async () => {
    await expect(chatPage.starterGrid).toBeVisible();
    const sprintCard = chatPage.getStarterCard('Sprint Health');
    await expect(sprintCard).toBeVisible();
    await expect(sprintCard.locator('.starter-card-tag')).toContainText(/GridHub/i);

    const issuesCard = chatPage.getStarterCard('Issues Report in Excel');
    await expect(issuesCard).toBeVisible();
    await expect(issuesCard.locator('.starter-card-tag')).toContainText(/GridHub/i);

    const leaveCard = chatPage.getStarterCard('Leave Requests to Decide');
    await expect(leaveCard).toBeVisible();
    await expect(leaveCard.locator('.starter-card-tag')).toContainText(/Persona/i);
  });

  // HK-CHAT-05 [NEW] (R-05)
  test('HK-CHAT-05: should open model popover and display available intelligence engines', async () => {
    await chatPage.openModelPicker();
    await expect(chatPage.modelPopover).toBeVisible();

    const modelOptions = chatPage.modelPopover.locator('button[role="menuitemradio"]');
    await expect(modelOptions.first()).toBeVisible();
    expect(await modelOptions.count()).toBeGreaterThanOrEqual(1);
  });

  // HK-CHAT-06 [NEW] (R-06)
  test('HK-CHAT-06: should switch model selection and update selector button badge', async () => {
    const unselectedOptions = chatPage.modelPopover.locator(
      'button[role="menuitemradio"]:not(.selected)',
    );
    if ((await unselectedOptions.count()) > 0) {
      const targetOption = unselectedOptions.first();
      const targetName = (await targetOption.locator('.model-menu-name').innerText()).trim();
      await targetOption.click();
      await expect(chatPage.modelPopover).toBeHidden();
      await expect(chatPage.modelSelectorBtn).toContainText(targetName);
    } else {
      await chatPage.modelSelectorBtn.click();
      await expect(chatPage.modelPopover).toBeHidden();
    }
  });

  // HK-CHAT-07 [NEW] (R-07)
  test('HK-CHAT-07: should validate prompt input and toggle send button active state', async () => {
    // Empty input: button should be idle / disabled
    await expect(chatPage.sendBtn).toHaveClass(/idle/);
    await expect(chatPage.sendBtn).toBeDisabled();

    // Type text: button becomes active and enabled
    await chatPage.enterPrompt('Hello Athenna');
    await expect(chatPage.sendBtn).toHaveClass(/active/);
    await expect(chatPage.sendBtn).toBeEnabled();

    // Clear text: button restores idle
    await chatPage.enterPrompt('');
    await expect(chatPage.sendBtn).toHaveClass(/idle/);
    await expect(chatPage.sendBtn).toBeDisabled();
  });

  // HK-CHAT-08 [NEW] (R-08)
  test('HK-CHAT-08: should trigger prompt via starter card and transition stage to chat stream', async () => {
    const sprintCard = chatPage.getStarterCard('Sprint Health');
    await sprintCard.click();

    // The ready stage box should transition to the live chat stream
    await expect(chatPage.readyStage).toBeHidden({ timeout: 10_000 });
    await expect(chatPage.chatStream).toBeVisible({ timeout: 10_000 });

    // Verify user bubble is rendered
    const firstUserBubble = chatPage.userBubbles.first();
    await expect(firstUserBubble).toBeVisible();
    await expect(firstUserBubble.locator('.user-plain-text')).toContainText(
      /How is the current sprint going\?/i,
    );
  });

  // HK-CHAT-09 [NEW] (R-09)
  test('HK-CHAT-09: should render assistant bubble with Athenna avatar and generated response', async () => {
    test.setTimeout(120_000);
    // Wait for Athenna to stream the response
    await chatPage.waitForAssistantReply(1, 90_000);

    const firstAssistantBubble = chatPage.assistantBubbles.first();
    await expect(firstAssistantBubble).toBeVisible();
    await expect(firstAssistantBubble.locator('.stream-avatar.assistant-avatar')).toBeVisible();

    const bubbleText = firstAssistantBubble.locator('.stream-bubble-text');
    await expect(bubbleText).toBeVisible();
    // Verify response content is present
    await expect(bubbleText).not.toBeEmpty();
  });

  // HK-CHAT-10 [NEW] (R-10)
  test('HK-CHAT-10: should submit follow-up query and append new turn to active stream', async () => {
    test.setTimeout(120_000);
    const followUpQuery = 'What are my current permissions in AIWorkHub?';
    await chatPage.sendPrompt(followUpQuery);

    // Verify second user bubble is appended
    await expect(chatPage.userBubbles).toHaveCount(2, { timeout: 10_000 });
    const secondUserBubble = chatPage.userBubbles.nth(1);
    await expect(secondUserBubble.locator('.user-plain-text')).toContainText(followUpQuery);

    // Verify second assistant response bubble is appended
    await chatPage.waitForAssistantReply(2, 90_000);
    await expect(chatPage.assistantBubbles).toHaveCount(2, { timeout: 10_000 });
    const secondAssistantBubble = chatPage.assistantBubbles.nth(1);
    await expect(secondAssistantBubble.locator('.stream-bubble-text')).not.toBeEmpty();
  });
});
