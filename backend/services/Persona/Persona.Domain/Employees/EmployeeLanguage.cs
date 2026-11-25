namespace Persona.Domain.Employees;

public sealed class EmployeeLanguage
{
    public long Id { get; set; }
    public long TenantId { get; set; }
    public uint Version { get; set; }
    public long EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public string Proficiency { get; set; } = "";
}