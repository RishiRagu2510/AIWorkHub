---
plan_id: launchpad-plan-crud
title: LaunchPad Plan Management & Access Request Lifecycle Test Plan
version: 1.0
status: Approved
created: 2026-11-19
source: [url:https://localhost:5171/plan, file:frontend/src/pages/LandingPlan.jsx, file:frontend/src/pages/PlanPage.jsx, file:frontend/src/components/CancelPlan.jsx, file:frontend/src/components/RequestAccess.jsx, file:frontend/src/features/auth/planCatalog.js]
target_env: local
ui_verified: 2026-11-19 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-19
approved_hash: 9f89c6821eed7c32
---

# LaunchPad Plan Management & Access Request Lifecycle Test Plan

## 1. Overview
- **Objective:** Verify end-to-end interactive mutation workflows for company subscription plan management on LaunchPad (`https://localhost:5171/plan`) using dedicated test user credentials (`qa.test@griddynamics.com`), testing plan metrics, capability breakdown, cancellation confirmation prompt, cancellation request submission, pending review badges, and request withdrawal.
- **In scope:**
  - Header & hero banner verification (Enterprise Workspace Plan, capability metrics).
  - Portals catalog inspection (GridHub, Persona, Chronos, AIWorkHub) with "Included" badges.
  - Tools catalog inspection (Athenna AI chat) with "Included" badge.
  - Interactive "Cancel plan" trigger and confirmation prompt ("Ask to cancel...").
  - Cancellation confirmation dismissal via "Keep {name}".
  - Submitting cancellation request to platform operators ("Send cancellation request").
  - Real-time UI transition to "Cancellation under review" status badge.
  - Dynamic Requests list rendering with pulsing status indicator and review badge.
  - Pending Requests metric pill increment in hero banner.
  - Withdrawing request via "Withdraw request" button (`useCancelAccessRequestMutation`).
  - Restoration verification ensuring request is removed and "Cancel plan" button is restored.
- **Out of scope:**
  - Backend Stripe payment gateway webhooks.
  - Super-admin platform billing overrides.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded with `ADMIN` role.
  - Backend services running: ApiGateway (`5204`), IdentityTenant (`5233`), PostgreSQL (`5432`).
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Plan page renders header with "Your plan" and Enterprise Workspace Plan hero banner | Functional | PlanPage.jsx | PageHeader & Hero |
| R-02 | Hero metrics display Enabled Capabilities ratio and dynamic Pending Requests pill | Functional | PlanPage.jsx | `.plan-hero-metrics` |
| R-03 | Portals section lists all licensed portals with Included status badges and admin actions | Functional | PlanPage.jsx | `.plan-section` Portals |
| R-04 | Tools section lists enabled add-ons with Included status badges and admin actions | Functional | PlanPage.jsx | `.plan-section` Tools |
| R-05 | Clicking "Cancel plan" reveals inline confirmation prompt with Confirm and Keep buttons | Functional | CancelPlan.jsx | Interactive confirmation |
| R-06 | Clicking "Keep {name}" dismisses confirmation prompt without sending request | Functional | CancelPlan.jsx | Prompt dismissal |
| R-07 | Clicking "Send cancellation request" submits mutation and transitions item to pending state | Functional | CancelPlan.jsx | `useRequestAccessMutation` |
| R-08 | Pending item displays "Cancellation under review" status indicator in catalog row | Functional | CancelPlan.jsx | `.plan-cancel-pending` |
| R-09 | Requests section renders submitted request with review badge and Withdraw button | Functional | PlanPage.jsx | `.plan-requests-list` |
| R-10 | Clicking "Withdraw request" deletes request and restores item's original "Cancel plan" button | Functional | PlanPage.jsx | `useCancelAccessRequestMutation` |

## 3. Scenarios

### LP-PLAN-01: Header & Plan Overview — Render title and Enterprise Plan banner
- **Goal:** Verify LaunchPad Plan page header, title, and hero subscription banner.
- **Preconditions:** Authenticated as test user on `https://localhost:5171/plan`.
- **Steps:**
  1. Navigate to `https://localhost:5171/plan`.
  2. Inspect page heading `Your plan` (`h1`).
  3. Inspect hero banner title `Active Features & Services` and `Enterprise Workspace Plan` badge.
- **Expected Result:** Header and hero banner render correctly.

### LP-PLAN-02: Enabled Capabilities Metric — Verify subscription metric pill
- **Goal:** Verify Enabled Capabilities metric displays active count.
- **Preconditions:** On `https://localhost:5171/plan`.
- **Steps:**
  1. Locate `.plan-metric-pill` in hero banner.
  2. Verify metric number format: `5 / 5` (4 portals + 1 tool).
  3. Verify metric label: `Enabled Capabilities`.
- **Expected Result:** Enabled Capabilities ratio is accurately rendered.

### LP-PLAN-03: Portals Section — Verify all 4 primary portals with Included badges
- **Goal:** Ensure all core portals are listed as active.
- **Preconditions:** On `https://localhost:5171/plan`.
- **Steps:**
  1. Inspect section `Portals` (`#plan-portals`).
  2. Verify items: `GridHub`, `Persona`, `Chronos`, and `AIWorkHub`.
  3. Verify each item has `.plan-status-on` badge with text `Included`.
- **Expected Result:** All 4 portals display active Included badges.

### LP-PLAN-04: Tools Section — Verify Athenna AI chat with Included badge
- **Goal:** Ensure workspace tools section renders active assistants.
- **Preconditions:** On `https://localhost:5171/plan`.
- **Steps:**
  1. Inspect section `Tools` (`#plan-tools`).
  2. Verify item: `Athenna AI chat`.
  3. Verify item has `.plan-status-on` badge with text `Included`.
- **Expected Result:** Tools section accurately lists Athenna AI chat as included.

### LP-PLAN-05: Cancellation Prompt — Open and dismiss cancellation confirmation
- **Goal:** Verify inline confirmation dialog opens and safely dismisses without mutating.
- **Preconditions:** On `https://localhost:5171/plan`.
- **Steps:**
  1. Locate target item row based on browser worker assignment.
  2. Click "Cancel plan" button (`button.plan-cancel-button`).
  3. Verify confirmation prompt appears: `Ask to cancel {name}?`.
  4. Click "Keep {name}" button (`button.plan-cancel-keep`).
  5. Verify confirmation prompt dismisses and "Cancel plan" button reappears.
- **Expected Result:** Confirmation prompt opens and cleanly dismisses without modifying plan state.

### LP-PLAN-06: Cancellation Submission — Submit request to platform operators
- **Goal:** Submit cancellation request and verify API mutation succeeds.
- **Preconditions:** Target item row located.
- **Steps:**
  1. Click "Cancel plan" button on target item.
  2. Click "Send cancellation request" button (`button.plan-cancel-send`).
  3. Await mutation completion and UI refresh.
- **Expected Result:** Request sent successfully; item updates action cell.

### LP-PLAN-07: Pending State — Verify Cancellation Under Review status in row
- **Goal:** Validate item row reflects pending cancellation status.
- **Preconditions:** Cancellation request submitted for target item.
- **Steps:**
  1. Locate target item row in catalog.
  2. Verify action cell now renders `.plan-cancel-pending` with text `Cancellation under review`.
  3. Verify "Cancel plan" button is no longer visible on this item.
- **Expected Result:** Row displays pending review status chip.

### LP-PLAN-08: Requests Section — Render submitted request in Requests list
- **Goal:** Verify request tracking section displays submitted request.
- **Preconditions:** Cancellation request pending.
- **Steps:**
  1. Locate section `Requests from your company` (`#plan-requests`).
  2. Locate request item matching target item name.
  3. Verify request title: `Cancel {name}`.
  4. Verify badge: `Cancellation under review`.
  5. Verify hero banner displays `.pending-metric` with count `>= 1`.
- **Expected Result:** Requests list tracks the pending cancellation item.

### LP-PLAN-09: Request Withdrawal — Withdraw pending cancellation request
- **Goal:** Remove pending request via Withdraw button.
- **Preconditions:** Pending request item visible in Requests list.
- **Steps:**
  1. In request item, click "Withdraw request" button (`button.plan-request-cancel`).
  2. Await mutation completion and list refresh.
- **Expected Result:** Request withdrawal triggers and request item is removed.

### LP-PLAN-10: Restoration Verification — Ensure original Cancel Plan state restores
- **Goal:** Confirm plan returns to original unrequested baseline.
- **Preconditions:** Request withdrawn.
- **Steps:**
  1. Locate target item in catalog.
  2. Verify target item restores "Cancel plan" button (`button.plan-cancel-button`).
  3. Verify `.plan-cancel-pending` is no longer visible on target item.
  4. If no other requests remain, verify Requests section renders `.plan-empty-card`.
- **Expected Result:** Full restoration to original state with zero orphaned requests.

## 4. Requirements Traceability Matrix (RTM)
| Scenario ID | Requirement Ref | Test File | Target URL | Method |
|---|---|---|---|---|
| LP-PLAN-01 | R-01 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-02 | R-02 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-03 | R-03 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-04 | R-04 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-05 | R-05, R-06 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-06 | R-07 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-07 | R-08 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-08 | R-02, R-09 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-09 | R-10 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
| LP-PLAN-10 | R-08, R-10 | tests/e2e/launchpad-plan-crud.spec.ts | https://localhost:5171/plan | Automated E2E |
