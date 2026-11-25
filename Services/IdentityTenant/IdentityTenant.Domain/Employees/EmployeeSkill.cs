using IdentityTenant.Domain.Careers;

namespace IdentityTenant.Domain.Employees;

public sealed class EmployeeSkill
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public uint Version { get; set; }
    public long EmployeeId { get; set; }
    public long? SkillId { get; set; }
    public Skill? Skill { get; set; }
    public string SkillName { get; set; } = "";
    public string? Category { get; set; }
    public int Proficiency { get; set; }
    public decimal YearsOfExperience { get; set; }
    public DateOnly? LastUsedDate { get; set; }
}