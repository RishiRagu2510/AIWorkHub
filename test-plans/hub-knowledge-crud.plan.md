---
plan_id: hub-knowledge-crud
title: Hub Portal — Knowledge Base Documents Mutation Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5172/knowledge, file:frontend/hub/src/pages/KnowledgePage.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 269656ed64dd8e6d
---

# Hub Portal — Knowledge Base Documents Mutation Test Plan

## 1. Overview
- **Objective:** Verify end-to-end knowledge base document ingestion, permission audience scoping, vectorization feedback, document preview inspection, and deletion workflows on AIWorkHub Central Hub (`https://localhost:5172/knowledge`) using dedicated test user credentials (`qa.test@griddynamics.com`), covering empty upload button disablement, role selection boundaries, multipart document upload, real-time ingestion feedback, table presentation, indexed document count metrics, modal preview verification, and inline document deletion.
- **In scope:**
  - Validation: Upload button disablement when no file is staged.
  - Audience restriction: Toggling "Specific Roles" with no role selected shows error hint and disables upload.
  - Audience configuration: Selecting permitted role checkbox enables submission.
  - Document ingestion: Staging text/markdown file with custom title and "Everyone" audience.
  - Ingestion feedback: Success notice banner and form reset upon submission.
  - Document table presentation: Row rendering with title, version tag, and audience pill.
  - Document counter metrics: Top-right indexed docs counter reflection.
  - Document content viewer: Opening preview modal and asserting extracted text.
  - Modal dismissal: Closing modal via "Done Reading" button.
  - Deletion lifecycle: Clicking delete icon to open inline confirmation, confirming deletion, verifying removal notice, and verifying row removal.
- **Out of scope:**
  - pgvector cosine distance embeddings calculation internals.
  - Third-party file formats beyond text/markdown (PDF/DOCX binary parsing).
- **Assumptions:**
  - Test user `qa.test@griddynamics.com` is configured with active `ADMIN` role.
  - Microservices running: ApiGateway (`5204`), IdentityTenant (`5233`), AIAgent.Api (`5250`), Central Hub frontend (`5172`).
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Prevent document upload and disable upload button when no file is selected | Functional / Validation | KnowledgePage.jsx | Upload button disabled when file is null |
| R-02 | Enforce audience selection by requiring at least one role when "Specific Roles" is selected | Functional / Boundary | KnowledgePage.jsx | Error hint and upload button disabled |
| R-03 | Enable document upload when valid role is selected under "Specific Roles" | Functional / UI | KnowledgePage.jsx | Selecting role checkbox clears error hint |
| R-04 | Upload and ingest a new text document with custom title and "Everyone" audience | Functional / Mutation | KnowledgePage.jsx | Dispatches multipart upload to AIAgent.Api |
| R-05 | Display success notification banner and reset form inputs upon document ingestion | UI / Feedback | KnowledgePage.jsx | Banner: "... was added. Athenna can answer from it now." |
| R-06 | Render newly ingested document in the indexed documents table with title and badges | UI / Table | KnowledgePage.jsx | Displays title, audience pill, and v1 badge |
| R-07 | Update indexed document counter in the header | UI / Metric | KnowledgePage.jsx | Increments count-number in header |
| R-08 | Open document content preview modal upon clicking document row and display extracted text | UI / Modal | KnowledgePage.jsx | Modal renders full document text in pre tag |
| R-09 | Close document content preview modal via "Done Reading" button | UI / Modal | KnowledgePage.jsx | Dismisses modal backdrop and dialog |
| R-10 | Delete document using inline delete action and verify document is purged from table | Functional / Mutation | KnowledgePage.jsx | Inline confirmation box: Delete / Cancel |

## 3. Scenarios & Test Cases
### Scenario 1: Upload Button Disablement on Empty Form (R-01)
- **Tag:** `@functional @validation`
- **Given:** Authenticated admin on `/knowledge`.
- **When:** No file is staged in the upload form.
- **Then:** Upload button is disabled.

### Scenario 2: Audience Role Selection Validation (R-02)
- **Tag:** `@functional @boundary`
- **Given:** Admin on `/knowledge` with file staged.
- **When:** Switches target audience to "Specific Roles" without selecting any role.
- **Then:** Field error hint appears and upload button is disabled.

### Scenario 3: Enable Submission on Role Selection (R-03)
- **Tag:** `@functional @ui`
- **Given:** Admin on `/knowledge` with "Specific Roles" active.
- **When:** Checks "Developers" role checkbox.
- **Then:** Field error hint disappears and upload button is enabled.

### Scenario 4: Ingest New Document (R-04)
- **Tag:** `@functional @mutation @smoke`
- **Given:** Admin on `/knowledge`.
- **When:** Stages markdown file, sets custom title `E2E-Knowledge-[timestamp]`, sets "Everyone" audience, and submits.
- **Then:** Document is uploaded and vectorized.

### Scenario 5: Verify Ingestion Feedback Banner and Form Reset (R-05)
- **Tag:** `@ui @feedback`
- **Given:** Admin submitted document.
- **Then:** Success notice appears and title input is cleared.

### Scenario 6: Verify Document Row in Table (R-06)
- **Tag:** `@ui @table`
- **Given:** Admin on `/knowledge`.
- **Then:** Table row for `E2E-Knowledge-[timestamp]` displays title, "Everyone" pill, and "v1" version tag.

### Scenario 7: Verify Indexed Documents Counter (R-07)
- **Tag:** `@ui @metrics`
- **Given:** Admin on `/knowledge`.
- **Then:** Header counter `count-number` is greater than 0.

### Scenario 8: Open Preview Modal and Verify Extracted Text (R-08)
- **Tag:** `@ui @modal`
- **Given:** Admin viewing indexed documents table.
- **When:** Clicks document row.
- **Then:** Preview modal opens showing extracted text content.

### Scenario 9: Dismiss Preview Modal (R-09)
- **Tag:** `@ui @modal`
- **Given:** Admin viewing document preview modal.
- **When:** Clicks "Done Reading" button.
- **Then:** Modal closes and table view is restored.

### Scenario 10: Delete Document and Verify Purge (R-10)
- **Tag:** `@functional @mutation @cleanup`
- **Given:** Admin viewing document row in table.
- **When:** Clicks trash icon, clicks inline "Delete" button.
- **Then:** Removal notice appears and document row is purged from table.

## 4. Traceability Matrix (RTM)
| Requirement | Scenario | Test Case Function |
|---|---|---|
| R-01 | Scenario 1 | `test('HK-CRUD-01: should disable upload button when no file is selected', ...)` |
| R-02 | Scenario 2 | `test('HK-CRUD-02: should require at least one role when Specific Roles is selected', ...)` |
| R-03 | Scenario 3 | `test('HK-CRUD-03: should enable upload button when role checkbox is checked', ...)` |
| R-04 | Scenario 4 | `test('HK-CRUD-04: should upload and ingest new document with Everyone audience', ...)` |
| R-05 | Scenario 5 | `test('HK-CRUD-05: should display success notification and reset form upon ingestion', ...)` |
| R-06 | Scenario 6 | `test('HK-CRUD-06: should render newly ingested document in table with badges', ...)` |
| R-07 | Scenario 7 | `test('HK-CRUD-07: should reflect incremented document count in header metric', ...)` |
| R-08 | Scenario 8 | `test('HK-CRUD-08: should open preview modal on row click and render extracted text', ...)` |
| R-09 | Scenario 9 | `test('HK-CRUD-09: should dismiss document preview modal via Done Reading button', ...)` |
| R-10 | Scenario 10 | `test('HK-CRUD-10: should delete document via inline confirmation and purge from table', ...)` |
