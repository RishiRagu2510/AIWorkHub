using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace IdentityTenant.Contracts;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeRequest(
 [Range(0,long.MaxValue)] long UserId,
 [Required,StringLength(80)] string EmployeeCode,
 [Required,StringLength(100)] string FirstName,
 [Required,StringLength(100)] string LastName,
 DateOnly JoiningDate,
 [StringLength(100)] string? PreferredName=null,
 [StringLength(40)] string? Phone=null,
 [StringLength(2000)] string? ProfilePhotoUrl=null,
 long? DepartmentId=null, long? DisciplineId=null, long? JobTitleId=null,
 long? DesignationId=null, long? GradeId=null, long? LocationId=null,
 long? ManagerEmployeeId=null, string EmploymentType="FULL_TIME",
 string EmploymentStatus="ACTIVE", DateOnly? EndDate=null, long? CareerProfileId=null, long? CareerLevelId=null, [StringLength(150)]string? Location=null, long? CatalogGradeId=null, long? CatalogDepartmentId=null, [StringLength(100)]string? Designation=null, DateOnly? CareerEffectiveDate=null, [StringLength(1000)]string? CareerChangeNote=null,
 [StringLength(50)] string? AadharNumber=null, [StringLength(1000)] string? PermanentAddress=null, [StringLength(200)] string? EmergencyContactName=null, [StringLength(50)] string? EmergencyContactPhone=null, [StringLength(100)] string? EmergencyContactRelation=null);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EmployeeSelfRequest([StringLength(100)]string? PreferredName, [StringLength(40)]string? Phone, long? LocationId, [StringLength(2000)]string? ProfilePhotoUrl=null, [StringLength(150)]string? Location=null, [StringLength(50)]string? AadharNumber=null, [StringLength(1000)]string? PermanentAddress=null, [StringLength(200)]string? EmergencyContactName=null, [StringLength(50)]string? EmergencyContactPhone=null, [StringLength(100)]string? EmergencyContactRelation=null);
public sealed record EmployeeReferenceRequest([Required]string Kind,[Required,StringLength(150)]string Name);
public sealed record EmployeeSearch(string? Search=null,long? DepartmentId=null,long? DisciplineId=null,long? GradeId=null,
 long? ManagerEmployeeId=null,long? LocationId=null,string? EmploymentStatus=null,int Page=1,int PageSize=25,bool Team=false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SkillRequest([StringLength(150)]string? SkillName,[StringLength(100)]string? Category,int Proficiency,decimal YearsOfExperience,DateOnly? LastUsedDate,long? SkillId=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record EducationRequest([Required,StringLength(200)]string Institution,[Required,StringLength(200)]string Qualification,DateOnly StartDate,DateOnly? EndDate,[StringLength(200)]string? FieldOfStudy=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificationRequest([Required,StringLength(200)]string Name,[Required,StringLength(200)]string Issuer,DateOnly IssuedDate,DateOnly? ExpiryDate,[StringLength(2000)]string? CredentialUrl,[StringLength(200)]string? CredentialId=null,[StringLength(2000)]string? FileUrl=null,[StringLength(255)]string? FileName=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ExperienceRequest([Required,StringLength(200)]string Company,[Required,StringLength(200)]string Title,[StringLength(4000)]string? Description,DateOnly StartDate,DateOnly? EndDate);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record LanguageRequest([Required,StringLength(100)]string Name,[Required,StringLength(100)]string Proficiency);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompensationRequest(decimal Ctc,decimal Bonus,[Required,StringLength(3,MinimumLength=3)]string Currency,DateOnly EffectiveFrom,DateOnly? EffectiveTo);
