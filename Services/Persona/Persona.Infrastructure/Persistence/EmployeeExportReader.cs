using System.Text.Json;
using Persona.Domain.Employees;
using Microsoft.EntityFrameworkCore;

namespace Persona.Infrastructure.Persistence;


public sealed record EmployeeExport(IReadOnlyList<object> Items, int TotalCount, bool Truncated);






public static class EmployeeExportReader
{
    public const int MaxEmployees = 500;

    private sealed class SnapshotRow
    {
        public long EmployeeId { get; set; }
        public string Snapshot { get; set; } = "";
    }

    public static async Task<EmployeeExport> ReadAsync(PersonaDbContext db, long tenantId, string? status, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, MaxEmployees);
        var query = db.Set<Employee>().AsNoTracking().Where(e => e.TenantId == tenantId && !e.IsDeleted);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.EmploymentStatus == status);

        var total = await query.CountAsync(ct);
        var people = await query.OrderBy(e => e.LastName).ThenBy(e => e.Id).Take(limit).ToListAsync(ct);
        var ids = people.Select(p => p.Id).ToList();

        
        var skills = (await db.Set<EmployeeSkill>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.EmployeeId)).OrderBy(x => x.Id)
            .Select(x => new { x.EmployeeId, skillName = x.SkillId == null ? x.SkillName : x.Skill!.Name, x.Proficiency, x.YearsOfExperience })
            .ToListAsync(ct)).ToLookup(x => x.EmployeeId);
        var experience = (await db.Set<EmployeeExperience>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.EmployeeId)).OrderBy(x => x.Id)
            .Select(x => new { x.EmployeeId, x.Company, x.Title, x.StartDate, x.EndDate }).ToListAsync(ct)).ToLookup(x => x.EmployeeId);
        var education = (await db.Set<EmployeeEducation>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.EmployeeId)).OrderBy(x => x.Id)
            .Select(x => new { x.EmployeeId, x.Institution, x.Qualification, x.FieldOfStudy, x.StartDate, x.EndDate }).ToListAsync(ct)).ToLookup(x => x.EmployeeId);
        var certifications = (await db.Set<EmployeeCertification>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.EmployeeId)).OrderBy(x => x.Id)
            .Select(x => new { x.EmployeeId, x.Name, x.Issuer }).ToListAsync(ct)).ToLookup(x => x.EmployeeId);
        var languages = (await db.Set<EmployeeLanguage>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.EmployeeId)).OrderBy(x => x.Id)
            .Select(x => new { x.EmployeeId, x.Name, x.Proficiency }).ToListAsync(ct)).ToLookup(x => x.EmployeeId);
        var compensation = (await db.Set<EmployeeCompensation>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && ids.Contains(x.EmployeeId)).OrderBy(x => x.Id)
            .Select(x => new { x.EmployeeId, x.Ctc, x.Bonus, x.Currency, x.EffectiveFrom, x.EffectiveTo }).ToListAsync(ct)).ToLookup(x => x.EmployeeId);

        
        
        var snapshots = (await db.Database.SqlQuery<SnapshotRow>(
                $"SELECT DISTINCT ON (employee_id) employee_id AS \"EmployeeId\", snapshot::text AS \"Snapshot\" FROM employee_career_history WHERE tenant_id={tenantId} ORDER BY employee_id, id DESC")
            .ToListAsync(ct)).ToDictionary(r => r.EmployeeId, r => r.Snapshot);

        object? Career(long employeeId)
        {
            if (!snapshots.TryGetValue(employeeId, out var text)) return null;
            try { return JsonSerializer.Deserialize<JsonElement>(text); }
            catch (JsonException) { return null; }
        }

        var items = people.Select(e => (object)new
        {
            e.Id,
            e.EmployeeCode,
            e.FirstName,
            e.LastName,
            e.WorkEmail,
            e.Phone,
            e.EmploymentType,
            e.EmploymentStatus,
            e.JoiningDate,
            e.ManagerEmployeeId,
            e.DepartmentId,
            e.JobTitleId,
            e.DesignationId,
            e.GradeId,
            e.LocationId,
            career = Career(e.Id),
            skills = skills[e.Id].ToList(),
            experience = experience[e.Id].ToList(),
            education = education[e.Id].ToList(),
            certifications = certifications[e.Id].ToList(),
            languages = languages[e.Id].ToList(),
            compensation = compensation[e.Id].ToList()
        }).ToList();

        return new EmployeeExport(items, total, total > people.Count);
    }
}
