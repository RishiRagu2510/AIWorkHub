---
plan_id: chronos-worklog-crud
title: Chronos Portal — Work Logs & Timesheet Mutation Test Plan
version: 1.0
status: Approved
created: 2026-11-18
source: [url:https://localhost:5175, file:frontend/chronos/src/pages/LogWorkPage.jsx, file:frontend/chronos/src/pages/Timesheets.jsx, file:frontend/src/features/timesheets/WorkLogForm.jsx, file:frontend/src/features/timesheets/WorkLogTable.jsx]
target_env: local
ui_verified: 2026-11-18 via Playwright on local
approved_by: "rragupathi"
approved_on: 2026-11-18
approved_hash: c997a46c6e92bf92
---

# Chronos Portal — Work Logs & Timesheet Mutation Test Plan

## 1. Overview
- **Objective:** Verify end-to-end work logging, timesheet updates, editing, and deletion workflows on Chronos (`https://localhost:5175`) using dedicated test user credentials (`qa.test@griddynamics.com`), covering form validation, time entry creation, weekly target recalculation, timesheet table row rendering, work log editing, layout switching, and work log deletion.
- **In scope:**
  - Form validation on `/timesheets/log-work` (submitting without selecting issue or entering hours).
  - Logging focused work hours with unique description, duration, and billable status.
  - Verifying success banner `Work log saved successfully!` on Log Work page.
  - Verifying weekly logged metrics and target card updates on `/timesheets/log-work`.
  - Viewing the saved work log on `/timesheets/my` in the data table.
  - Editing an existing work log row (updating duration and description).
  - Verifying the updated hours and description persist in the timesheet view.
  - Switching between Table and Hierarchy views to confirm entry grouping.
  - Deleting the work log entry with confirmation dialog.
  - Verifying the deleted entry is purged from the timesheet table.
- **Out of scope:**
  - Manager approval/rejection workflows involving second-party test users.
  - Exporting payroll CSV files to third-party payroll engines.
- **Assumptions:**
  - Dedicated synthetic test user `qa.test@griddynamics.com` active.
  - Microservices running: ApiGateway (`5204`), IdentityTenant (`5233`), Jira API (`5204/api/jira/*`), Chronos frontend (`5175`).
- **Change summary:** NEW 10 · UPDATE 0 · REUSE 0 · RETIRE 0

## 2. Requirement Model
| ID | Requirement (testable statement) | Type | Source | Notes |
|---|---|---|---|---|
| R-01 | Prevent work log submission and display validation feedback when required fields are missing | Functional / Validation | WorkLogForm.jsx | Alerts on missing issue or hours |
| R-02 | Create a new work log entry with valid issue, hours, date, and description on `/timesheets/log-work` | Functional / Mutation | LogWorkPage.jsx, WorkLogForm.jsx | Banner: "Work log saved successfully!" |
| R-03 | Update weekly total logged hours and completion metric card on Log Work page | UI / Metric | LogWorkPage.jsx | Total Logged hours increment |
| R-04 | Navigate to `/timesheets/my` and verify the newly logged entry is displayed in the table | UI / Table | Timesheets.jsx, WorkLogTable.jsx | Date, Issue, Description, Hours |
| R-05 | Verify billable badge and draft status indicator on the logged timesheet row | UI / Badge | WorkLogTable.jsx | "Yes" (billable), "DRAFT" status |
| R-06 | Open inline work log editor from table row and update duration and description | Functional / Mutation | WorkLogTable.jsx, WorkLogForm.jsx | Inline form populated with values |
| R-07 | Save edited work log and verify updated duration and description render in table | Functional / Mutation | WorkLogTable.jsx | Updated hours reflect in row |
| R-08 | Switch timesheet layout to Hierarchy view and verify work log is organized under its project | UI / Layout | Timesheets.jsx, TimesheetHierarchy.jsx | Grouped by project tree |
| R-09 | Delete the work log using row action delete button and inline confirmation | Functional / Mutation | WorkLogTable.jsx | Confirm delete action |
| R-10 | Verify the deleted work log is purged and no longer appears in the timesheet table | Functional / Verification | WorkLogTable.jsx | Row removed from table |

## 3. Scenarios & Test Cases
### Scenario 1: Form Validation on Empty Log Work (R-01)
- **Tag:** `@functional @validation`
- **Given:** Authenticated user on `/timesheets/log-work`.
- **When:** Submits form without selecting an issue or entering hours.
- **Then:** Validation feedback appears indicating hours or issue required.

### Scenario 2: Log Work Entry (R-02)
- **Tag:** `@functional @mutation @smoke`
- **Given:** User on `/timesheets/log-work`.
- **When:** Selects assigned issue, enters `2.5` hours, unique description `E2E-Log-[timestamp]`, and submits.
- **Then:** Success banner `Work log saved successfully!` is displayed.

### Scenario 3: Verify Weekly Target Metrics (R-03)
- **Tag:** `@ui @metrics`
- **Given:** User on `/timesheets/log-work`.
- **Then:** Target card displays total logged hours greater than 0.

### Scenario 4: Verify Entry in Timesheet Table (R-04)
- **Tag:** `@ui @table`
- **Given:** User navigates to `/timesheets/my`.
- **Then:** Data table contains row with description `E2E-Log-[timestamp]` and hours `2.5h`.

### Scenario 5: Verify Billable and Draft Badges (R-05)
- **Tag:** `@ui @badges`
- **Given:** User viewing the logged row on `/timesheets/my`.
- **Then:** Row displays billable badge `Yes` and period status `DRAFT`.

### Scenario 6: Edit Work Log Entry (R-06)
- **Tag:** `@functional @mutation`
- **Given:** User on `/timesheets/my`.
- **When:** Clicks `Edit` on the work log row, updates hours to `3.5` and appends `-Updated` to description.
- **Then:** Submits updated work log.

### Scenario 7: Verify Updated Row Values (R-07)
- **Tag:** `@functional @verification`
- **Given:** User on `/timesheets/my`.
- **Then:** Table row reflects `3.5h` and updated description.

### Scenario 8: Verify in Hierarchy View (R-08)
- **Tag:** `@ui @layout`
- **Given:** User on `/timesheets/my`.
- **When:** Clicks `Hierarchy` view switcher.
- **Then:** Entry is visible within the project hierarchy tree.

### Scenario 9: Delete Work Log Entry (R-09)
- **Tag:** `@functional @mutation @cleanup`
- **Given:** User on `/timesheets/my` in Table view.
- **When:** Clicks `Delete` on the work log row and clicks `Confirm delete`.
- **Then:** Row is removed from the table.

### Scenario 10: Verify Deletion Persistence (R-10)
- **Tag:** `@functional @verification`
- **Given:** User on `/timesheets/my`.
- **Then:** Deleted description is not found in the table.
