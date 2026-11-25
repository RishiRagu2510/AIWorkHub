using System.ComponentModel.DataAnnotations;

namespace Jira.Contracts.Projects;

public sealed record CreateProjectRequest
{
    [Required, StringLength(1000), RegularExpression("[A-Za-z][A-Za-z0-9_ -]*")]
    public string Key { get; init; } = "";
    public string ProjectName { get => Key; init => Key = value; }
    [Required, StringLength(200)]
    public string Name { get; init; } = "";
    
    [Range(1, long.MaxValue)]
    public long? AccountId { get; init; }
    public string AccountName { get => Name; init => Name = value; }
    [StringLength(1000)]
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string ProjectType { get; init; } = "SOFTWARE";
    [Range(1, long.MaxValue)]
    public long? ParentProjectId { get; init; }
}
