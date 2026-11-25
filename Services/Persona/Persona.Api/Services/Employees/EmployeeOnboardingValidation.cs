using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Persona.Contracts;
using Persona.Domain.Employees;
using IdentityTenant.Domain.Enums;
using Persona.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class EmployeeOnboardingValidation(
    PersonaDbContext db,
    EmployeeAccess access,
    ITenantContext actor)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<object> Validate(EmployeeOnboardingRequest proposal, CancellationToken ct)
    {
        EmployeeRequest draft;
        try
        {
            draft = proposal.Employee.Deserialize<EmployeeRequest>(Json) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new EmployeeProblem(400, "Provide valid employee information.");
        }

        var errors = new List<ValidationResult>();
        var candidate = draft with
        {
            UserId = 1,
            EmployeeCode = string.IsNullOrWhiteSpace(draft.EmployeeCode) ? "PENDING" : draft.EmployeeCode
        };
        if (!Validator.TryValidateObject(candidate, new ValidationContext(candidate), errors, true))
            throw new EmployeeProblem(400, string.Join(" ", errors.Select(e => e.ErrorMessage)));
        if (string.IsNullOrWhiteSpace(draft.FirstName) || string.IsNullOrWhiteSpace(draft.LastName) ||
            !new EmailAddressAttribute().IsValid(proposal.WorkEmail))
            throw new EmployeeProblem(400, "Name and work email are required.");
        EmployeeAccess.Dates(draft.JoiningDate, draft.EndDate);
        EmployeeAccess.Url(draft.ProfilePhotoUrl);
        if (draft.EmploymentType is not ("FULL_TIME" or "PART_TIME" or "CONTRACT" or "INTERN") ||
            draft.EmploymentStatus is not ("ACTIVE" or "ON_LEAVE" or "TERMINATED" or "INACTIVE"))
            throw new EmployeeProblem(400, "Invalid employment type or status.");
        if (draft.EmploymentStatus is "ACTIVE" or "ON_LEAVE" && draft.EndDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new EmployeeProblem(400, "Active employment cannot have a past end date.");
        foreach (var pair in new[]
                 {
                     (draft.DepartmentId, "DEPARTMENT"), (draft.DisciplineId, "DISCIPLINE"),
                     (draft.JobTitleId, "JOB_TITLE"), (draft.DesignationId, "DESIGNATION"), (draft.GradeId, "GRADE"),
                     (draft.LocationId, "LOCATION")
                 }) await access.Reference(pair.Item1, pair.Item2, ct);
        var level = await db.CareerLevels.Include(l => l.CareerProfile).Include(l => l.Grade).SingleOrDefaultAsync(
                        l => l.Id == draft.CareerLevelId && l.CareerProfileId == draft.CareerProfileId && l.IsActive &&
                             l.CareerProfile.IsActive && l.Grade.IsActive, ct) ??
                    throw new EmployeeProblem(400, "Select a career profile and its grade/job title.");
        if (draft.CatalogGradeId != level.GradeId)
            throw new EmployeeProblem(400, "Grade must match the selected job title.");
        if (draft.DepartmentId is null && draft.CatalogDepartmentId != level.CareerProfileId)
            throw new EmployeeProblem(400, "Department is required.");
        if (draft.CatalogDepartmentId.HasValue && draft.CatalogDepartmentId != level.CareerProfileId)
            throw new EmployeeProblem(400, "Invalid department mapping.");

        if (!string.IsNullOrWhiteSpace(draft.Designation) &&
            draft.Designation is not ("Intern" or "Junior" or "Senior" or "Lead" or "Staff" or "Principal"))
            throw new EmployeeProblem(400, "Select a valid designation.");
        var manager = await access.ManagerCandidates().SingleOrDefaultAsync(e => e.Id == draft.ManagerEmployeeId, ct) ??
                      throw new EmployeeProblem(400,
                          "Select an active, eligible reporting manager in your organization.");
        if (string.Equals(manager.WorkEmail, proposal.WorkEmail.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new EmployeeProblem(400, "An employee cannot be their own manager.");
        if (proposal.TeamId.HasValue && !await db.Teams.AnyAsync(
                t => t.Id == proposal.TeamId && t.TenantId == actor.TenantId && t.Status == TeamStatus.Active, ct))
            throw new EmployeeProblem(400, "Select a team in your organization.");
        if (await access.Query.AnyAsync(e => e.WorkEmail.ToLower() == proposal.WorkEmail.Trim().ToLower(), ct))
            throw new EmployeeProblem(409, "An employee with this work email already exists.");

        var normalized = proposal with
        {
            WorkEmail = proposal.WorkEmail.Trim().ToLowerInvariant(),
            Employee = JsonSerializer.SerializeToElement(draft with { UserId = 0 }, Json)
        };
        var refs = await db.Set<EmployeeReference>().Where(r => r.TenantId == actor.TenantId).ToListAsync(ct);
        return new
        {
            proposal = normalized,
            display = new
            {
                department = draft.DepartmentId.HasValue
                    ? refs.First(r => r.Id == draft.DepartmentId).Name
                    : level.CareerProfile.Name,
                careerProfile = level.CareerProfile.Name, grade = level.Grade.Code, jobTitle = level.CareerTitle,
                designation = level.Grade.Code switch
                {
                    "T0" => "Intern", "T1" => "Junior", "T2" => "No designation prefix", "T3" => "Senior",
                    "T4" => "Staff",
                    _ => draft.DesignationId.HasValue
                        ? refs.First(r => r.Id == draft.DesignationId).Name
                        : draft.Designation
                },
                manager = new
                    { manager.Id, manager.EmployeeCode, manager.FirstName, manager.LastName, manager.WorkEmail },
                team = await db.Teams
                    .Where(t => t.TenantId == actor.TenantId && t.Status == TeamStatus.Active &&
                                t.Id == proposal.TeamId).Select(t => t.Name).SingleOrDefaultAsync(ct)
            }
        };
    }
}