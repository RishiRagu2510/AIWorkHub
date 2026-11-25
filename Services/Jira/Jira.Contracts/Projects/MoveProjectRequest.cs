using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Jira.Contracts.Projects;

public sealed record MoveProjectRequest
{
    
    [JsonRequired, Range(1, long.MaxValue)]
    public long? ParentProjectId { get; init; }
}
