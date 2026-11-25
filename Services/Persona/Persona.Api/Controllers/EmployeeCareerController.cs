using Authorization;
using Persona.Api.Services.Employees;
using Persona.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Persona.Api.Controllers;

[RequirePortal("employee")]
[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[EmployeeErrors]
[Route("api/employees")]
public sealed class EmployeeCareerController(EmployeeCareerService career) : ControllerBase
{
    private IActionResult Changed(SectionChangeDto change)
    {
        Response.Headers.ETag = $"\"{change.Version}\"";
        return Ok(change);
    }


    [HttpGet("{id:long}/skills")]
    [HttpGet("me/skills")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> GetSkills(long id = 0, CancellationToken ct = default)
    {
        return Ok(await career.SkillsAsync(id, ct));
    }

    [HttpPost("{id:long}/skills")]
    [HttpPost("me/skills")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> AddSkills(SkillRequest r, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.SaveSkillAsync(id, 0, r, ct));
    }

    [HttpPut("{id:long}/skills/{entryId:long}")]
    [HttpPut("me/skills/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> UpdateSkills(long entryId, SkillRequest r, long id = 0,
        CancellationToken ct = default)
    {
        return Changed(await career.SaveSkillAsync(id, entryId, r, ct));
    }

    [HttpDelete("{id:long}/skills/{entryId:long}")]
    [HttpDelete("me/skills/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> DeleteSkills(long entryId, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.DeleteSkillAsync(id, entryId, ct));
    }


    [HttpGet("{id:long}/education")]
    [HttpGet("me/education")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> GetEducation(long id = 0, CancellationToken ct = default)
    {
        return Ok(await career.EducationAsync(id, ct));
    }

    [HttpPost("{id:long}/education")]
    [HttpPost("me/education")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> AddEducation(EducationRequest r, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.SaveEducationAsync(id, 0, r, ct));
    }

    [HttpPut("{id:long}/education/{entryId:long}")]
    [HttpPut("me/education/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> UpdateEducation(long entryId, EducationRequest r, long id = 0,
        CancellationToken ct = default)
    {
        return Changed(await career.SaveEducationAsync(id, entryId, r, ct));
    }

    [HttpDelete("{id:long}/education/{entryId:long}")]
    [HttpDelete("me/education/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> DeleteEducation(long entryId, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.DeleteEducationAsync(id, entryId, ct));
    }


    [HttpGet("{id:long}/certifications")]
    [HttpGet("me/certifications")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> GetCertifications(long id = 0, CancellationToken ct = default)
    {
        return Ok(await career.CertificationsAsync(id, ct));
    }

    [HttpPost("{id:long}/certifications")]
    [HttpPost("me/certifications")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> AddCertifications(CertificationRequest r, long id = 0,
        CancellationToken ct = default)
    {
        return Changed(await career.SaveCertificationAsync(id, 0, r, ct));
    }

    [HttpPut("{id:long}/certifications/{entryId:long}")]
    [HttpPut("me/certifications/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> UpdateCertifications(long entryId, CertificationRequest r, long id = 0,
        CancellationToken ct = default)
    {
        return Changed(await career.SaveCertificationAsync(id, entryId, r, ct));
    }

    [HttpPost("{id:long}/certifications/upload")]
    [HttpPost("me/certifications/upload")]
    [RequirePermission("EMPLOYEE_VIEW")]
    [RequestSizeLimit(10485760)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10485760)]
    public async Task<IActionResult> UploadCertificationFile(IFormFile file, long id = 0,
        CancellationToken ct = default)
    {
        return Ok(await career.UploadCertificateAsync(id, file, ct));
    }

    [HttpGet("{id:long}/certifications/file/{fileId}")]
    [HttpGet("me/certifications/file/{fileId}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> DownloadCertificationFile(string fileId, long id = 0,
        CancellationToken ct = default)
    {
        var file = await career.CertificateFileAsync(id, fileId, ct);
        if (file is null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpDelete("{id:long}/certifications/{entryId:long}")]
    [HttpDelete("me/certifications/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> DeleteCertifications(long entryId, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.DeleteCertificationAsync(id, entryId, ct));
    }


    [HttpGet("{id:long}/experience")]
    [HttpGet("me/experience")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> GetExperience(long id = 0, CancellationToken ct = default)
    {
        return Ok(await career.ExperienceAsync(id, ct));
    }

    [HttpPost("{id:long}/experience")]
    [HttpPost("me/experience")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> AddExperience(ExperienceRequest r, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.SaveExperienceAsync(id, 0, r, ct));
    }

    [HttpPut("{id:long}/experience/{entryId:long}")]
    [HttpPut("me/experience/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> UpdateExperience(long entryId, ExperienceRequest r, long id = 0,
        CancellationToken ct = default)
    {
        return Changed(await career.SaveExperienceAsync(id, entryId, r, ct));
    }

    [HttpDelete("{id:long}/experience/{entryId:long}")]
    [HttpDelete("me/experience/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> DeleteExperience(long entryId, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.DeleteExperienceAsync(id, entryId, ct));
    }


    [HttpGet("{id:long}/languages")]
    [HttpGet("me/languages")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> GetLanguages(long id = 0, CancellationToken ct = default)
    {
        return Ok(await career.LanguagesAsync(id, ct));
    }

    [HttpPost("{id:long}/languages")]
    [HttpPost("me/languages")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> AddLanguages(LanguageRequest r, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.SaveLanguageAsync(id, 0, r, ct));
    }

    [HttpPut("{id:long}/languages/{entryId:long}")]
    [HttpPut("me/languages/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> UpdateLanguages(long entryId, LanguageRequest r, long id = 0,
        CancellationToken ct = default)
    {
        return Changed(await career.SaveLanguageAsync(id, entryId, r, ct));
    }

    [HttpDelete("{id:long}/languages/{entryId:long}")]
    [HttpDelete("me/languages/{entryId:long}")]
    [RequirePermission("EMPLOYEE_VIEW")]
    public async Task<IActionResult> DeleteLanguages(long entryId, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.DeleteLanguageAsync(id, entryId, ct));
    }


    [HttpGet("{id:long}/compensation")]
    [Authorize(Roles = "ADMIN,HR,HUMAN_RESOURCE")]
    [RequirePermission("EMPLOYEE_VIEW_SENSITIVE")]
    public async Task<IActionResult> GetCompensation(long id = 0, CancellationToken ct = default)
    {
        return Ok(await career.CompensationAsync(id, ct));
    }

    [HttpPost("{id:long}/compensation")]
    [Authorize(Roles = "ADMIN,HR,HUMAN_RESOURCE")]
    [RequirePermission("EMPLOYEE_VIEW_SENSITIVE")]
    [RequirePermission("EMPLOYEE_UPDATE")]
    public async Task<IActionResult> AddCompensation(CompensationRequest r, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.SaveCompensationAsync(id, 0, r, ct));
    }

    [HttpPut("{id:long}/compensation/{entryId:long}")]
    [Authorize(Roles = "ADMIN,HR,HUMAN_RESOURCE")]
    [RequirePermission("EMPLOYEE_VIEW_SENSITIVE")]
    [RequirePermission("EMPLOYEE_UPDATE")]
    public async Task<IActionResult> UpdateCompensation(long entryId, CompensationRequest r, long id = 0,
        CancellationToken ct = default)
    {
        return Changed(await career.SaveCompensationAsync(id, entryId, r, ct));
    }

    [HttpDelete("{id:long}/compensation/{entryId:long}")]
    [Authorize(Roles = "ADMIN,HR,HUMAN_RESOURCE")]
    [RequirePermission("EMPLOYEE_VIEW_SENSITIVE")]
    [RequirePermission("EMPLOYEE_UPDATE")]
    public async Task<IActionResult> DeleteCompensation(long entryId, long id = 0, CancellationToken ct = default)
    {
        return Changed(await career.DeleteCompensationAsync(id, entryId, ct));
    }
}