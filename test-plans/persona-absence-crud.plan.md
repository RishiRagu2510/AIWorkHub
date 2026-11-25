---
plan_id: persona-absence-crud
title: Persona Portal — Leave & WFH Mutation Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5174, file:frontend/persona/src/pages/EmployeeAbsence.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: 736b853386860ae0
---

# Persona Portal — Leave & WFH Mutation Test Plan

## 1. Overview
- **Objective:** Verify end-to-end leave and work-from-home (WFH) request workflows on Persona (`https://localhost:5174/employees/leave`) using dedicated test user credentials (`qa.test@griddynamics.com`), covering form validation, balance limit enforcement, WFH submission, floating holiday (casual leave) submission, pending balance reservation on balance cards, table record presentation, cancellation workflow, immediate status updates, and balance restoration.
- **In scope:**
  - Form validation on empty submission on `/employees/leave`.
  - Exceeded balance alert (`division.absence-alert-warning[role="alert"]`) and submit button disablement when requested leave days exceed available balance.
  - Submitting Work From Home (WFH) request with unique reason and date span.
  - Verifying manager approval dispatch notice `"Request sent to your manager."` and form reset.
  - Verifying the newly submitted WFH request renders in the `My requests` table with `Work from home` chip and `PENDING` badge.
  - Submitting 1-day Floating holiday (casual leave) within available allowance.
  - Verifying pending balance deduction updates the Floating holidays metric card.
  - Finding the pending request in the table and triggering `Cancel request`.
  - Verifying cancellation success alert `"Request cancelled."` and `CANCELLED` badge in table.
  - Verifying the cancelled request restores the available days on the Floating holiday balance card.
- **Out of scope:**
  - Manager-side approval and rejection in Team approvals view involving second test account.
  - Annual leave policy updates via admin role.
- **Assumptions:**
  - Dedicated test user `qa.test@griddynamics.com` is configured with active employee profile (`GD-QA-001`) and assigned manager (`id = 10`).
  - Leave policy configured for year 2026 with 2 Floating holiday days.
  - Microservices running: ApiGateway (`5204`), IdentityTenant (`5233`), Persona.Api (`5234`), Persona frontend (`5174`).
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Prevent submission when required dates or reason are empty | Functional / Validation | EmployeeAbsence.jsx | Native and DOM validation prevents empty submit |
| R-02 | Enforce leave quota limits by displaying warning alert and disabling submit button when requested days exceed available allowance | Functional / Boundary | EmployeeAbsence.jsx | Warning banner: "exceeds your available ... balance" |
| R-03 | Submit Work from home (WFH) request with valid dates and unique reason without deducting leave days | Functional / Mutation | EmployeeAbsence.jsx | WFH request creates pending item |
| R-04 | Display success confirmation notice "Request sent to your manager." and clear form inputs upon submission | UI / Feedback | EmployeeAbsence.jsx | Status banner with checkmark icon |
| R-05 | Render newly submitted request in "My requests" table with kind chip, date range, duration, and PENDING status badge | UI / Table | EmployeeAbsence.jsx | RequestRow renders correct badges and cells |
| R-06 | Submit Floating holiday (casual leave) request within available balance quota | Functional / Mutation | EmployeeAbsence.jsx | Creates pending casual leave request |
| R-07 | Update Floating holiday balance card to reflect incremented pending days and reserved allowance | UI / Metric | EmployeeAbsence.jsx | Balance card reflects pending reservation |
| R-08 | Locate pending request in "My requests" table and initiate cancellation | Functional / Mutation | EmployeeAbsence.jsx | "Cancel request" button triggers cancellation |
| R-09 | Display success banner "Request cancelled." and update request row badge to CANCELLED | UI / Feedback | EmployeeAbsence.jsx | Immediate state transition without full reload |
| R-10 | Verify Floating holiday balance card restores available days after request cancellation | Functional / Verification | EmployeeAbsence.jsx | Pending count decrements, available days restored |

## 3. Scenarios & Test Cases
### Scenario 1: Form Validation on Empty Submit (R-01)
- **Tag:** `@functional @validation`
- **Given:** Authenticated user on `/employees/leave`.
- **When:** Submits form with empty dates and reason.
- **Then:** Native required validation flags missing fields and prevents submission.

### Scenario 2: Exceeded Balance Quota Enforcement (R-02)
- **Tag:** `@functional @boundary`
- **Given:** User on `/employees/leave` with 2 available Floating holiday days.
- **When:** Selects `Floating holidays` and specifies 5 working days duration.
- **Then:** Exceeded warning banner is displayed and submit button is disabled.

### Scenario 3: Submit Work from Home Request (R-03)
- **Tag:** `@functional @mutation @smoke`
- **Given:** User on `/employees/leave`.
- **When:** Selects `Work from home`, enters valid dates and reason `E2E-WFH-[timestamp]`, and submits.
- **Then:** Mutation is dispatched to Persona API.

### Scenario 4: Verify Dispatch Success Notice (R-04)
- **Tag:** `@ui @feedback`
- **Given:** User submitted WFH request.
- **Then:** Success notice `Request sent to your manager.` appears and form is reset.

### Scenario 5: Verify Request in Table with PENDING Badge (R-05)
- **Tag:** `@ui @table`
- **Given:** User on `/employees/leave`.
- **Then:** Table row with `E2E-WFH-[timestamp]` displays `Work from home` chip and `PENDING` badge.

### Scenario 6: Submit Floating Holiday Request (R-06)
- **Tag:** `@functional @mutation`
- **Given:** User on `/employees/leave`.
- **When:** Selects `Floating holidays`, enters 1 working day date range and reason `E2E-LEAVE-[timestamp]`, and submits.
- **Then:** Request is dispatched and success banner appears.

### Scenario 7: Verify Pending Balance on Metric Card (R-07)
- **Tag:** `@ui @metrics`
- **Given:** User on `/employees/leave`.
- **Then:** Floating holidays card displays `1 pending` stat pill.

### Scenario 8: Cancel Pending Request via Row Action (R-08)
- **Tag:** `@functional @mutation @cleanup`
- **Given:** User viewing `My requests` table.
- **When:** Clicks `Cancel request` button on the `E2E-LEAVE-[timestamp]` row.
- **Then:** Cancellation mutation is dispatched.

### Scenario 9: Verify Cancellation Banner and CANCELLED Badge (R-09)
- **Tag:** `@ui @feedback`
- **Given:** User cancelled the leave request.
- **Then:** Success notice `Request cancelled.` appears and row status badge reflects `CANCELLED`.

### Scenario 10: Verify Restored Balance on Metric Card (R-10)
- **Tag:** `@functional @verification`
- **Given:** User on `/employees/leave`.
- **Then:** Floating holidays balance card pending count returns to 0 and available days are restored.

## 4. Traceability Matrix (RTM)
| Requirement | Scenario | Test Case Function |
|---|---|---|
| R-01 | Scenario 1 | `test('PR-CRUD-01: should prevent submission when required fields are missing', ...)` |
| R-02 | Scenario 2 | `test('PR-CRUD-02: should enforce leave quota and disable submit on exceeded balance', ...)` |
| R-03 | Scenario 3 | `test('PR-CRUD-03: should submit work from home request with unique reason', ...)` |
| R-04 | Scenario 4 | `test('PR-CRUD-04: should display success banner and reset form upon submission', ...)` |
| R-05 | Scenario 5 | `test('PR-CRUD-05: should render new request in table with PENDING badge and WFH kind chip', ...)` |
| R-06 | Scenario 6 | `test('PR-CRUD-06: should submit 1-day floating holiday within available allowance', ...)` |
| R-07 | Scenario 7 | `test('PR-CRUD-07: should reflect pending reservation on floating holiday balance card', ...)` |
| R-08 | Scenario 8 | `test('PR-CRUD-08: should trigger cancellation on pending floating holiday request', ...)` |
| R-09 | Scenario 9 | `test('PR-CRUD-09: should verify cancellation notice and updated CANCELLED status badge', ...)` |
| R-10 | Scenario 10 | `test('PR-CRUD-10: should verify restored available balance on floating holiday card', ...)` |
