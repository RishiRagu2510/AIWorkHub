using System.Text.Json;
using System.Text.RegularExpressions;
using Authentication;
using Persona.Contracts;
using Persona.Domain.Employees;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using Persona.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class EmployeeService(
    PersonaDbContext db,
    ITenantContext actor,
    EmployeeAccess access,
    EmployeeEvents events,
    EmployeeCareerHistory history,
    EmployeeOnboardingValidation validation)
{
    private static readonly string[] ReferenceKinds =
        ["DEPARTMENT", "DISCIPLINE", "JOB_TITLE", "DESIGNATION", "GRADE", "LOCATION"];


    public async Task<IReadOnlyList<ManagerOptionDto>> ManagerOptionsAsync(long? employeeId, CancellationToken ct)
    {
        var eligible = await access.ManagerCandidates().Select(e => e.Id).ToListAsync(ct);
        var rows = await access.Query.AsNoTracking().Select(e => new
        {
            e.Id, e.UserId, e.FirstName, e.LastName, e.EmployeeCode, e.WorkEmail, e.ManagerEmployeeId,
            e.EmploymentStatus
        }).ToListAsync(ct);
        var excluded = new HashSet<long>();
        if (employeeId.HasValue)
        {
            excluded.Add(employeeId.Value);
            bool changed;
            do
            {
                changed = false;
                foreach (var row in rows)
                    if (row.ManagerEmployeeId.HasValue && excluded.Contains(row.ManagerEmployeeId.Value))
                        changed |= excluded.Add(row.Id);
            } while (changed);
        }

        return rows.Where(e => eligible.Contains(e.Id) && !excluded.Contains(e.Id)).OrderBy(e => e.FirstName)
            .ThenBy(e => e.LastName)
            .Select(e => new ManagerOptionDto(e.Id, e.UserId, e.FirstName, e.LastName, e.EmployeeCode, e.WorkEmail))
            .ToList();
    }


    public async Task<LoginUserPageDto> LoginUsersAsync(string? search, int page, CancellationToken ct)
    {
        if (page < 1 || page > 100000) throw new EmployeeProblem(400, "Invalid page.");
        var q = db.Users.AsNoTracking().Where(u =>
            u.TenantId == actor.TenantId && u.Status == UserStatus.Active &&
            !db.Set<Employee>().Any(e => e.TenantId == actor.TenantId && e.UserId == u.Id));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            q = q.Where(u => u.DisplayName.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        var count = await q.CountAsync(ct);
        var items = await q.OrderBy(u => u.DisplayName).ThenBy(u => u.Id).Skip((page - 1) * 25).Take(25)
            .Select(u => new LoginUserDto(u.Id, u.DisplayName, u.Email)).ToListAsync(ct);
        return new LoginUserPageDto(items, page, 25, count, (int)Math.Ceiling(count / 25d));
    }


    public async Task<EmployeeListDto?> ListAsync(EmployeeSearch q, CancellationToken ct)
    {
        if (q.Page < 1 || q.Page > 100000 || q.PageSize < 1 || q.PageSize > 100)
            throw new EmployeeProblem(400, "Invalid pagination.");
        var query = access.Query.AsNoTracking();
        if (q.Team)
        {
            if (!access.Can("EMPLOYEE_VIEW_TEAM")) return null;
            var me = await access.Me(ct);
            query = query.Where(e => e.ManagerEmployeeId == me.Id);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var term = q.Search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.FirstName.ToLower().Contains(term) || e.LastName.ToLower().Contains(term) ||
                e.EmployeeCode.ToLower().Contains(term) || e.WorkEmail.ToLower().Contains(term));
        }

        if (q.DepartmentId.HasValue) query = query.Where(e => e.DepartmentId == q.DepartmentId);
        if (q.DisciplineId.HasValue) query = query.Where(e => e.DisciplineId == q.DisciplineId);
        if (q.GradeId.HasValue) query = query.Where(e => e.GradeId == q.GradeId);
        if (q.ManagerEmployeeId.HasValue) query = query.Where(e => e.ManagerEmployeeId == q.ManagerEmployeeId);
        if (q.LocationId.HasValue) query = query.Where(e => e.LocationId == q.LocationId);
        if (!string.IsNullOrWhiteSpace(q.EmploymentStatus))
            query = query.Where(e => e.EmploymentStatus == q.EmploymentStatus);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(e => e.LastName).ThenBy(e => e.Id).Skip((q.Page - 1) * q.PageSize)
            .Take(q.PageSize).ToListAsync(ct);


        var allCodes = await db.Set<Employee>().AsNoTracking().Where(e => e.TenantId == actor.TenantId)
            .Select(e => e.EmployeeCode).ToListAsync(ct);
        var maxCodeNum = 0;
        foreach (var code in allCodes)
        {
            var match = Regex.Match(code ?? "", @"EMP-(\d+)", RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var n) && n > maxCodeNum) maxCodeNum = n;
        }

        return new EmployeeListDto(rows.Select(e => Profile(e, false)).ToList(), q.Page, q.PageSize, count,
            (int)Math.Ceiling(count / (double)q.PageSize), maxCodeNum);
    }

    public async Task<OrgChartPageDto> OrgChartAsync(long? managerId, int page, string? roleLevel, CancellationToken ct)
    {
        if (page < 1 || page > 100000) throw new EmployeeProblem(400, "Invalid page.");
        if (managerId.HasValue) await access.Get(managerId.Value, ct);
        var q = access.Query;
        if (roleLevel is null)
        {
            q = q.Where(e => e.ManagerEmployeeId == managerId);
        }
        else
        {
            if (roleLevel is not ("ADMIN" or "MANAGER" or "DEVELOPER" or "OTHER"))
                throw new EmployeeProblem(400, "Invalid role level.");
            var admins = db.UserRoles
                .Where(ur => ur.TenantId == actor.TenantId && db.Roles.Any(r =>
                    r.Id == ur.RoleId && r.TenantId == actor.TenantId && r.Code == "ADMIN")).Select(ur => ur.UserId);
            var managers = db.UserRoles.Where(ur => ur.TenantId == actor.TenantId && db.Roles.Any(r =>
                    r.Id == ur.RoleId && r.TenantId == actor.TenantId && (r.Code == "MANAGER" ||
                                                                          r.Code == "PEOPLE_MANAGER" ||
                                                                          r.Code == "PROJECT_MANAGER" ||
                                                                          r.Code == "ENGINEERING_MANAGER")))
                .Select(ur => ur.UserId);
            var developers = db.UserRoles
                .Where(ur => ur.TenantId == actor.TenantId && db.Roles.Any(r =>
                    r.Id == ur.RoleId && r.TenantId == actor.TenantId && r.Code == "DEVELOPER"))
                .Select(ur => ur.UserId);
            q = roleLevel switch
            {
                "ADMIN" => q.Where(e => admins.Contains(e.UserId)),
                "MANAGER" => q.Where(e => !admins.Contains(e.UserId) && managers.Contains(e.UserId)),
                "DEVELOPER" => q.Where(e =>
                    !admins.Contains(e.UserId) && !managers.Contains(e.UserId) && developers.Contains(e.UserId)),
                _ => q.Where(e =>
                    !admins.Contains(e.UserId) && !managers.Contains(e.UserId) && !developers.Contains(e.UserId))
            };
        }

        var rows = await q.OrderBy(e => e.LastName).ThenBy(e => e.Id).Skip((page - 1) * 50).Take(50).Select(e => new
        {
            e.Id, e.FirstName, e.LastName, e.EmployeeCode, e.ManagerEmployeeId, e.JobTitleId, e.DepartmentId,
            e.LocationId, e.ProfilePhotoUrl,
            careerTitle = e.CareerLevel == null ? null : e.CareerLevel.CareerTitle,
            directReportCount = access.Query.Count(r => r.ManagerEmployeeId == e.Id),
            hasReports = access.Query.Any(r => r.ManagerEmployeeId == e.Id)
        }).ToListAsync(ct);
        var total = await q.CountAsync(ct);
        return new OrgChartPageDto(
            rows.Select(r => new OrgChartNodeDto(r.Id, r.FirstName, r.LastName, r.EmployeeCode, r.ManagerEmployeeId,
                r.JobTitleId, r.DepartmentId, r.LocationId, r.ProfilePhotoUrl, r.careerTitle, r.directReportCount,
                r.hasReports)).ToList(),
            page, 50, total);
    }


    public async Task<EmployeeProfileDto> GetAsync(long id, CancellationToken ct)
    {
        return await WithRolesAsync(await access.Get(id, ct), ct);
    }

    public async Task<EmployeeProfileDto> MeAsync(CancellationToken ct)
    {
        return await WithRolesAsync(await access.Me(ct), ct);
    }

    public async Task<IReadOnlyList<CareerHistoryEntryDto>> CareerHistoryAsync(long id, CancellationToken ct)
    {
        await access.Get(id, ct);
        return (await history.Read(id, ct)).Select(r => new CareerHistoryEntryDto(r.Id, r.EffectiveDate, r.RecordedAt,
            r.RecordedBy, r.ChangeType, JsonSerializer.Deserialize<JsonElement>(r.Snapshot), r.Note)).ToList();
    }


    public Task<EmployeeProfileDto> CreateAsync(EmployeeRequest r, CancellationToken ct)
    {
        return CreateEmployeeAsync(r, null, null, ct);
    }


    public Task<EmployeeProfileDto> CreateFromOnboardingAsync(EmployeeRequest r, long issueId,
        OnboardingApprovalClaims approval, CancellationToken ct)
    {
        var proposal = new EmployeeOnboardingRequest(approval.WorkEmail, default, TeamId: approval.TeamId);
        var workEmail = proposal.WorkEmail.Trim().ToLowerInvariant();
        return CreateEmployeeAsync(r, issueId, async (req, t) =>
        {
            await validation.Validate(
                proposal with { Employee = JsonSerializer.SerializeToElement(req, EmployeeOnboardingValidation.Json) },
                t);
            var user = await db.Users.FirstOrDefaultAsync(
                u => u.TenantId == actor.TenantId && u.Email.ToLower() == workEmail, t);
            if (user is null)
            {
                var name = string.IsNullOrWhiteSpace($"{req.FirstName} {req.LastName}".Trim())
                    ? workEmail
                    : $"{req.FirstName} {req.LastName}".Trim();
                user = new User(actor.TenantId, workEmail, name);
                user.VerifyEmail();
                db.Add(user);
                await db.SaveChangesAsync(t);
                var devRole =
                    await db.Roles.FirstOrDefaultAsync(ro => ro.TenantId == actor.TenantId && ro.Code == "DEVELOPER",
                        t);
                if (devRole is not null)
                {
                    db.Add(new UserRole(actor.TenantId, user.Id, devRole.Id));
                    await db.SaveChangesAsync(t);
                }
            }

            if (user.Status != UserStatus.Active)
                throw new EmployeeProblem(400, "The user account for this email is not active.");
            return req with { UserId = user.Id };
        }, ct);
    }

    private async Task<EmployeeProfileDto> CreateEmployeeAsync(EmployeeRequest r, long? onboardingId,
        Func<EmployeeRequest, CancellationToken, Task<EmployeeRequest>>? validate, CancellationToken ct)
    {
        Employee e = new() { TenantId = actor.TenantId };
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            if (onboardingId.HasValue)
            {
                var previous = await db.Database
                    .SqlQuery<long>(
                        $"SELECT employee_id AS \"Value\" FROM employee_onboarding_links WHERE tenant_id={actor.TenantId} AND issue_id={onboardingId.Value}")
                    .ToListAsync(t);
                if (previous.Count > 0)
                {
                    e = await access.Get(previous[0], t);
                    return;
                }
            }

            if (validate is not null) r = await validate(r, t);
            if (await db.Set<Employee>().AnyAsync(x => x.TenantId == actor.TenantId && x.UserId == r.UserId, t))
                throw new EmployeeProblem(409, "An employee profile already exists for this login account.",
                    "EMPLOYEE_ALREADY_EXISTS");
            await Apply(e, r, t);
            db.Add(e);
            await db.SaveChangesAsync(t);
            await history.Add(e, e.JoiningDate, "INITIAL", await history.Snapshot(e, t), null, t);
            await events.Write(e, "EMPLOYEE_CREATED", null, EmployeeEvents.Snapshot(e), t);
            if (onboardingId.HasValue)
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO employee_onboarding_links(tenant_id,issue_id,employee_id) VALUES({actor.TenantId},{onboardingId.Value},{e.Id})",
                    t);
        }, ct);
        return Profile(e, true);
    }

    public async Task<EmployeeProfileDto> UpdateAsync(long id, EmployeeRequest r, CancellationToken ct)
    {
        Employee e = null!;
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            e = await access.Get(id, t);
            access.CheckVersion(e);
            var before = EmployeeEvents.Snapshot(e);
            var manager = e.ManagerEmployeeId;
            var department = e.DepartmentId;
            var careerKey = EmployeeCareerHistory.Key(e);
            var careerBefore = await history.Snapshot(e, t);
            if (e.UserId != r.UserId)
                throw new EmployeeProblem(400, "An employee's linked login user cannot be changed.");
            await Apply(e, r, t);
            if (careerKey != EmployeeCareerHistory.Key(e))
                await history.Changed(e, careerBefore, r.CareerEffectiveDate, r.CareerChangeNote, t);
            await db.SaveChangesAsync(t);
            await events.Write(e, "EMPLOYEE_UPDATED", before, EmployeeEvents.Snapshot(e), t);
            if (manager != e.ManagerEmployeeId)
                await events.Write(e, "EMPLOYEE_MANAGER_CHANGED", new { managerEmployeeId = manager },
                    new { e.ManagerEmployeeId }, t);
            if (department != e.DepartmentId)
                await events.Write(e, "EMPLOYEE_DEPARTMENT_CHANGED", new { departmentId = department },
                    new { e.DepartmentId }, t);
        }, ct);
        return await WithRolesAsync(e, ct);
    }


    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            var e = await access.Get(id, t);
            access.CheckVersion(e);
            if (await access.Query.AnyAsync(x => x.ManagerEmployeeId == id, t))
                throw new EmployeeProblem(409, "Reassign direct reports before deleting their manager.");
            var before = EmployeeEvents.Snapshot(e);
            e.IsDeleted = true;
            e.UpdatedAt = DateTimeOffset.UtcNow;
            var linkedUser =
                await db.Users.FirstOrDefaultAsync(u => u.Id == e.UserId && u.TenantId == actor.TenantId, t);
            if (linkedUser != null)
            {
                linkedUser.Disable();
                var userRoles = await db.UserRoles
                    .Where(ur => ur.TenantId == actor.TenantId && ur.UserId == linkedUser.Id).ToListAsync(t);
                if (userRoles.Count > 0) db.UserRoles.RemoveRange(userRoles);
            }

            await db.SaveChangesAsync(t);
            await events.Write(e, "EMPLOYEE_DELETED", before, new { e.Id, e.IsDeleted }, t);
        }, ct);
    }


    public async Task<EmployeeProfileDto> UpdateSelfAsync(EmployeeSelfRequest r, CancellationToken ct)
    {
        Employee e = null!;
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            e = await access.Me(t);
            access.CheckVersion(e);
            await access.Reference(r.LocationId, "LOCATION", t);
            if (r.ProfilePhotoUrl != e.ProfilePhotoUrl) EmployeeAccess.Url(r.ProfilePhotoUrl);
            var before = EmployeeEvents.Snapshot(e);
            e.ProfilePhotoUrl = r.ProfilePhotoUrl;
            e.PreferredName = r.PreferredName?.Trim();
            e.Phone = r.Phone?.Trim();
            e.LocationId = await ResolveLocation(r.Location, r.LocationId, t);
            e.AadharNumber = r.AadharNumber?.Trim();
            e.PermanentAddress = r.PermanentAddress?.Trim();
            e.EmergencyContactName = r.EmergencyContactName?.Trim();
            e.EmergencyContactPhone = r.EmergencyContactPhone?.Trim();
            e.EmergencyContactRelation = r.EmergencyContactRelation?.Trim();
            e.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(t);
            await events.Write(e, "EMPLOYEE_PROFILE_UPDATED", before, EmployeeEvents.Snapshot(e), t);
        }, ct);
        return await WithRolesAsync(e, ct);
    }


    public async Task<CareerCatalogDto> CareerCatalogAsync(CancellationToken ct)
    {
        return new CareerCatalogDto(
            await db.Skills.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name)
                .Select(s => new CatalogSkillDto(s.Id, s.Name, s.Category, s.Description)).ToListAsync(ct),
            await db.Grades.AsNoTracking().Where(g => g.IsActive).OrderBy(g => g.DisplayOrder)
                .Select(g => new CatalogGradeDto(g.Id, g.Code, g.Name, g.Family)).ToListAsync(ct),
            await db.CareerProfiles.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name)
                .Select(p => new CatalogProfileDto(p.Id, p.Code, p.Name)).ToListAsync(ct),
            await db.CareerLevels.AsNoTracking().Where(l => l.IsActive && l.CareerProfile.IsActive && l.Grade.IsActive)
                .OrderBy(l => l.DisplayOrder)
                .Select(l => new CatalogLevelDto(l.Id, l.CareerProfileId, l.GradeId, l.Grade.Code, l.CareerTitle))
                .ToListAsync(ct));
    }

    public async Task<IReadOnlyList<EmployeeReferenceDto>> ReferencesAsync(CancellationToken ct)
    {
        return await db.Set<EmployeeReference>().AsNoTracking().Where(r => r.TenantId == actor.TenantId)
            .OrderBy(r => r.Kind).ThenBy(r => r.Name)
            .Select(r => new EmployeeReferenceDto(r.Id, r.TenantId, r.Version, r.Kind, r.Name)).ToListAsync(ct);
    }

    public async Task<EmployeeReferenceDto> AddReferenceAsync(EmployeeReferenceRequest r, CancellationToken ct)
    {
        if (!ReferenceKinds.Contains(r.Kind) || string.IsNullOrWhiteSpace(r.Name))
            throw new EmployeeProblem(400, "Invalid reference type or name.");
        var item = new EmployeeReference { TenantId = actor.TenantId, Kind = r.Kind, Name = r.Name.Trim() };

        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            db.Add(item);
            await db.SaveChangesAsync(t);
            await events.Write(new Employee { Id = item.Id, EmployeeCode = $"ORG-{item.Kind}-{item.Id}" },
                "EMPLOYEE_ORG_UPDATED", null, new { item.Id, item.Kind, item.Name }, t, "EMPLOYEE_REFERENCE");
        }, ct);
        return ToDto(item);
    }


    public async Task<EmployeeReferenceDto> UpdateReferenceAsync(long id, EmployeeReferenceRequest r, string ifMatch,
        CancellationToken ct)
    {
        EmployeeReference item = null!;
        await db.ExecuteInTransactionAsync(async t =>
        {
            await access.Lock(t);
            item = await db.Set<EmployeeReference>()
                       .SingleOrDefaultAsync(x => x.TenantId == actor.TenantId && x.Id == id, t) ??
                   throw new EmployeeProblem(404, "Reference not found.");
            if (string.IsNullOrEmpty(ifMatch)) throw new EmployeeProblem(428, "If-Match is required.");
            if (ifMatch != $"\"{item.Version}\"")
                throw new EmployeeProblem(409, "Reference changed. Reload and retry.");
            if (r.Kind != item.Kind || string.IsNullOrWhiteSpace(r.Name))
                throw new EmployeeProblem(400, "Reference kind cannot change and name is required.");
            var before = new { item.Id, item.Kind, item.Name };
            item.Name = r.Name.Trim();
            await db.SaveChangesAsync(t);
            await events.Write(new Employee { Id = item.Id, EmployeeCode = $"ORG-{item.Kind}-{item.Id}" },
                "EMPLOYEE_ORG_UPDATED", before, new { item.Id, item.Kind, item.Name }, t, "EMPLOYEE_REFERENCE");
        }, ct);
        return ToDto(item);
    }


    private async Task<EmployeeProfileDto> WithRolesAsync(Employee e, CancellationToken ct)
    {
        var roles = await (from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            where ur.TenantId == actor.TenantId && r.TenantId == actor.TenantId && ur.UserId == e.UserId
            select r.Code).ToArrayAsync(ct);
        return Profile(e,
            e.UserId == actor.UserId || access.Can("EMPLOYEE_UPDATE") || access.Can("EMPLOYEE_VIEW_SENSITIVE"), roles);
    }

    private static EmployeeProfileDto Profile(Employee e, bool contact, string[]? roles = null)
    {
        return new EmployeeProfileDto(
            roles, e.Id, e.UserId, e.EmployeeCode, e.FirstName, e.LastName, e.PreferredName, e.WorkEmail, e.WorkAddress,
            contact ? e.Phone : null, contact ? e.AadharNumber : null, contact ? e.PermanentAddress : null,
            contact ? e.EmergencyContactName : null,
            contact ? e.EmergencyContactPhone : null, contact ? e.EmergencyContactRelation : null, e.ProfilePhotoUrl,
            e.DepartmentId, e.DisciplineId, e.JobTitleId, e.DesignationId, e.GradeId, e.CareerProfileId,
            e.CareerLevelId, e.LocationId, e.ManagerEmployeeId,
            e.EmploymentType, e.EmploymentStatus, e.JoiningDate, e.EndDate, e.CreatedAt, e.UpdatedAt, e.Version);
    }

    private static EmployeeReferenceDto ToDto(EmployeeReference r)
    {
        return new EmployeeReferenceDto(r.Id, r.TenantId, r.Version, r.Kind, r.Name);
    }


    private async Task Apply(Employee e, EmployeeRequest r, CancellationToken ct)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == r.UserId && u.TenantId == actor.TenantId, ct) ??
                   throw new EmployeeProblem(400, "Select a user in your organization.");
        if (string.IsNullOrWhiteSpace(r.EmployeeCode) || string.IsNullOrWhiteSpace(r.FirstName) ||
            string.IsNullOrWhiteSpace(r.LastName)) throw new EmployeeProblem(400, "Code and name are required.");
        if (await db.Set<Employee>()
                .AnyAsync(x => x.TenantId == actor.TenantId && x.Id != e.Id && x.EmployeeCode == r.EmployeeCode.Trim(),
                    ct))
            throw new EmployeeProblem(409, "This employee code is already assigned. Enter a unique employee code.",
                "EMPLOYEE_CODE_EXISTS");
        if (r.EmploymentStatus is "ACTIVE" or "ON_LEAVE" && r.EndDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new EmployeeProblem(400,
                "An active employee cannot have a past employment end date. Leave it blank for ongoing employment; use the career effective date for promotions.");
        if (r.EndDate.HasValue && e.Id > 0 && (await history.Read(e.Id, ct)).Any(h => h.EffectiveDate > r.EndDate))
            throw new EmployeeProblem(400, "Employment end date cannot precede a recorded career change.");
        EmployeeAccess.Dates(r.JoiningDate, r.EndDate);
        if (r.ProfilePhotoUrl != e.ProfilePhotoUrl) EmployeeAccess.Url(r.ProfilePhotoUrl);
        if (r.EmploymentType is not ("FULL_TIME" or "PART_TIME" or "CONTRACT" or "INTERN") ||
            r.EmploymentStatus is not ("ACTIVE" or "ON_LEAVE" or "TERMINATED" or "INACTIVE"))
            throw new EmployeeProblem(400, "Invalid employment type or status.");
        foreach (var pair in new[]
                 {
                     (r.DepartmentId, "DEPARTMENT"), (r.DisciplineId, "DISCIPLINE"), (r.JobTitleId, "JOB_TITLE"),
                     (r.DesignationId, "DESIGNATION"), (r.GradeId, "GRADE"), (r.LocationId, "LOCATION")
                 })
            await access.Reference(pair.Item1, pair.Item2, ct);

        if (r.CareerLevelId.HasValue)
        {
            if (!r.CareerProfileId.HasValue)
            {
                var profileId = await db.CareerLevels.Where(l => l.Id == r.CareerLevelId.Value)
                    .Select(l => l.CareerProfileId).FirstOrDefaultAsync(ct);
                if (profileId > 0)
                    r = r with
                    {
                        CareerProfileId = profileId, CatalogDepartmentId = r.CatalogDepartmentId ?? profileId
                    };
            }
            else
            {
                var directMatch = await db.CareerLevels.AnyAsync(
                    l => l.Id == r.CareerLevelId.Value && l.CareerProfileId == r.CareerProfileId && l.IsActive &&
                         l.Grade.IsActive, ct);
                if (!directMatch)
                {
                    var requestedLevel = await db.CareerLevels.Include(l => l.Grade)
                        .FirstOrDefaultAsync(l => l.Id == r.CareerLevelId.Value && l.IsActive, ct);
                    if (requestedLevel != null)
                    {
                        var targetGradeId = r.CatalogGradeId ?? requestedLevel.GradeId;
                        var alignedLevel = await db.CareerLevels.FirstOrDefaultAsync(
                            l => l.CareerProfileId == r.CareerProfileId && l.GradeId == targetGradeId && l.IsActive,
                            ct);
                        if (alignedLevel != null) r = r with { CareerLevelId = alignedLevel.Id };
                    }
                }
            }
        }

        if (r.CareerProfileId.HasValue &&
            !await db.CareerProfiles.AnyAsync(p => p.Id == r.CareerProfileId && p.IsActive, ct))
            throw new EmployeeProblem(400, "Select an active career profile.");
        if (r.CareerLevelId.HasValue && !await db.CareerLevels.AnyAsync(
                l => l.Id == r.CareerLevelId && l.CareerProfileId == r.CareerProfileId && l.IsActive &&
                     l.Grade.IsActive, ct))
            throw new EmployeeProblem(400, "Select a grade/title belonging to the selected career profile.");

        var selectedDepartmentId = r.DepartmentId;
        if (r.CatalogDepartmentId.HasValue)
        {
            if (r.CatalogDepartmentId != r.CareerProfileId)
                throw new EmployeeProblem(400, "Department does not match the selected career mapping.");
            var profile = await db.CareerProfiles.SingleAsync(p => p.Id == r.CatalogDepartmentId, ct);
            selectedDepartmentId = (await ReferenceFor("DEPARTMENT", profile.Name, false, ct)).Id;
        }

        var selectedGradeId = r.GradeId;
        if (r.CatalogGradeId.HasValue)
        {
            var grade = await db.Grades.SingleOrDefaultAsync(g => g.Id == r.CatalogGradeId && g.IsActive, ct) ??
                        throw new EmployeeProblem(400, "Select an active grade.");
            if (r.CareerLevelId.HasValue &&
                !await db.CareerLevels.AnyAsync(l => l.Id == r.CareerLevelId && l.GradeId == grade.Id, ct))
                throw new EmployeeProblem(400, "Job title does not match the selected grade.");
            selectedGradeId = (await ReferenceFor("GRADE", grade.Code, false, ct)).Id;
        }

        var selectedDesignationId = r.DesignationId;
        var gradeCode = selectedGradeId.HasValue
            ? await db.Set<EmployeeReference>().Where(x => x.Id == selectedGradeId && x.TenantId == actor.TenantId)
                .Select(x => x.Name).SingleAsync(ct)
            : null;
        if (gradeCode is null && r.CareerLevelId.HasValue)
            gradeCode = await db.CareerLevels.Where(x => x.Id == r.CareerLevelId).Select(x => x.Grade.Code)
                .SingleAsync(ct);
        var designation = gradeCode switch
        {
            "T0" => "Intern", "T1" => "Junior", "T2" => "", "T3" => "Senior", "T4" => "Staff", _ => r.Designation
        };
        if (gradeCode == "T2") selectedDesignationId = null;
        if (!string.IsNullOrEmpty(designation))
        {
            var name = designation.Trim();
            if (name is not ("Intern" or "Junior" or "Senior" or "Lead" or "Staff" or "Principal"))
                throw new EmployeeProblem(400, "Select a valid designation.");
            selectedDesignationId = (await ReferenceFor("DESIGNATION", name, true, ct)).Id;
        }

        e.CareerProfileId = r.CareerProfileId;
        e.CareerLevelId = r.CareerLevelId;

        if (r.ManagerEmployeeId.HasValue &&
            !await access.ManagerCandidates().AnyAsync(m => m.Id == r.ManagerEmployeeId.Value, ct))
            throw new EmployeeProblem(400,
                "Select an active employee with an Admin, HR, Manager, Engineering Manager, EM, CEO, CTO, PM, or PGM role.");
        var seen = new HashSet<long>();
        if (e.Id > 0) seen.Add(e.Id);
        var parent = r.ManagerEmployeeId;
        while (parent.HasValue)
        {
            if (!seen.Add(parent.Value)) throw new EmployeeProblem(400, "Manager hierarchy cannot contain a cycle.");
            parent = (await access.Get(parent.Value, ct)).ManagerEmployeeId;
        }

        e.UserId = r.UserId;
        e.EmployeeCode = r.EmployeeCode.Trim().ToUpperInvariant();
        e.FirstName = r.FirstName.Trim();
        e.LastName = r.LastName.Trim();
        e.WorkEmail = user.Email;
        e.PreferredName = r.PreferredName?.Trim();
        e.Phone = r.Phone?.Trim();
        e.AadharNumber = r.AadharNumber?.Trim();
        e.PermanentAddress = r.PermanentAddress?.Trim();
        e.EmergencyContactName = r.EmergencyContactName?.Trim();
        e.EmergencyContactPhone = r.EmergencyContactPhone?.Trim();
        e.EmergencyContactRelation = r.EmergencyContactRelation?.Trim();
        e.ProfilePhotoUrl = r.ProfilePhotoUrl;
        e.DepartmentId = selectedDepartmentId;
        e.DisciplineId = r.DisciplineId;
        e.JobTitleId = r.JobTitleId;
        e.DesignationId = selectedDesignationId;
        e.GradeId = selectedGradeId;
        e.LocationId = await ResolveLocation(r.Location, r.LocationId, ct);
        e.ManagerEmployeeId = r.ManagerEmployeeId;
        e.EmploymentType = r.EmploymentType;
        e.EmploymentStatus = r.EmploymentStatus;
        e.JoiningDate = r.JoiningDate;
        e.EndDate = r.EndDate;
        e.UpdatedAt = DateTimeOffset.UtcNow;
    }


    private async Task<EmployeeReference> ReferenceFor(string kind, string name, bool caseInsensitive,
        CancellationToken ct)
    {
        var reference = caseInsensitive
            ? await db.Set<EmployeeReference>()
                .FirstOrDefaultAsync(
                    x => x.TenantId == actor.TenantId && x.Kind == kind && x.Name.ToLower() == name.ToLower(), ct)
            : await db.Set<EmployeeReference>()
                .FirstOrDefaultAsync(x => x.TenantId == actor.TenantId && x.Kind == kind && x.Name == name, ct);
        if (reference is null)
        {
            reference = new EmployeeReference { TenantId = actor.TenantId, Kind = kind, Name = name };
            db.Add(reference);
            await db.SaveChangesAsync(ct);
        }

        return reference;
    }

    private async Task<long?> ResolveLocation(string? text, long? legacyId, CancellationToken ct)
    {
        if (text is null) return legacyId;
        var name = text.Trim();
        if (name.Length == 0) return null;
        return (await ReferenceFor("LOCATION", name, true, ct)).Id;
    }
}