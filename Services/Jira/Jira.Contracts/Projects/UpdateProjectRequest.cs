using System.ComponentModel.DataAnnotations;

namespace Jira.Contracts.Projects;

public sealed record UpdateProjectRequest
{
    [Required, StringLength(200)]
    public string Name { get; init; } = "";
    
    [Range(1, long.MaxValue)]
    public long? AccountId { get; init; }
    public string AccountName { get => Name; init => Name = value; }
    [StringLength(1000)]
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string ProjectType { get; init; } = "SOFTWARE";
    public string? Status { get; init; }
}
