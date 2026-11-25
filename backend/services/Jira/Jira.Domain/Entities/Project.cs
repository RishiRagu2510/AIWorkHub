using Jira.Domain.Common;
using Jira.Domain.Enums;

namespace Jira.Domain.Entities;

public sealed class Project : Entity
{
    
    public long? AccountId { get; private set; }
    public void SetAccount(long accountId, string accountName)
    {
        if (accountId <= 0) throw new ArgumentOutOfRangeException(nameof(accountId));
        AccountId = accountId; Name = accountName; Touch();
    }
    public void SelectAccount(long? accountId) { AccountId = accountId; }
    public string DisplayName { get; private set; } = "";
    public void Rename(string? name) {
        if (name is null) return;
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 1000) throw new ArgumentException("Project name must contain 1–1000 characters.");
        DisplayName = name.Trim(); Touch();
    }
    public uint Version { get; private set; }

    private Project()
    {
    }

    public Project(
        long tenantId,
        string key,
        string name,
        string? description = null,
        ProjectType projectType = ProjectType.Software,
        long? projectLeadUserId = null,
        long? parentProjectId = null)
    {
        if (tenantId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException(
                "Project key is required.",
                nameof(key));
        }

        if (key.Trim().Length > 1000)
        {
            throw new ArgumentException(
                "Project key cannot exceed 1000 characters.",
                nameof(key));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Project name is required.",
                nameof(name));
        }

        if (projectLeadUserId.HasValue &&
            projectLeadUserId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(projectLeadUserId));
        }

        if (parentProjectId.HasValue && parentProjectId <= 0)
            throw new ArgumentOutOfRangeException(nameof(parentProjectId));
        ParentProjectId = parentProjectId;
        TenantId = tenantId;
        Key = key.Trim().ToUpperInvariant();
        DisplayName = key.Trim();
        Name = name.Trim();
        Description = description?.Trim();
        ProjectType = projectType;
        Status = ProjectStatus.Active;
        ProjectLeadUserId = projectLeadUserId;
    }

    
    public long? ParentProjectId { get; private set; }

    public long TenantId { get; private set; }

    public string Key { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public ProjectType ProjectType { get; private set; }

    public ProjectStatus Status { get; private set; }

    public long? ProjectLeadUserId { get; private set; }

    public void Update(
        string name,
        string? description,
        ProjectType projectType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Project name is required.",
                nameof(name));
        }

        Name = name.Trim();
        Description = description?.Trim();
        ProjectType = projectType;

        Touch();
    }

    public void MoveTo(long? parentProjectId)
    {
        if (parentProjectId.HasValue && (parentProjectId <= 0 || parentProjectId == Id))
            throw new ArgumentException("A project cannot be its own parent.", nameof(parentProjectId));
        ParentProjectId = parentProjectId;
        Touch();
    }

    public void AssignProjectLead(long userId)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId));
        }

        ProjectLeadUserId = userId;
        Touch();
    }

    public void RemoveProjectLead()
    {
        ProjectLeadUserId = null;
        Touch();
    }

    public void Archive()
    {
        Status = ProjectStatus.Archived;
        Touch();
    }

    public void Disable()
    {
        Status = ProjectStatus.Disabled;
        Touch();
    }

    public void Activate()
    {
        Status = ProjectStatus.Active;
        Touch();
    }
}