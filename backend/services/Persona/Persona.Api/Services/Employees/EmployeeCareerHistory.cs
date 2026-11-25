using System.Text.Json;
using Persona.Domain.Employees;
using Persona.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class CareerHistoryRow
{
    public long Id { get; set; }
    public DateOnly? EffectiveDate { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public long RecordedBy { get; set; }
    public string ChangeType { get; set; } = "";
    public string Snapshot { get; set; } = "";
    public string? Note { get; set; }
}

public sealed class EmployeeCareerHistory(PersonaDbContext db, ITenantContext actor)
{
    public static string Key(Employee e)
    {
        return JsonSerializer.Serialize(new
            { e.GradeId, e.DesignationId, e.DepartmentId, e.JobTitleId, e.CareerProfileId, e.CareerLevelId });
    }

    public async Task<string> Snapshot(Employee e, CancellationToken ct)
    {
        var refs = await db.Set<EmployeeReference>().Where(r => r.TenantId == e.TenantId)
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        string? Name(long? id)
        {
            return id.HasValue ? refs.GetValueOrDefault(id.Value) : null;
        }

        var level = e.CareerLevelId.HasValue
            ? await db.CareerLevels.Include(l => l.Grade).SingleAsync(l => l.Id == e.CareerLevelId, ct)
            : null;
        var profile = e.CareerProfileId.HasValue
            ? await db.CareerProfiles.Where(p => p.Id == e.CareerProfileId).Select(p => p.Name).SingleAsync(ct)
            : null;
        return JsonSerializer.Serialize(new
        {
            grade = Name(e.GradeId) ?? level?.Grade.Code, designation = Name(e.DesignationId),
            department = Name(e.DepartmentId) ?? profile, jobTitle = level?.CareerTitle ?? Name(e.JobTitleId)
        });
    }

    public Task<List<CareerHistoryRow>> Read(long employeeId, CancellationToken ct)
    {
        return db.Database.SqlQuery<CareerHistoryRow>(
                $"SELECT id AS \"Id\",effective_date AS \"EffectiveDate\",recorded_at AS \"RecordedAt\",recorded_by AS \"RecordedBy\",change_type AS \"ChangeType\",snapshot::text AS \"Snapshot\",note AS \"Note\" FROM employee_career_history WHERE tenant_id={actor.TenantId} AND employee_id={employeeId} ORDER BY id DESC")
            .ToListAsync(ct);
    }

    public Task Add(Employee e, DateOnly? date, string type, string snapshot, string? note, CancellationToken ct)
    {
        return db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO employee_career_history(tenant_id,employee_id,effective_date,recorded_by,change_type,snapshot,note) VALUES({e.TenantId},{e.Id},{date},{actor.UserId},{type},{snapshot}::jsonb,{note})",
            ct);
    }

    public async Task Changed(Employee e, string before, DateOnly? effectiveDate, string? note, CancellationToken ct)
    {
        var rows = await Read(e.Id, ct);
        if (!effectiveDate.HasValue)
            throw new EmployeeProblem(400,
                "An effective date is required when changing grade, designation, department or job title.");
        if (e.EndDate.HasValue && effectiveDate > e.EndDate)
            throw new EmployeeProblem(400, "Career effective date cannot be after employment ended.");
        if (effectiveDate < e.JoiningDate || effectiveDate > DateOnly.FromDateTime(DateTime.UtcNow) ||
            rows.Any(r => r.EffectiveDate > effectiveDate))
            throw new EmployeeProblem(400,
                "Effective date must be on or after joining and the latest career change, and cannot be in the future.");
        if (rows.Count == 0)
            await Add(e, null, "BASELINE", before,
                "Previously recorded career details; original effective date unknown.", ct);
        await Add(e, effectiveDate, "CAREER_CHANGE", await Snapshot(e, ct), note, ct);
    }
}