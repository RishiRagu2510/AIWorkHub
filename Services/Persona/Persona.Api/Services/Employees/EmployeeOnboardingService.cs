using Persona.Contracts;
using Persona.Domain.Employees;
using IdentityTenant.Domain.Enums;
using Persona.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class EmployeeOnboardingService(
    PersonaDbContext db,
    EmployeeAccess access,
    ITenantContext actor,
    EmployeeOnboardingValidation validation)
{
    public async Task<OnboardingValidationDto> Validate(EmployeeOnboardingRequest proposal, CancellationToken ct)
    {
        var validated = await validation.Validate(proposal, ct);
        var assignedRoles = from ur in db.UserRoles
            join role in db.Roles on ur.RoleId equals role.Id
            where ur.TenantId == actor.TenantId && role.TenantId == actor.TenantId
            select new { ur.UserId, RoleId = role.Id, role.Code };
        var permissions = from role in assignedRoles
            join rp in db.RolePermissions on role.RoleId equals rp.RoleId
            join permission in db.Permissions on rp.PermissionId equals permission.Id
            select new { role.UserId, permission.Code };
        var reviewers = await db.Users.Where(u => u.TenantId == actor.TenantId && u.Status == UserStatus.Active
                                                                               && assignedRoles.Any(r =>
                                                                                   r.UserId == u.Id &&
                                                                                   (r.Code == "ADMIN" ||
                                                                                       r.Code == "HR" ||
                                                                                       r.Code == "HUMAN_RESOURCE"))
                                                                               && permissions.Any(p =>
                                                                                   p.UserId == u.Id &&
                                                                                   p.Code == "EMPLOYEE_CREATE") &&
                                                                               permissions.Any(p =>
                                                                                   p.UserId == u.Id &&
                                                                                   p.Code == "ISSUE_VIEW"))
            .Select(u => new { u.Id, roles = assignedRoles.Where(r => r.UserId == u.Id).Select(r => r.Code).ToArray() })
            .ToListAsync(ct);
        return new OnboardingValidationDto(validated,
            reviewers.Select(r => new OnboardingReviewerDto(r.Id, r.roles)).ToList());
    }

    public async Task<OnboardingOptionsDto> Options(CancellationToken ct)
    {
        var references = await db.Set<EmployeeReference>().AsNoTracking().Where(r => r.TenantId == actor.TenantId)
            .Select(r => new OnboardingReferenceDto(r.Id, r.Kind, r.Name)).ToListAsync(ct);
        var profiles = await db.CareerProfiles.Where(p => p.IsActive)
            .Select(p => new OnboardingProfileDto(p.Id, p.Name)).ToListAsync(ct);
        var levels = await db.CareerLevels.Where(l => l.IsActive && l.CareerProfile.IsActive && l.Grade.IsActive)
            .Select(l => new OnboardingLevelDto(l.Id, l.CareerProfileId, l.GradeId, l.Grade.Code, l.CareerTitle))
            .ToListAsync(ct);
        var managers = await access.ManagerCandidates()
            .Select(e => new OnboardingManagerDto(e.Id, e.FirstName, e.LastName, e.EmployeeCode, e.WorkEmail))
            .ToListAsync(ct);
        var teams = await db.Teams.Where(t => t.TenantId == actor.TenantId && t.Status == TeamStatus.Active)
            .Select(t => new OnboardingTeamDto(t.Id, t.Name)).ToListAsync(ct);
        return new OnboardingOptionsDto(references, profiles, levels, managers, teams,
            ["FULL_TIME", "PART_TIME", "CONTRACT", "INTERN"], ["ACTIVE", "ON_LEAVE", "TERMINATED", "INACTIVE"],
            ["Intern", "Junior", "Senior", "Lead", "Staff", "Principal"]);
    }
}