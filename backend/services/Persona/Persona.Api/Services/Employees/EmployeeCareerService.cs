using System.Text.RegularExpressions;
using Persona.Contracts;
using Persona.Domain.Employees;
using Persona.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class EmployeeCareerService(
    PersonaDbContext db,
    ITenantContext actor,
    EmployeeAccess access,
    EmployeeEvents events,
    IEmployeePhotoStorage storage)
{
    private Task<Employee> Employee(long id, CancellationToken ct)
    {
        return id == 0 ? access.Me(ct) : access.Get(id, ct);
    }


    public async Task<IReadOnlyList<EmployeeSkillDto>> SkillsAsync(long id, CancellationToken ct)
    {
        var e = await Employee(id, ct);
        await access.RequireProfile(e, false, ct);
        return await db.Set<EmployeeSkill>().AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.EmployeeId == e.Id).OrderBy(x => x.Id)
            .Select(x => new EmployeeSkillDto(x.Id, x.TenantId, x.EmployeeId, x.SkillId,
                x.SkillId == null ? x.SkillName : x.Skill!.Name, x.SkillId == null ? x.Category : x.Skill!.Category,
                x.Proficiency, x.YearsOfExperience, x.LastUsedDate, x.Version))
            .ToListAsync(ct);
    }


    public Task<SectionChangeDto> SaveSkillAsync(long id, long entryId, SkillRequest r, CancellationToken ct)
    {
        return Mutate(id, "EMPLOYEE_SKILL_UPDATED", async (e, t) =>
        {
            if (r.Proficiency < 1 || r.Proficiency > 5 || r.YearsOfExperience < 0 || r.YearsOfExperience > 80 ||
                r.LastUsedDate > DateOnly.FromDateTime(DateTime.UtcNow))
                throw new EmployeeProblem(400, "Invalid skill proficiency, experience or last-used date.");
            var item = entryId == 0
                ? new EmployeeSkill { TenantId = actor.TenantId, EmployeeId = e.Id }
                : await Entry<EmployeeSkill>(e, entryId, t);
            var name = r.SkillName?.Trim().ToLowerInvariant();
            var candidates = await db.Skills
                .Where(x => x.IsActive && (r.SkillId.HasValue ? x.Id == r.SkillId.Value : x.Name.ToLower() == name))
                .Take(2).ToListAsync(t);
            if (candidates.Count != 1) throw new EmployeeProblem(400, "Select an active skill from the skill master.");
            var skill = candidates[0];
            if (await db.Set<EmployeeSkill>()
                    .AnyAsync(
                        x => x.TenantId == actor.TenantId && x.EmployeeId == e.Id && x.Id != entryId &&
                             (x.SkillId == skill.Id || x.SkillName.ToLower() == skill.Name.ToLower()), t))
                throw new EmployeeProblem(409, "This skill is already on the employee profile.");
            item.SkillId = skill.Id;

            item.SkillName = skill.Name;
            item.Category = skill.Category;
            item.Proficiency = r.Proficiency;
            item.YearsOfExperience = r.YearsOfExperience;
            item.LastUsedDate = r.LastUsedDate;
            if (entryId == 0) db.Add(item);
            return item;
        }, ct);
    }

    public Task<SectionChangeDto> DeleteSkillAsync(long id, long entryId, CancellationToken ct)
    {
        return Delete<EmployeeSkill>(id, "EMPLOYEE_SKILL_UPDATED", entryId, ct);
    }


    public async Task<IReadOnlyList<EmployeeEducationDto>> EducationAsync(long id, CancellationToken ct)
    {
        return (await List<EmployeeEducation>(id, ct)).Select(ToDto).ToList();
    }

    public Task<SectionChangeDto> SaveEducationAsync(long id, long entryId, EducationRequest r, CancellationToken ct)
    {
        return Mutate(id, "EMPLOYEE_EDUCATION_UPDATED", async (e, t) =>
        {
            EmployeeAccess.Dates(r.StartDate, r.EndDate);
            var item = entryId == 0
                ? new EmployeeEducation { TenantId = actor.TenantId, EmployeeId = e.Id }
                : await Entry<EmployeeEducation>(e, entryId, t);
            item.Institution = r.Institution;
            item.Qualification = r.Qualification;
            item.FieldOfStudy = r.FieldOfStudy?.Trim();
            item.StartDate = r.StartDate;
            item.EndDate = r.EndDate;
            if (entryId == 0) db.Add(item);
            return item;
        }, ct);
    }

    public Task<SectionChangeDto> DeleteEducationAsync(long id, long entryId, CancellationToken ct)
    {
        return Delete<EmployeeEducation>(id, "EMPLOYEE_EDUCATION_UPDATED", entryId, ct);
    }


    public async Task<IReadOnlyList<EmployeeCertificationDto>> CertificationsAsync(long id, CancellationToken ct)
    {
        return (await List<EmployeeCertification>(id, ct)).Select(ToDto).ToList();
    }

    public Task<SectionChangeDto> SaveCertificationAsync(long id, long entryId, CertificationRequest r,
        CancellationToken ct)
    {
        return Mutate(id, "EMPLOYEE_CERTIFICATION_UPDATED", async (e, t) =>
        {
            EmployeeAccess.Dates(r.IssuedDate, r.ExpiryDate);
            EmployeeAccess.Url(r.CredentialUrl);
            var item = entryId == 0
                ? new EmployeeCertification { TenantId = actor.TenantId, EmployeeId = e.Id }
                : await Entry<EmployeeCertification>(e, entryId, t);
            item.Name = r.Name;
            item.Issuer = r.Issuer;
            item.IssuedDate = r.IssuedDate;
            item.ExpiryDate = r.ExpiryDate;
            item.CredentialUrl = r.CredentialUrl;
            item.CredentialId = r.CredentialId?.Trim();
            item.FileUrl = r.FileUrl;
            item.FileName = r.FileName?.Trim();
            if (entryId == 0) db.Add(item);
            return item;
        }, ct);
    }


    public Task<SectionChangeDto> DeleteCertificationAsync(long id, long entryId, CancellationToken ct)
    {
        return Mutate(id, "EMPLOYEE_CERTIFICATION_UPDATED", async (e, t) =>
        {
            var item = await Entry<EmployeeCertification>(e, entryId, t);
            if (!string.IsNullOrEmpty(item.FileUrl))
            {
                var lastPart = Path.GetFileName(item.FileUrl);
                if (!string.IsNullOrEmpty(lastPart))
                    try
                    {
                        await storage.Remove($"certifications/{actor.TenantId}/{e.Id}/{lastPart}",
                            CancellationToken.None);
                    }
                    catch
                    {
                    }
            }

            db.Remove(item);
            return new DeletedEntryDto(entryId);
        }, ct);
    }


    public async Task<CertificateUploadDto> UploadCertificateAsync(long id, IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) throw new EmployeeProblem(400, "Please select a file to upload.");
        if (file.Length > 10 * 1024 * 1024) throw new EmployeeProblem(400, "File size must not exceed 10 MB.");
        var e = await Employee(id, ct);
        await access.RequireProfile(e, true, ct);
        access.CheckVersion(e);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowed = new[] { ".pdf", ".png", ".jpg", ".jpeg", ".webp" };
        if (!allowed.Contains(ext)) throw new EmployeeProblem(400, "Only PDF, PNG, JPG, and WebP files are allowed.");
        var fileId = Guid.NewGuid();
        var key = $"certifications/{actor.TenantId}/{e.Id}/{fileId:N}{ext}";
        using var stream = file.OpenReadStream();
        await storage.Put(key, stream, file.Length, ct, ContentType(ext));
        return new CertificateUploadDto($"/api/employees/{e.Id}/certifications/file/{fileId:N}{ext}", file.FileName);
    }


    public async Task<CertificateFileDto?> CertificateFileAsync(long id, string fileId, CancellationToken ct)
    {
        var e = await Employee(id, ct);
        await access.RequireProfile(e, false, ct);
        var certs = await db.Set<EmployeeCertification>().AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId && x.EmployeeId == e.Id).ToListAsync(ct);
        var matched = certs.FirstOrDefault(x =>
            x.FileUrl != null && x.FileUrl.EndsWith(fileId, StringComparison.OrdinalIgnoreCase));
        if (matched == null) return null;
        var key = $"certifications/{actor.TenantId}/{e.Id}/{fileId}";
        var ext = Path.GetExtension(fileId).ToLowerInvariant();
        var bytes = await storage.Get(key, ct);
        return new CertificateFileDto(bytes, ContentType(ext), matched.FileName ?? $"certification{ext}");
    }

    private static string ContentType(string ext)
    {
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }


    public async Task<IReadOnlyList<EmployeeExperienceDto>> ExperienceAsync(long id, CancellationToken ct)
    {
        return (await List<EmployeeExperience>(id, ct)).Select(ToDto).ToList();
    }

    public Task<SectionChangeDto> SaveExperienceAsync(long id, long entryId, ExperienceRequest r, CancellationToken ct)
    {
        return Mutate(id, "EMPLOYEE_EXPERIENCE_UPDATED", async (e, t) =>
        {
            EmployeeAccess.Dates(r.StartDate, r.EndDate);
            var item = entryId == 0
                ? new EmployeeExperience { TenantId = actor.TenantId, EmployeeId = e.Id }
                : await Entry<EmployeeExperience>(e, entryId, t);
            item.Company = r.Company;
            item.Title = r.Title;
            item.Description = r.Description;
            item.StartDate = r.StartDate;
            item.EndDate = r.EndDate;
            if (entryId == 0) db.Add(item);
            return item;
        }, ct);
    }

    public Task<SectionChangeDto> DeleteExperienceAsync(long id, long entryId, CancellationToken ct)
    {
        return Delete<EmployeeExperience>(id, "EMPLOYEE_EXPERIENCE_UPDATED", entryId, ct);
    }


    public async Task<IReadOnlyList<EmployeeLanguageDto>> LanguagesAsync(long id, CancellationToken ct)
    {
        return (await List<EmployeeLanguage>(id, ct)).Select(ToDto).ToList();
    }

    public Task<SectionChangeDto> SaveLanguageAsync(long id, long entryId, LanguageRequest r, CancellationToken ct)
    {
        return Mutate(id, "EMPLOYEE_LANGUAGES_UPDATED", async (e, t) =>
        {
            var item = entryId == 0
                ? new EmployeeLanguage { TenantId = actor.TenantId, EmployeeId = e.Id }
                : await Entry<EmployeeLanguage>(e, entryId, t);
            item.Name = r.Name;
            item.Proficiency = r.Proficiency;
            if (entryId == 0) db.Add(item);
            return item;
        }, ct);
    }

    public Task<SectionChangeDto> DeleteLanguageAsync(long id, long entryId, CancellationToken ct)
    {
        return Delete<EmployeeLanguage>(id, "EMPLOYEE_LANGUAGES_UPDATED", entryId, ct);
    }


    public async Task<IReadOnlyList<EmployeeCompensationDto>> CompensationAsync(long id, CancellationToken ct)
    {
        var e = await Employee(id, ct);
        return (await db.Set<EmployeeCompensation>().AsNoTracking()
                .Where(x => x.TenantId == actor.TenantId && x.EmployeeId == e.Id).OrderBy(x => x.Id).ToListAsync(ct))
            .Select(ToDto).ToList();
    }

    public Task<SectionChangeDto> SaveCompensationAsync(long id, long entryId, CompensationRequest r,
        CancellationToken ct)
    {
        return Mutate(id, "EMPLOYEE_COMPENSATION_UPDATED", async (e, t) =>
        {
            EmployeeAccess.Dates(r.EffectiveFrom, r.EffectiveTo);
            if (r.Ctc < 0 || r.Bonus < 0 || !Regex.IsMatch(r.Currency, "^[A-Z]{3}$"))
                throw new EmployeeProblem(400, "Invalid compensation or currency.");
            if (await db.Set<EmployeeCompensation>().AnyAsync(
                    x => x.TenantId == actor.TenantId && x.EmployeeId == e.Id && x.Id != entryId &&
                         (x.EffectiveTo == null || x.EffectiveTo >= r.EffectiveFrom) &&
                         (r.EffectiveTo == null || x.EffectiveFrom <= r.EffectiveTo), t))
                throw new EmployeeProblem(409, "Compensation effective periods cannot overlap.");
            var item = entryId == 0
                ? new EmployeeCompensation { TenantId = actor.TenantId, EmployeeId = e.Id }
                : await Entry<EmployeeCompensation>(e, entryId, t);
            item.Ctc = r.Ctc;
            item.Bonus = r.Bonus;
            item.Currency = r.Currency;
            item.EffectiveFrom = r.EffectiveFrom;
            item.EffectiveTo = r.EffectiveTo;
            if (entryId == 0) db.Add(item);
            return item;
        }, ct, true);
    }

    public Task<SectionChangeDto> DeleteCompensationAsync(long id, long entryId, CancellationToken ct)
    {
        return Delete<EmployeeCompensation>(id, "EMPLOYEE_COMPENSATION_UPDATED", entryId, ct, true);
    }


    private async Task<SectionChangeDto> Mutate(long id, string action,
        Func<Employee, CancellationToken, Task<object>> update, CancellationToken ct, bool sensitive = false)
    {
        object result = null!;
        Employee e = null!;
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            e = await Employee(id, t);
            if (!sensitive) await access.RequireProfile(e, true, t);
            access.CheckVersion(e);
            result = await update(e, t);
            e.UpdatedAt = DateTimeOffset.UtcNow;
            db.ChangeTracker.DetectChanges();
            var before = sensitive
                ? null
                : db.ChangeTracker.Entries()
                    .Where(x => x.Entity is not Domain.Employees.Employee &&
                                (x.State == EntityState.Modified || x.State == EntityState.Deleted))
                    .Select(x => x.OriginalValues.ToObject()).FirstOrDefault();
            await db.SaveChangesAsync(t);
            await events.Write(e, action, before, sensitive ? new { section = action, employeeId = e.Id } : result, t);
        }, ct);
        return new SectionChangeDto(Present(result), e.Version);
    }

    private Task<SectionChangeDto> Delete<T>(long id, string action, long entryId, CancellationToken ct,
        bool sensitive = false) where T : class
    {
        return Mutate(id, action, async (e, t) =>
        {
            db.Remove(await Entry<T>(e, entryId, t));
            return new DeletedEntryDto(entryId);
        }, ct, sensitive);
    }


    private async Task<T> Entry<T>(Employee e, long entryId, CancellationToken ct) where T : class
    {
        return await db.Set<T>()
                   .SingleOrDefaultAsync(
                       x => EF.Property<long>(x, "Id") == entryId && EF.Property<long>(x, "EmployeeId") == e.Id &&
                            EF.Property<long>(x, "TenantId") == actor.TenantId, ct)
               ?? throw new EmployeeProblem(404, "Entry not found.");
    }


    private async Task<List<T>> List<T>(long id, CancellationToken ct) where T : class
    {
        var e = await Employee(id, ct);
        await access.RequireProfile(e, false, ct);
        return await db.Set<T>().AsNoTracking()
            .Where(x => EF.Property<long>(x, "TenantId") == actor.TenantId &&
                        EF.Property<long>(x, "EmployeeId") == e.Id).OrderBy(x => EF.Property<long>(x, "Id"))
            .ToListAsync(ct);
    }


    private static object Present(object result)
    {
        return result switch
        {
            EmployeeSkill s => new SavedEmployeeSkillDto(s.Id, s.TenantId, s.Version, s.EmployeeId, s.SkillId,
                s.Skill is null
                    ? null
                    : new SkillDto(s.Skill.Id, s.Skill.Code, s.Skill.Name, s.Skill.Category, s.Skill.Description,
                        s.Skill.IsActive, s.Skill.CreatedAt, s.Skill.UpdatedAt), s.SkillName, s.Category, s.Proficiency,
                s.YearsOfExperience, s.LastUsedDate),
            EmployeeEducation x => ToDto(x),
            EmployeeCertification x => ToDto(x),
            EmployeeExperience x => ToDto(x),
            EmployeeLanguage x => ToDto(x),
            EmployeeCompensation x => ToDto(x),
            _ => result
        };
    }

    private static EmployeeEducationDto ToDto(EmployeeEducation x)
    {
        return new EmployeeEducationDto(x.FieldOfStudy, x.Id, x.TenantId, x.Version, x.EmployeeId, x.Institution,
            x.Qualification,
            x.StartDate, x.EndDate);
    }

    private static EmployeeCertificationDto ToDto(EmployeeCertification x)
    {
        return new EmployeeCertificationDto(x.CredentialId, x.Id, x.TenantId, x.Version, x.EmployeeId, x.Name, x.Issuer,
            x.IssuedDate,
            x.ExpiryDate, x.CredentialUrl, x.FileUrl, x.FileName);
    }

    private static EmployeeExperienceDto ToDto(EmployeeExperience x)
    {
        return new EmployeeExperienceDto(x.Id, x.TenantId, x.Version, x.EmployeeId, x.Company, x.Title, x.Description,
            x.StartDate,
            x.EndDate);
    }

    private static EmployeeLanguageDto ToDto(EmployeeLanguage x)
    {
        return new EmployeeLanguageDto(x.Id, x.TenantId, x.Version, x.EmployeeId, x.Name, x.Proficiency);
    }

    private static EmployeeCompensationDto ToDto(EmployeeCompensation x)
    {
        return new EmployeeCompensationDto(x.Id, x.TenantId, x.Version, x.EmployeeId, x.Ctc, x.Bonus, x.Currency,
            x.EffectiveFrom,
            x.EffectiveTo);
    }
}