BEGIN;
INSERT INTO permissions(code,name,description) VALUES
 ('WORKLOG_VIEW','View work logs','View own authorized issue work logs'),
 ('WORKLOG_CREATE','Log work','Log work on authorized issues'),
 ('WORKLOG_UPDATE','Edit work logs','Edit own draft or rejected work logs'),
 ('WORKLOG_DELETE','Delete work logs','Delete own draft or rejected work logs'),
 ('TIMESHEET_VIEW','View timesheets','View and submit own weekly timesheet'),
 ('TIMESHEET_TEAM_VIEW','Review team time','View team time within the existing Jira resource scope'),
 ('TIMESHEET_APPROVE','Approve timesheets','Approve or reject other users submitted timesheets within scope')
ON CONFLICT(code) DO NOTHING;

INSERT INTO role_permissions(role_id,permission_id)
SELECT r.id,p.id FROM roles r CROSS JOIN permissions p
WHERE (r.code IN ('ADMIN','PROJECT_MANAGER','DEVELOPER') AND p.code IN ('WORKLOG_VIEW','WORKLOG_CREATE','WORKLOG_UPDATE','WORKLOG_DELETE','TIMESHEET_VIEW'))
   OR (r.code IN ('ADMIN','PROJECT_MANAGER') AND p.code IN ('TIMESHEET_TEAM_VIEW','TIMESHEET_APPROVE'))
ON CONFLICT DO NOTHING;
COMMIT;
