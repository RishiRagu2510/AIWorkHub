namespace AIAgent.Infrastructure.Tools;







public static class JiraRoutes
{
    public const string Issues = "/api/issues";
    public const string IssueSearch = "/api/issues/search";
    public static string Issue(object id) => $"/api/issues/{id}";
    public static string IssueByKey(string escapedKey) => $"/api/issues/key/{escapedKey}";
    public static string IssueAssignee(object id) => $"/api/issues/{id}/assignee";
    public static string IssueTransition(object id) => $"/api/issues/{id}/transition";
    public static string IssueComments(object id) => $"/api/issues/{id}/comments";

    public const string Projects = "/api/projects";
    public const string ProjectAllocations = "/api/projects/allocations";
    public static string ProjectsByUser(object userId) => $"/api/projects/by-user/{userId}";
    public static string ProjectAssignmentMembers(object projectId) => $"/api/projects/{projectId}/assignment-members";

    public const string Boards = "/api/boards";
    public static string BoardSprints(object boardId) => $"/api/boards/{boardId}/sprints";
    public static string BoardBacklog(object boardId) => $"/api/boards/{boardId}/backlog";

    public const string WorkLogs = "/api/worklogs";
    public static string WorkLog(object id) => $"/api/worklogs/{id}";

    public const string MyTimesheet = "/api/timesheets/me";
    public const string TeamTimesheets = "/api/timesheets/team";
    public const string SubmitTimesheet = "/api/timesheets/submit";
    public static string TimesheetApprovers(object periodId) => $"/api/timesheets/{periodId}/approvers";
    
    public static string TimesheetAction(object action) => $"/api/timesheets/{action}";
}


public static class IdentityRoutes
{
    public const string CurrentUser = "/api/auth/me";

    public const string Employees = "/api/employees";
    public static string Employee(object id) => $"/api/employees/{id}";
    public static string EmployeeSection(object id, object section) => $"/api/employees/{id}/{section}";
    public static string EmployeeSkills(object id) => $"/api/employees/{id}/skills";
    public static string EmployeeCareerHistory(object id) => $"/api/employees/{id}/career-history";
    public static string EmployeeCompensation(object id) => $"/api/employees/{id}/compensation";
    public const string EmployeeReferences = "/api/employees/references";
    public const string CareerCatalog = "/api/employees/career-catalog";
    public const string ExportDetails = "/api/employees/export-details";

    public const string Me = "/api/employees/me";
    public const string MyProfile = "/api/employees/me/profile";
    public const string MySkills = "/api/employees/me/skills";
    public static string MySection(object section) => $"/api/employees/me/{section}";

    public const string Absence = "/api/employees/absence";
    public const string AbsencePolicy = "/api/employees/absence/policy";
    public static string AbsenceDecision(object requestId) => $"/api/employees/absence/{requestId}/decision";
}
