using System.Text.Json.Serialization;
namespace Persona.Contracts;





public sealed record EmployeeSkillDto(long Id, long TenantId, long EmployeeId, long? SkillId, string SkillName, string? Category, int Proficiency, decimal YearsOfExperience, DateOnly? LastUsedDate, uint Version);


public sealed record SavedEmployeeSkillDto(long Id, long TenantId, uint Version, long EmployeeId, long? SkillId, SkillDto? Skill, string SkillName, string? Category, int Proficiency, decimal YearsOfExperience, DateOnly? LastUsedDate);

public sealed record EmployeeEducationDto(string? FieldOfStudy, long Id, long TenantId, uint Version, long EmployeeId, string Institution, string Qualification, DateOnly StartDate, DateOnly? EndDate);

public sealed record EmployeeCertificationDto(string? CredentialId, long Id, long TenantId, uint Version, long EmployeeId, string Name, string Issuer, DateOnly IssuedDate, DateOnly? ExpiryDate, string? CredentialUrl, string? FileUrl, string? FileName);

public sealed record EmployeeExperienceDto(long Id, long TenantId, uint Version, long EmployeeId, string Company, string Title, string? Description, DateOnly StartDate, DateOnly? EndDate);

public sealed record EmployeeLanguageDto(long Id, long TenantId, uint Version, long EmployeeId, string Name, string Proficiency);

public sealed record EmployeeCompensationDto(long Id, long TenantId, uint Version, long EmployeeId, decimal Ctc, decimal Bonus, string Currency, DateOnly EffectiveFrom, DateOnly? EffectiveTo);


public sealed record DeletedEntryDto([property: JsonPropertyName("deletedId")] long DeletedId);


public sealed record SectionChangeDto(object Item, uint Version);

public sealed record CertificateUploadDto(string FileUrl, string FileName);


public sealed record CertificateFileDto(byte[] Bytes, string ContentType, string FileName);
