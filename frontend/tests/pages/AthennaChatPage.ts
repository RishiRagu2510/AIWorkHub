import { type Page, type Locator, expect } from '@playwright/test';

export class AthennaChatPage {
  readonly page: Page;
  readonly baseUrl: string;

  // Header Greeting
  readonly greetingBar: Locator;
  readonly greetingMain: Locator;
  readonly agentStatusPill: Locator;
  readonly agentRbacPill: Locator;

  // Left Column Metric Cards
  readonly dailyIssuesCard: Locator;
  readonly dailyWorkLogCard: Locator;

  // Center Stage (Initial Idle)
  readonly readyStage: Locator;
  readonly glowingOrb: Locator;
  readonly readyTitle: Locator;
  readonly readySubtitle: Locator;
  readonly starterGrid: Locator;
  readonly starterCards: Locator;

  // Chat Stream (Active Conversation)
  readonly chatStream: Locator;
  readonly userBubbles: Locator;
  readonly assistantBubbles: Locator;
  readonly assistantAvatars: Locator;
  readonly assistantLoading: Locator;

  // Prompt Dock
  readonly promptDock: Locator;
  readonly promptInput: Locator;
  readonly sendBtn: Locator;
  readonly modelSelectorBtn: Locator;
  readonly modelPopover: Locator;

  constructor(page: Page, baseUrl: string = 'https://localhost:5172') {
    this.page = page;
    this.baseUrl = baseUrl;

    this.greetingBar = page.locator('.hub-header-greeting-bar');
    this.greetingMain = page.locator('.greeting-main');
    this.agentStatusPill = page.locator('.agent-status-pill-live');
    this.agentRbacPill = page.locator('.agent-rbac-pill');

    this.dailyIssuesCard = page.locator('.agentic-card.daily-issues-card');
    this.dailyWorkLogCard = page
      .locator('.agentic-card.metric-card')
      .filter({ hasText: 'Daily Work Log' });

    this.readyStage = page.locator('.ai-ready-stage-box');
    this.glowingOrb = page.locator('.ai-glowing-orb-wrapper');
    this.readyTitle = page.locator('.ai-ready-title');
    this.readySubtitle = page.locator('.ai-ready-subtitle');
    this.starterGrid = page.locator('.ai-starter-cards-grid');
    this.starterCards = page.locator('.ai-starter-card');

    this.chatStream = page.locator('.agentic-chat-stream');
    this.userBubbles = page.locator('.stream-bubble-row.user');
    this.assistantBubbles = page
      .locator('.stream-bubble-row.assistant')
      .filter({ hasNot: page.locator('.loading') });
    this.assistantAvatars = page.locator('.stream-avatar.assistant-avatar');
    this.assistantLoading = page.locator('.stream-bubble.assistant.loading');

    this.promptDock = page.locator('.agentic-prompt-dock');
    this.promptInput = page.locator('input.agentic-prompt-input');
    this.sendBtn = page.locator('button.agentic-send-btn');
    this.modelSelectorBtn = page.locator('button.intelligence-selector-btn');
    this.modelPopover = page.locator('.intelligence-model-popover[role="menu"]');
  }

  async goto() {
    if (!this.page.url().includes('/home')) {
      await this.page.goto(`${this.baseUrl}/home`);
    }
    await this.page.waitForLoadState('domcontentloaded');
    await expect(this.greetingBar).toBeVisible({ timeout: 20_000 });
  }

  getStarterCard(title: string): Locator {
    return this.starterCards.filter({
      has: this.page.locator(`.starter-card-title:has-text("${title}")`),
    });
  }

  getModelOption(name: string): Locator {
    return this.modelPopover
      .locator('button[role="menuitemradio"]')
      .filter({ has: this.page.locator(`.model-menu-name:has-text("${name}")`) });
  }

  async openModelPicker() {
    if (!(await this.modelPopover.isVisible())) {
      await this.modelSelectorBtn.click();
      await expect(this.modelPopover).toBeVisible();
    }
  }

  async selectModel(name: string) {
    await this.openModelPicker();
    const option = this.getModelOption(name);
    await option.click();
    await expect(this.modelPopover).toBeHidden();
  }

  async enterPrompt(text: string) {
    await this.promptInput.fill(text);
  }

  async sendPrompt(text?: string) {
    if (text) {
      await this.enterPrompt(text);
    }
    await this.sendBtn.click();
  }

  async waitForAssistantReply(count: number = 1, timeout: number = 90_000) {
    // Wait until expected count of completed assistant replies are rendered
    await expect(this.assistantBubbles).toHaveCount(count, { timeout });
    // And verify the temporary loading placeholder has disappeared
    await expect(this.assistantLoading).toBeHidden({ timeout: 15_000 });
  }
}
