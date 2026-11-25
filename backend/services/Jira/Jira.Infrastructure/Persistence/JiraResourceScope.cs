using Microsoft.EntityFrameworkCore;
using Jira.Domain.Entities;
using TenantContext;

namespace Jira.Infrastructure.Persistence;







public sealed class JiraResourceScope(JiraDbContext db, ITenantContext context)
{
    public IQueryable<Project> MemberProjects(long tenantId)
    {
        var userId = context.UserId;
        var allowed = context.IsAuthenticated && context.TenantId == tenantId;
        var admin = context.Roles.Contains("ADMIN");
        
        
        
        return db.Projects.FromSqlInterpolated($"""
            WITH RECURSIVE accessible_projects AS (
                SELECT p.*, p.xmin FROM projects p
                WHERE {allowed} AND p.tenant_id = {tenantId} AND (
                    {admin} OR p.project_lead_user_id = {userId} OR EXISTS (
                        SELECT 1 FROM project_members m WHERE m.tenant_id = {tenantId}
                        AND m.project_id = p.id AND m.user_id = {userId}))
                UNION
                SELECT child.*, child.xmin FROM projects child
                JOIN accessible_projects parent ON child.parent_project_id = parent.id
                    AND child.tenant_id = parent.tenant_id
                WHERE child.tenant_id = {tenantId}
            ) SELECT * FROM accessible_projects
            """);
    }

    public IQueryable<Issue> Issues(long tenantId)
    {
        var userId = context.UserId;
        var allowed = context.IsAuthenticated && context.TenantId == tenantId;
        var admin = context.Roles.Contains("ADMIN");
        var allowProjectIssueScope = !context.Roles.Contains("DEVELOPER");
        var projects = MemberProjects(tenantId);
        return db.Issues.Where(issue => allowed && issue.TenantId == tenantId && !issue.IsDeleted &&
            (admin || issue.AssigneeUserId == userId || issue.Assignees.Any(a => a.UserId == userId) || issue.ReporterUserId == userId ||
             (allowProjectIssueScope && projects.Any(project => project.Id == issue.ProjectId))));
    }

    public IQueryable<Project> Projects(long tenantId)
    {
        var memberProjects = MemberProjects(tenantId);
        var issues = Issues(tenantId);
        return db.Projects.Where(project => project.TenantId == tenantId &&
            (memberProjects.Any(memberProject => memberProject.Id == project.Id) ||
             issues.Any(issue => issue.ProjectId == project.Id)));
    }

    public IQueryable<IssueComment> Comments(long tenantId)
    {
        var issues = Issues(tenantId);
        return db.IssueComments.Where(comment => comment.TenantId == tenantId &&
            issues.Any(issue => issue.Id == comment.IssueId));
    }
}
