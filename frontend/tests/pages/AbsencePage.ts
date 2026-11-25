import { type Page, type Locator, expect } from '@playwright/test';

export type AbsenceKind = 'CASUAL' | 'SICK' | 'EARNED' | 'UNPAID' | 'WFH';

const KIND_NAMES: Record<AbsenceKind, string> = {
  CASUAL: 'Floating holidays',
  SICK: 'Sick leave',
  EARNED: 'Earned leave',
  UNPAID: 'Unpaid leave',
  WFH: 'Work from home',
};

export class AbsencePage {
  readonly page: Page;
  readonly baseUrl: string;

  // Header and layout
  readonly heading: Locator;
  readonly yearSelectTrigger: Locator;
  readonly approverChip: Locator;

  // Balance Cards
  readonly balanceCards: Locator;
  readonly casualCard: Locator;
  readonly sickCard: Locator;
  readonly earnedCard: Locator;

  // Request Form
  readonly requestCard: Locator;
  readonly requestForm: Locator;
  readonly typeSelectTrigger: Locator;
  readonly typePopover: Locator;
  readonly startDateInput: Locator;
  readonly endDateInput: Locator;
  readonly reasonTextarea: Locator;
  readonly submitButton: Locator;
  readonly exceededAlert: Locator;
  readonly durationPill: Locator;
  readonly successNotice: Locator;

  // Requests Table
  readonly tableCard: Locator;
  readonly myRequestsTab: Locator;
  readonly teamApprovalsTab: Locator;
  readonly refreshButton: Locator;
  readonly requestsTable: Locator;
  readonly requestRows: Locator;
  readonly emptyTableMessage: Locator;

  constructor(page: Page, baseUrl: string = 'https://localhost:5174') {
    this.page = page;
    this.baseUrl = baseUrl;

    this.heading = page.getByRole('heading', { name: /Leave & WFH/i });
    this.yearSelectTrigger = page.locator('.absence-year-trigger');
    this.approverChip = page.locator('.absence-approver-chip');

    this.balanceCards = page.locator('.absence-balance-card');
    this.casualCard = page.locator('.absence-card-casual');
    this.sickCard = page.locator('.absence-card-sick');
    this.earnedCard = page.locator('.absence-card-earned');

    this.requestCard = page.locator('.absence-request-card');
    this.requestForm = page.locator('form.absence-request-form');
    this.typeSelectTrigger = page.locator('.absence-select-trigger');
    this.typePopover = page.locator('.absence-select-popover');
    this.startDateInput = page.locator('input.absence-hidden-native-date[aria-label="Start date"]');
    this.endDateInput = page.locator('input.absence-hidden-native-date[aria-label="End date"]');
    this.reasonTextarea = page.locator('form.absence-request-form textarea');
    this.submitButton = page.locator('form.absence-request-form button.primary');
    this.exceededAlert = page.locator('.absence-alert-warning[role="alert"]');
    this.durationPill = page.locator('.absence-duration-bar .absence-duration-pill');
    this.successNotice = page.locator('.absence-alert-success[role="status"]');

    this.tableCard = page.locator('.absence-table-card');
    this.myRequestsTab = page.locator('button.absence-tab-btn:has-text("My requests")');
    this.teamApprovalsTab = page.locator('button.absence-tab-btn:has-text("Team approvals")');
    this.refreshButton = page.locator('button.absence-refresh-btn');
    this.requestsTable = page.locator('.absence-table');
    this.requestRows = page.locator('.absence-table tbody tr:not(.absence-empty-row)');
    this.emptyTableMessage = page.locator('.absence-table-empty');
  }

  async goto() {
    if (!this.page.url().includes('/employees/leave')) {
      await this.page.goto(`${this.baseUrl}/employees/leave`);
    }
    await this.page.waitForLoadState('domcontentloaded');
    await expect(this.heading).toBeVisible({ timeout: 20_000 });
  }

  async selectType(kind: AbsenceKind) {
    const label = KIND_NAMES[kind];
    await this.typeSelectTrigger.click();
    await expect(this.typePopover).toBeVisible();
    await this.typePopover.locator('.absence-select-option', { hasText: label }).click();
    await expect(this.typePopover).toBeHidden();
  }

  async setDate(fieldLabel: 'Start date' | 'End date', dateStr: string) {
    await this.page.evaluate(
      ({ label, value }) => {
        const input = document.querySelector(`input[aria-label="${label}"]`) as HTMLInputElement;
        if (input) {
          const nativeSetter = Object.getOwnPropertyDescriptor(
            window.HTMLInputElement.prototype,
            'value',
          )?.set;
          nativeSetter?.call(input, value);
          input.dispatchEvent(new Event('input', { bubbles: true }));
          input.dispatchEvent(new Event('change', { bubbles: true }));
        }
      },
      { label: fieldLabel, value: dateStr },
    );
  }

  async fillForm(options: {
    kind?: AbsenceKind;
    startDate?: string;
    endDate?: string;
    reason?: string;
  }) {
    if (options.kind) {
      await this.selectType(options.kind);
    }
    if (options.startDate) {
      await this.setDate('Start date', options.startDate);
    }
    if (options.endDate) {
      await this.setDate('End date', options.endDate);
    }
    if (options.reason !== undefined) {
      await this.reasonTextarea.fill(options.reason);
    }
  }

  async submit() {
    await this.submitButton.click();
  }

  getRequestRowByReason(reasonText: string): Locator {
    return this.requestRows.filter({ hasText: reasonText });
  }

  async cancelRequestByReason(reasonText: string) {
    const row = this.getRequestRowByReason(reasonText);
    await expect(row).toBeVisible();
    const cancelBtn = row.locator('button.absence-btn-cancel');
    await expect(cancelBtn).toBeVisible();
    await cancelBtn.click();
  }
}
