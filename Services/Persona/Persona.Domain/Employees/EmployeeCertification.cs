namespace Persona.Domain.Employees;

public sealed class EmployeeCertification
{
    public string? CredentialId { get; set; }
    public long Id { get; set; }
    public long TenantId { get; set; }
    public uint Version { get; set; }
    public long EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public string Issuer { get; set; } = "";
    public DateOnly IssuedDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? CredentialUrl { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
}