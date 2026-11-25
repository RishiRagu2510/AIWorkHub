using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using IdentityTenant.Contracts;
using Jira.Contracts.Issues;

namespace Jira.Api.Onboarding;

public sealed record IdentityRejection(int Status, JsonElement Body);

public sealed record CreateEmployeeRequestResult(IssueDto? Issue, IdentityRejection? Rejection);

public sealed record CompleteEmployeeRequestResult(IEmployeeRequestView? Request, IdentityRejection? Rejection);

public sealed record CreateEmployeeRequest(CreateIssueRequest Issue, EmployeeOnboardingRequest Proposal);

public sealed record ReviewEmployeeRequest([Range(1, int.MaxValue)] int Revision, [Required] string Decision, [StringLength(2000)] string? Note);


public interface IEmployeeRequestView { bool IsEmployeeRequest { get; } }

public sealed record NotAnEmployeeRequestDto : IEmployeeRequestView
{
    public bool IsEmployeeRequest => false;
}

public sealed record EmployeeRequestDto(
    long IssueId, string Status, int Revision, long? ReviewedBy, DateTimeOffset? ReviewedAt, string? ReviewNote, long? EmployeeId,
    long RequestedBy, DateTimeOffset RequestedAt, JsonElement Data) : IEmployeeRequestView
{
    public bool IsEmployeeRequest => true;
}
