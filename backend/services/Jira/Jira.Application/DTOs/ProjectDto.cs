namespace Jira.Application.DTOs;

public sealed record ProjectDto(
    long Id,
    long TenantId,
    string Key,
    string Name,
    string? Description,
    string ProjectType,
    string Status,
    long? ProjectLeadUserId,
    long? ParentProjectId,
    uint Version,
    long? AccountId = null,
    string? DisplayName = null
    )
{
    public string AccountName => Name;
    public string ProjectName => string.IsNullOrEmpty(DisplayName) ? Key : DisplayName;
}