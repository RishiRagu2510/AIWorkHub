using Jira.Domain.Entities;
using Jira.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace Jira.Infrastructure.Persistence;

public sealed class JiraDbContext : DbContext
{
    public JiraDbContext(
        DbContextOptions<JiraDbContext> options)
        : base(options)
    {
    }

    public DbSet<WorkLog> WorkLogs => Set<WorkLog>();
    public DbSet<TimesheetPeriod> TimesheetPeriods => Set<TimesheetPeriod>();

    public DbSet<Notifications.Notification> Notifications => Set<Notifications.Notification>();

    public DbSet<Collaboration.ActivityEvent> ActivityEvents => Set<Collaboration.ActivityEvent>();
    public DbSet<Collaboration.IssueWatcher> Watchers => Set<Collaboration.IssueWatcher>();
    public DbSet<Collaboration.WorkflowRules> WorkflowRules => Set<Collaboration.WorkflowRules>();
    public DbSet<Collaboration.RequestReplay> RequestReplays => Set<Collaboration.RequestReplay>();
    public DbSet<Collaboration.Board> Boards => Set<Collaboration.Board>();
    public DbSet<Collaboration.BoardColumn> BoardColumns => Set<Collaboration.BoardColumn>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Issue> Issues => Set<Issue>();

    public DbSet<IssueComment> IssueComments =>
        Set<IssueComment>();

    public DbSet<OutboxMessage> OutboxMessages =>
        Set<OutboxMessage>();

    protected override void OnModelCreating(
        ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(JiraDbContext).Assembly);
    }
}