using IdentityTenant.Domain.Careers;

namespace IdentityTenant.Domain.Employees;

public sealed class Employee
{
    public long? CareerProfileId { get; set; }
    public long? CareerLevelId { get; set; }
    public CareerProfile? CareerProfile { get; set; }
    public CareerLevel? CareerLevel { get; set; }
    public long Id { get; set; }
    public long TenantId { get; set; }
    public uint Version { get; set; }
    public long UserId { get; set; }
    public string EmployeeCode { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? PreferredName { get; set; }
    public string? WorkAddress { get; set; }
    public string? AadharNumber { get; set; }
    public string? PermanentAddress { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }
    public string WorkEmail { get; set; } = "";
    public string? Phone { get; set; }
    public string? ProfilePhotoUrl { get; set; }
    public long? DepartmentId { get; set; }
    public long? DisciplineId { get; set; }
    public long? JobTitleId { get; set; }
    public long? DesignationId { get; set; }
    public long? GradeId { get; set; }
    public long? LocationId { get; set; }
    public long? ManagerEmployeeId { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string EmploymentStatus { get; set; } = "ACTIVE";
    public DateOnly JoiningDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}