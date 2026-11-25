---
plan_id: hub-athenna-chat
title: Central Hub Athenna AI Copilot Workspace Chat Test Plan
version: 1.0
status: Approved
created: 2026-11-19
source: [url:https://localhost:5172/home, file:frontend/hub/src/pages/HubHome.jsx, file:frontend/src/components/AthennaWorkspaceChat.jsx, file:frontend/src/components/ModelPicker.jsx, file:frontend/src/components/athennaStarterPrompts.js]
target_env: local
ui_verified: 2026-11-19 via Playwright MCP on local
approved_by: "rragupathi"
approved_on: 2026-11-19
approved_hash: d6a1f15d052bb1dd
---

# Central Hub Athenna AI Copilot Workspace Chat Test Plan

## 1. Overview
- **Objective:** Verify end-to-end interactive conversational workflows and intelligence controls for the Athenna Autonomous Workspace Copilot on Central Hub (`https://localhost:5172/home`) using dedicated test user credentials (`qa.test@griddynamics.com`), testing agent readiness, metric cards, starter prompt triggers, intelligence model selection, and multi-turn conversational streaming.
- **In scope:**
  - Agent header greeting bar (`Welcome back, ...`, `Online & Ready`, and `RBAC: ADMIN` badge).
  - Multi-agent metric cards in left column (Daily Updated Issues and Daily Work Log).
  - Initial stage rendering glowing AI orb, `AI is ready` title, and subtitle.
  - Starter prompt cards grid covering GridHub, Chronos, and Persona workflows.
  - Intelligence model selector popover opening, model options inspection (`Auto`, `Gemini Pro`), and switching.
  - Prompt dock input validation and send button state transition (idle to active).
  - Executing prompt via starter action card and verifying transition to chat stream (`.agentic-chat-stream`).
  - Rendering user message bubble and assistant response with Athenna AI avatar.
  - Submitting custom follow-up conversational prompt and verifying assistant streaming answer.
- **Out of scope:**
  - Third-party model API quota billing dashboards.
  - Audio voice transcription input.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` seeded with `ADMIN` role.
  - Backend services running: ApiGateway (`5204`), AIAgent.Api (`5250`), IdentityTenant (`5233`), PostgreSQL (`5432`).
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Greeting bar renders personalized user welcome, Online & Ready status, and RBAC pill | Functional | AthennaWorkspaceChat.jsx | `.hub-header-greeting-bar` |
| R-02 | Left column displays synchronized Daily Updated Issues and Chronos Daily Work Log cards | Functional | AthennaWorkspaceChat.jsx | `.agentic-cards-column` |
| R-03 | Center stage displays glowing AI orb and "AI is ready" heading when no messages are present | Functional | AthennaWorkspaceChat.jsx | `.ai-ready-stage-box` |
| R-04 | Starter prompt cards grid renders actionable cross-portal query templates | Functional | athennaStarterPrompts.js | `.ai-starter-cards-grid` |
| R-05 | Model dropdown popover opens and displays available intelligence engines | Functional | ModelPicker.jsx | `.intelligence-model-dropdown-wrap` |
| R-06 | Selecting a model updates active intelligence engine and trigger label | Functional | ModelPicker.jsx | `AVAILABLE_MODELS` |
| R-07 | Prompt input enables send button upon typing non-empty query | Functional | AthennaWorkspaceChat.jsx | `.agentic-prompt-dock` |
| R-08 | Clicking a starter prompt card submits query and transitions stage to live stream | Functional | AthennaWorkspaceChat.jsx | `.agentic-chat-stream` |
| R-09 | Stream bubble row displays user query alongside Athenna assistant response bubble | Functional | AthennaWorkspaceChat.jsx | `.stream-bubble-row` |
| R-10 | Submitting custom follow-up query appends new exchange to active conversation | Functional | AthennaWorkspaceChat.jsx | Multi-turn chat |

## 3. Scenarios

### HK-CHAT-01: Copilot Greeting & Agent Readiness — Verify header status & RBAC
- **Goal:** Verify personalized welcome greeting, agent status, and active role-based access control badge.
- **Preconditions:** Authenticated as test user on `https://localhost:5172/home`.
- **Steps:**
  1. Inspect `.hub-header-greeting-bar .greeting-main`.
  2. Inspect `.agent-status-pill-live` showing `Online & Ready`.
  3. Inspect `.agent-rbac-pill` showing `RBAC: ADMIN`.
- **Expected Result:** Header greeting and agent metadata render with correct user and role values.

### HK-CHAT-02: Multi-Agent Metric Cards — Verify left column telemetry cards
- **Goal:** Verify Daily Updated Issues and Daily Work Log metric cards render correctly.
- **Preconditions:** On `https://localhost:5172/home`.
- **Steps:**
  1. Locate `.agentic-card.daily-issues-card`.
  2. Verify heading: `Daily Updated Issues` and subtitle `Assigned to Me • GridHub`.
  3. Locate `.agentic-card.metric-card`.
  4. Verify heading: `Daily Work Log` and subtitle `Chronos Hours Logged Today`.
- **Expected Result:** Both telemetry cards display active portal integrations.

### HK-CHAT-03: AI Ready Stage & Glowing Orb — Verify initial idle state
- **Goal:** Verify center stage displays the glowing orb and invitation heading.
- **Preconditions:** Fresh chat session on `https://localhost:5172/home`.
- **Steps:**
  1. Locate `.ai-ready-stage-box`.
  2. Inspect `.ai-glowing-orb-wrapper`.
  3. Verify heading `h1.ai-ready-title` with text `AI is ready`.
  4. Verify subtitle `.ai-ready-subtitle` mentioning `GridHub, Persona, and Chronos`.
- **Expected Result:** Center stage displays the branded glowing AI orb and readiness prompt.

### HK-CHAT-04: Starter Action Cards Grid — Verify cross-portal templates
- **Goal:** Ensure starter prompt template cards render for quick queries.
- **Preconditions:** On `https://localhost:5172/home`.
- **Steps:**
  1. Locate `.ai-starter-cards-grid`.
  2. Verify card for GridHub: `What to Work on Today`.
  3. Verify card for Chronos: `Timesheet & Worklogs`.
  4. Verify card for Persona: `Leave & Absences`.
- **Expected Result:** Starter template cards render with their system tags and action arrows.

### HK-CHAT-05: Model Picker Popover — Open popover and inspect engines
- **Goal:** Verify intelligence engine dropdown opens and lists model options.
- **Preconditions:** On `https://localhost:5172/home`.
- **Steps:**
  1. Locate and click `button.intelligence-selector-btn`.
  2. Verify dropdown menu `.intelligence-model-popover[role="menu"]` is visible.
  3. Verify menu contains `Auto` and `Gemini Pro` model options.
- **Expected Result:** Model popover renders available intelligence models.

### HK-CHAT-06: Model Selection Switch — Switch model to Gemini Pro and back
- **Goal:** Change selected intelligence model and verify UI reflection.
- **Preconditions:** Model picker menu open.
- **Steps:**
  1. Click `Gemini Pro` menu item.
  2. Verify menu closes and trigger displays `Gemini Pro`.
  3. Re-open model picker and click `Auto`.
  4. Verify menu closes and trigger restores `Auto`.
- **Expected Result:** Model selection toggles smoothly and updates the trigger badge.

### HK-CHAT-07: Prompt Input State — Validate idle and active send states
- **Goal:** Verify prompt dock input validation and send button state.
- **Preconditions:** On `https://localhost:5172/home`.
- **Steps:**
  1. Inspect `button.agentic-send-btn` while input is empty (disabled/idle).
  2. Type `Hello Athenna` into `input.agentic-prompt-input`.
  3. Verify `button.agentic-send-btn` becomes enabled and active (`.active`).
  4. Clear input and verify button returns to idle/disabled.
- **Expected Result:** Send button state dynamically tracks input presence.

### HK-CHAT-08: Starter Card Trigger — Execute prompt via template card
- **Goal:** Click starter card to initiate a live conversation.
- **Preconditions:** On `https://localhost:5172/home` with initial stage visible.
- **Steps:**
  1. Click starter card `What to Work on Today`.
  2. Verify initial `.ai-ready-stage-box` is replaced by `.agentic-chat-stream`.
  3. Verify user message bubble contains `What should I work on today?`.
- **Expected Result:** Starter prompt launches conversation and renders message stream.

### HK-CHAT-09: Live Assistant Bubble — Verify response generation & avatar
- **Goal:** Ensure Athenna AI generates assistant response bubble in stream.
- **Preconditions:** Conversation stream active.
- **Steps:**
  1. Locate `.stream-bubble-row.assistant`.
  2. Verify assistant avatar `.stream-avatar.assistant-avatar` is visible.
  3. Verify assistant bubble `.stream-bubble.assistant` renders response text.
- **Expected Result:** Assistant response stream renders formatted content and avatar.

### HK-CHAT-10: Multi-Turn Conversation — Submit custom follow-up query
- **Goal:** Send follow-up prompt and verify second conversational turn.
- **Preconditions:** First turn completed in `.agentic-chat-stream`.
- **Steps:**
  1. Type `What are my current permissions in AIWorkHub?` into `input.agentic-prompt-input`.
  2. Click send button `button.agentic-send-btn`.
  3. Verify second user bubble is appended to the stream.
  4. Verify second assistant response bubble is appended with response content.
- **Expected Result:** Multi-turn conversation persists seamlessly in the stream.

## 4. Requirements Traceability Matrix (RTM)
| Scenario ID | Requirement Ref | Test File | Target URL | Method |
|---|---|---|---|---|
| HK-CHAT-01 | R-01 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-02 | R-02 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-03 | R-03 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-04 | R-04 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-05 | R-05 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-06 | R-06 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-07 | R-07 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-08 | R-08 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-09 | R-09 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
| HK-CHAT-10 | R-10 | tests/e2e/hub-athenna-chat.spec.ts | https://localhost:5172/home | Automated E2E |
