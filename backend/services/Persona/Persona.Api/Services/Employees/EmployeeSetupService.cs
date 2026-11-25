using System.Text.Json;
using Persona.Contracts;
using Persona.Domain.Careers;
using Persona.Domain.Employees;
using IdentityTenant.Domain.Entities;
using IdentityTenant.Domain.Enums;
using Persona.Infrastructure.Persistence;
using Messaging.Events;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class EmployeeSetupService(PersonaDbContext db, ITenantContext actor)
{
    private const int PageSize = 25;


    public async Task<SetupCatalogDto> CatalogAsync(CancellationToken ct)
    {
        var tenantRoles = await db.Roles.AsNoTracking()
            .Where(x => x.TenantId == actor.TenantId)
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Code, x.Name, x.Description })
            .ToListAsync(ct);

        var roleIds = tenantRoles.Select(r => r.Id).ToList();
        var rolePerms = await db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);

        var permsByRole = rolePerms
            .GroupBy(rp => rp.RoleId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.PermissionId).ToList());

        var roles = tenantRoles.Select(r => new SetupRoleDto(r.Id, r.Code, r.Name, r.Description,
            permsByRole.TryGetValue(r.Id, out var pIds) ? pIds : new List<long>())).ToList();

        var skills = await db.Skills.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var departments = await db.CareerProfiles.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);
        var grades = await db.Grades.AsNoTracking().OrderBy(x => x.DisplayOrder).ThenBy(x => x.Code).ToListAsync(ct);
        var titles = await db.CareerLevels.AsNoTracking().OrderBy(x => x.DisplayOrder)
            .Select(x => new TitleDto(x.Id, x.CareerTitle, x.CareerProfileId, x.GradeId, x.DisplayOrder, x.IsActive))
            .ToListAsync(ct);
        var permissions = await db.Permissions.AsNoTracking().OrderBy(p => p.Id)
            .Select(p => new SetupPermissionDto(p.Id, p.Code, p.Name, p.Description)).ToListAsync(ct);

        return new SetupCatalogDto(skills.Select(ToDto).ToList(), departments.Select(ToDto).ToList(),
            grades.Select(ToDto).ToList(), titles, roles, permissions);
    }


    public async Task<SetupUserPageDto> UsersAsync(string? search, long? roleId, int page, CancellationToken ct)
    {
        if (page < 1 || page > 100000) throw new EmployeeProblem(400, "Invalid page.");
        var users = db.Users.AsNoTracking().Where(u =>
            u.TenantId == actor.TenantId &&
            u.Status == UserStatus.Active &&
            !db.Set<Employee>().Any(e =>
                e.TenantId == actor.TenantId && e.UserId == u.Id &&
                (e.IsDeleted || e.EmploymentStatus == "TERMINATED")));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            users = users.Where(u => u.DisplayName.ToLower().Contains(term) || u.Email.ToLower().Contains(term));
        }

        if (roleId.HasValue)
        {
            if (roleId.Value == 0)
                users = users.Where(u => !db.UserRoles.Any(ur => ur.TenantId == actor.TenantId && ur.UserId == u.Id));
            else
                users = users.Where(u => db.UserRoles.Any(ur =>
                    ur.TenantId == actor.TenantId && ur.UserId == u.Id && ur.RoleId == roleId.Value));
        }

        var count = await users.CountAsync(ct);
        var rows = await users.OrderBy(u => u.DisplayName).ThenBy(u => u.Id).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(u => new
            {
                u.Id, u.DisplayName, u.Email,
                roleIds = db.UserRoles.Where(r => r.TenantId == actor.TenantId && r.UserId == u.Id)
                    .Select(r => r.RoleId).ToArray()
            }).ToListAsync(ct);
        var assignments = await db.UserRoles.CountAsync(x => x.TenantId == actor.TenantId, ct);
        return new SetupUserPageDto(assignments,
            rows.Select(r => new SetupUserDto(r.Id, r.DisplayName, r.Email, r.roleIds)).ToList(),
            page, PageSize, count, (int)Math.Ceiling(count / (double)PageSize));
    }


    public async Task<UserRolesDto> AssignRolesAsync(long userId, AssignRolesInput input, bool replace,
        CancellationToken ct)
    {
        await db.ExecuteInTransactionAsync(async token =>
        {
            await LockAndRequireSetupAccessAsync(token);
            if (!await db.Users.AnyAsync(u => u.Id == userId && u.TenantId == actor.TenantId, token))
                throw new EmployeeProblem(404, "User not found.");
            var ids = input.RoleIds.Distinct().ToArray();
            if (ids.Length == 0 && !replace) throw new EmployeeProblem(400, "Select at least one role to add.");
            if (await db.Roles.CountAsync(r => r.TenantId == actor.TenantId && ids.Contains(r.Id), token) != ids.Length)
                throw new EmployeeProblem(400, "Select roles from your organization.");
            var existing = await db.UserRoles.Where(r => r.TenantId == actor.TenantId && r.UserId == userId)
                .Select(r => r.RoleId).ToArrayAsync(token);
            if (replace)
            {
                if (input.ExpectedRoleIds is null ||
                    !existing.Order().SequenceEqual(input.ExpectedRoleIds.Distinct().Order()))
                    throw new EmployeeProblem(409, "User roles changed. Reopen the editor before saving.");
                var adminId = await db.Roles.Where(r => r.TenantId == actor.TenantId && r.Code == "ADMIN")
                    .Select(r => r.Id).SingleAsync(token);
                if (existing.Contains(adminId) && !ids.Contains(adminId) &&
                    !await AnotherActiveAdminAsync(userId, adminId, token))
                    throw new EmployeeProblem(400, "Keep at least one active administrator.", "LAST_ADMIN_REQUIRED");
                db.UserRoles.RemoveRange(await db.UserRoles
                    .Where(r => r.TenantId == actor.TenantId && r.UserId == userId && !ids.Contains(r.RoleId))
                    .ToListAsync(token));
            }

            foreach (var id in ids.Except(existing)) db.UserRoles.Add(new UserRole(actor.TenantId, userId, id));
            await db.SaveChangesAsync(token);
        }, ct);
        return new UserRolesDto(userId,
            await db.UserRoles.Where(r => r.TenantId == actor.TenantId && r.UserId == userId).Select(r => r.RoleId)
                .ToArrayAsync(ct));
    }


    public async Task<BulkRolesResultDto> BulkAssignRolesAsync(BulkAssignRolesInput input, CancellationToken ct)
    {
        var userIds = input.UserIds.Distinct().ToArray();
        if (userIds.Length == 0) throw new EmployeeProblem(400, "Select at least one user.");
        var roleIds = input.RoleIds.Distinct().ToArray();
        var mode = (input.Mode ?? "ADD").ToUpperInvariant();
        if (mode is not ("ADD" or "REPLACE" or "REMOVE")) throw new EmployeeProblem(400, "Invalid bulk action mode.");
        if (roleIds.Length == 0 && mode != "REPLACE") throw new EmployeeProblem(400, "Select at least one role.");

        var updatedCount = 0;
        await db.ExecuteInTransactionAsync(async token =>
        {
            await LockAndRequireSetupAccessAsync(token);

            if (roleIds.Length > 0 &&
                await db.Roles.CountAsync(r => r.TenantId == actor.TenantId && roleIds.Contains(r.Id), token) !=
                roleIds.Length)
                throw new EmployeeProblem(400, "Select valid roles from your organization.");

            var adminRole =
                await db.Roles.FirstOrDefaultAsync(r => r.TenantId == actor.TenantId && r.Code == "ADMIN", token);
            var adminRoleId = adminRole?.Id;

            var users = await db.Users.Where(u => u.TenantId == actor.TenantId && userIds.Contains(u.Id))
                .Select(u => u.Id).ToListAsync(token);

            foreach (var userId in users)
            {
                var existing = await db.UserRoles.Where(r => r.TenantId == actor.TenantId && r.UserId == userId)
                    .Select(r => r.RoleId).ToListAsync(token);
                var nextRoles = mode switch
                {
                    "ADD" => existing.Union(roleIds).ToList(),
                    "REMOVE" => existing.Except(roleIds).ToList(),
                    "REPLACE" => roleIds.ToList(),
                    _ => existing
                };


                if (adminRoleId.HasValue && existing.Contains(adminRoleId.Value) &&
                    !nextRoles.Contains(adminRoleId.Value) &&
                    !await AnotherActiveAdminAsync(userId, adminRoleId.Value, token))
                    throw new EmployeeProblem(400, "Cannot remove the last active administrator.",
                        "LAST_ADMIN_REQUIRED");

                var toRemove = existing.Except(nextRoles).ToList();
                if (toRemove.Count > 0)
                    db.UserRoles.RemoveRange(await db.UserRoles.Where(r =>
                            r.TenantId == actor.TenantId && r.UserId == userId && toRemove.Contains(r.RoleId))
                        .ToListAsync(token));

                foreach (var rId in nextRoles.Except(existing))
                    db.UserRoles.Add(new UserRole(actor.TenantId, userId, rId));
                updatedCount++;
            }

            await db.SaveChangesAsync(token);
        }, ct);

        return new BulkRolesResultDto(updatedCount, mode);
    }


    public async Task<DeleteAllResultDto> DeleteAllAsync(string kind, CancellationToken ct)
    {
        var deleted = 0;
        var total = 0;
        await db.ExecuteInTransactionAsync(async token =>
        {
            await LockAndRequireSetupAccessAsync(token);
            switch (kind)
            {
                case "departments":
                    total = await db.CareerProfiles.CountAsync(token);
                    deleted = await db.CareerProfiles.Where(p =>
                            !db.CareerLevels.Any(l => l.CareerProfileId == p.Id) && !db.Set<Employee>().Any(e =>
                                e.CareerProfileId == p.Id || db.Set<EmployeeReference>().Any(r =>
                                    r.Id == e.DepartmentId && r.TenantId == e.TenantId && r.Name == p.Name)))
                        .ExecuteDeleteAsync(token);
                    break;
                case "grades":
                    total = await db.Grades.CountAsync(token);
                    deleted = await db.Grades.Where(g =>
                            !db.CareerLevels.Any(l => l.GradeId == g.Id) && !db.Set<Employee>().Any(e =>
                                db.Set<EmployeeReference>().Any(r =>
                                    r.Id == e.GradeId && r.TenantId == e.TenantId && r.Name == g.Code)))
                        .ExecuteDeleteAsync(token);
                    break;
                case "titles":
                    total = await db.CareerLevels.CountAsync(token);
                    deleted = await db.CareerLevels
                        .Where(l => !db.Set<Employee>().Any(e => e.CareerLevelId == l.Id) &&
                                    !db.Set<CareerLevelSkill>().Any(s => s.CareerLevelId == l.Id))
                        .ExecuteDeleteAsync(token);
                    break;
                case "skills":
                    total = await db.Skills.CountAsync(token);
                    deleted = await db.Skills
                        .Where(s => !db.Set<CareerLevelSkill>().Any(cls => cls.SkillId == s.Id) &&
                                    !db.Set<EmployeeSkill>().Any(es => es.SkillId == s.Id)).ExecuteDeleteAsync(token);
                    break;
                case "roles":
                {
                    total = await db.Roles.CountAsync(r => r.TenantId == actor.TenantId, token);
                    var ids = await db.Roles
                        .Where(r => r.TenantId == actor.TenantId && r.Code != "ADMIN" &&
                                    !db.UserRoles.Any(ur => ur.RoleId == r.Id)).Select(r => r.Id).ToArrayAsync(token);
                    await db.RolePermissions.Where(r => ids.Contains(r.RoleId)).ExecuteDeleteAsync(token);
                    deleted = await db.Roles.Where(r => r.TenantId == actor.TenantId && ids.Contains(r.Id))
                        .ExecuteDeleteAsync(token);
                    break;
                }
                case "users":
                    total = await db.UserRoles.CountAsync(r => r.TenantId == actor.TenantId, token);

                    deleted = await db.UserRoles
                        .Where(r => r.TenantId == actor.TenantId &&
                                    !db.Roles.Any(role => role.Id == r.RoleId && role.Code == "ADMIN"))
                        .ExecuteDeleteAsync(token);
                    break;
                default: throw new EmployeeProblem(400, "Unknown setup category.");
            }
        }, ct);
        return new DeleteAllResultDto(deleted, total - deleted);
    }


    public async Task DeleteAsync(string kind, long id, CancellationToken ct)
    {
        await db.ExecuteInTransactionAsync(async token =>
        {
            await LockAndRequireSetupAccessAsync(token);
            switch (kind)
            {
                case "departments":
                {
                    var row = await db.CareerProfiles.FindAsync([id], token) ??
                              throw new EmployeeProblem(404, "Department not found.");
                    if (await db.CareerLevels.AnyAsync(x => x.CareerProfileId == id, token) || await db.Set<Employee>()
                            .AnyAsync(
                                x => x.CareerProfileId == id || db.Set<EmployeeReference>().Any(r =>
                                    r.Id == x.DepartmentId && r.TenantId == x.TenantId && r.Name == row.Name), token))
                        throw new EmployeeProblem(400, "Department is in use.", "SETUP_DEPARTMENT_IN_USE");
                    db.CareerProfiles.Remove(row);
                    break;
                }
                case "grades":
                {
                    var row = await db.Grades.FindAsync([id], token) ??
                              throw new EmployeeProblem(404, "Grade not found.");
                    if (await db.CareerLevels.AnyAsync(x => x.GradeId == id, token) || await db.Set<Employee>()
                            .AnyAsync(
                                x => db.Set<EmployeeReference>().Any(r =>
                                    r.Id == x.GradeId && r.TenantId == x.TenantId && r.Name == row.Code), token))
                        throw new EmployeeProblem(400, "Grade is in use.", "SETUP_GRADE_IN_USE");
                    db.Grades.Remove(row);
                    break;
                }
                case "titles":
                {
                    var row = await db.CareerLevels.FindAsync([id], token) ??
                              throw new EmployeeProblem(404, "Job title not found.");
                    if (await db.Set<Employee>().AnyAsync(x => x.CareerLevelId == id, token) ||
                        await db.Set<CareerLevelSkill>().AnyAsync(x => x.CareerLevelId == id, token))
                        throw new EmployeeProblem(400, "Job title is in use.", "SETUP_TITLE_IN_USE");
                    db.CareerLevels.Remove(row);
                    break;
                }
                case "skills":
                {
                    var row = await db.Skills.FindAsync([id], token) ??
                              throw new EmployeeProblem(404, "Skill not found.");
                    if (await db.Set<CareerLevelSkill>().AnyAsync(x => x.SkillId == id, token) ||
                        await db.Set<EmployeeSkill>().AnyAsync(x => x.SkillId == id, token))
                        throw new EmployeeProblem(400, "Skill is in use.", "SETUP_SKILL_IN_USE");
                    db.Skills.Remove(row);
                    break;
                }
                case "roles":
                {
                    var row = await db.Roles.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == actor.TenantId,
                        token) ?? throw new EmployeeProblem(404, "Role not found.");
                    if (row.Code == "ADMIN")
                        throw new EmployeeProblem(400, "The administrator role cannot be deleted.",
                            "SETUP_ADMIN_REQUIRED");
                    if (await db.UserRoles.AnyAsync(x => x.RoleId == id, token))
                        throw new EmployeeProblem(400, "Role is assigned to users.", "SETUP_ROLE_IN_USE");
                    db.RolePermissions.RemoveRange(await db.RolePermissions.Where(x => x.RoleId == id)
                        .ToListAsync(token));
                    db.Roles.Remove(row);
                    break;
                }
                case "permissions":
                {
                    var row = await db.Permissions.FindAsync([id], token) ??
                              throw new EmployeeProblem(404, "Permission not found.");
                    db.RolePermissions.RemoveRange(await db.RolePermissions.Where(x => x.PermissionId == id)
                        .ToListAsync(token));
                    db.Permissions.Remove(row);
                    break;
                }
                default: throw new EmployeeProblem(400, "This category cannot be deleted.");
            }

            await db.SaveChangesAsync(token);
        }, ct);
    }


    public async Task<object> SaveAsync(string kind, long? id, SetupInput input, CancellationToken ct)
    {
        var name = await ResolveNameAsync(kind, input, ct);
        var code = input.Code?.Trim();
        if (name.Length == 0) throw new EmployeeProblem(400, "Name is required.");
        if (kind != "titles" && string.IsNullOrEmpty(code)) throw new EmployeeProblem(400, "Code is required.");
        if (input.DisplayOrder < 0) throw new EmployeeProblem(400, "Display order cannot be negative.");

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(72410931)", ct);
        var saved = kind switch
        {
            "departments" => await SaveDepartmentAsync(id, name, code, ct),
            "grades" => await SaveGradeAsync(id, name, code, input, ct),
            "titles" => await SaveTitleAsync(id, name, input, ct),
            "skills" => await SaveSkillAsync(id, name, code, input, ct),
            "roles" => await SaveRoleAsync(id, name, code, input, ct),
            "permissions" => await SavePermissionAsync(id, name, code, input, ct),
            _ => throw new EmployeeProblem(400, "Unknown setup category.")
        };

        await db.SaveChangesAsync(ct);
        if (saved is Permission perm && input.RoleIds != null)
            await ReplacePermissionLinksAsync(perm, input.RoleIds, ct);
        if (saved is Role role)
        {
            if (input.PermissionIds != null) await ReplaceRoleLinksAsync(role, input.PermissionIds, ct);
            else if (!id.HasValue) await GiveStarterPermissionsAsync(role, ct);
        }

        if (saved is Skill skill) await RecordSkillChangeAsync(skill, ct);
        await transaction.CommitAsync(ct);

        return saved switch
        {
            Role r => new SetupRoleDto(r.Id, r.Code, r.Name, r.Description,
                await db.RolePermissions.Where(rp => rp.RoleId == r.Id).Select(rp => rp.PermissionId).ToListAsync(ct)),
            Permission p => new SetupPermissionDto(p.Id, p.Code, p.Name, p.Description),
            CareerLevel level => new SavedTitleDto(level.Id, level.CareerTitle),
            CareerProfile department => ToDto(department),
            Grade grade => ToDto(grade),
            Skill s => ToDto(s),
            _ => saved
        };
    }


    private async Task<string> ResolveNameAsync(string kind, SetupInput input, CancellationToken ct)
    {
        var name = (input.Name ?? "").Trim();
        var code = input.Code?.Trim();
        if (kind == "grades" && string.IsNullOrEmpty(name))
            name = code ?? "";
        if (kind == "titles" && string.IsNullOrEmpty(name) && input.DepartmentId.HasValue && input.GradeId.HasValue)
        {
            var dept = await db.CareerProfiles.FindAsync([input.DepartmentId.Value], ct);
            var grd = await db.Grades.FindAsync([input.GradeId.Value], ct);
            if (grd != null && !string.IsNullOrWhiteSpace(grd.Name) &&
                !grd.Name.Equals(grd.Code, StringComparison.OrdinalIgnoreCase))
                name = grd.Name.Trim();
            else if (dept != null && grd != null)
                name = $"{dept.Name} {grd.Code}".Trim();
            else if (grd != null)
                name = grd.Code;
        }

        return name;
    }

    private async Task<object> SaveDepartmentAsync(long? id, string name, string? code, CancellationToken ct)
    {
        var row = id.HasValue
            ? await db.CareerProfiles.FindAsync([id.Value], ct) ??
              throw new EmployeeProblem(404, "Department not found.")
            : new CareerProfile { Code = code! };
        if (id.HasValue && row.Code != code) throw new EmployeeProblem(400, "Existing codes cannot be changed.");
        if (await db.CareerProfiles.AnyAsync(
                x => x.Id != row.Id && x.Code == code && x.Name.ToLower() == name.ToLower(), ct))
            throw new EmployeeProblem(400, "A department role with this code and name already exists.");

        if (id.HasValue)
            foreach (var reference in await db.Set<EmployeeReference>()
                         .Where(x => x.TenantId == actor.TenantId && x.Kind == "DEPARTMENT" && x.Name == row.Name)
                         .ToListAsync(ct))
                reference.Name = name;
        row.Name = name;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        if (!id.HasValue) db.CareerProfiles.Add(row);
        return row;
    }

    private async Task<object> SaveGradeAsync(long? id, string name, string? code, SetupInput input,
        CancellationToken ct)
    {
        var row = id.HasValue
            ? await db.Grades.FindAsync([id.Value], ct) ?? throw new EmployeeProblem(404, "Grade not found.")
            : new Grade { Code = code! };
        if (id.HasValue && row.Code != code) throw new EmployeeProblem(400, "Existing codes cannot be changed.");
        if (await db.Grades.AnyAsync(x => x.Id != row.Id && x.Code == code, ct))
            throw new EmployeeProblem(400, "A grade with this code already exists.");
        row.Name = name;
        row.Family = input.Family?.Trim();
        row.DisplayOrder = input.DisplayOrder;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        if (!id.HasValue) db.Grades.Add(row);
        return row;
    }

    private async Task<object> SaveTitleAsync(long? id, string name, SetupInput input, CancellationToken ct)
    {
        if (!await db.CareerProfiles.AnyAsync(x => x.Id == input.DepartmentId && x.IsActive, ct) ||
            !await db.Grades.AnyAsync(x => x.Id == input.GradeId && x.IsActive, ct))
            throw new EmployeeProblem(400, "Select an active department and grade.");
        var row = id.HasValue
            ? await db.CareerLevels.FindAsync([id.Value], ct) ?? throw new EmployeeProblem(404, "Job title not found.")
            : new CareerLevel { CareerProfileId = input.DepartmentId!.Value, GradeId = input.GradeId!.Value };

        if (id.HasValue && (row.CareerProfileId != input.DepartmentId || row.GradeId != input.GradeId))
            throw new EmployeeProblem(400, "Create a new title to use a different department or grade.");
        if (await db.CareerLevels.AnyAsync(
                x => x.Id != row.Id && x.CareerProfileId == input.DepartmentId && x.GradeId == input.GradeId, ct))
            throw new EmployeeProblem(400, "This department and grade already have a title. Edit the existing title.");
        row.CareerTitle = name;
        row.DisplayOrder = input.DisplayOrder;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        if (!id.HasValue) db.CareerLevels.Add(row);
        return row;
    }

    private async Task<object> SaveSkillAsync(long? id, string name, string? code, SetupInput input,
        CancellationToken ct)
    {
        var row = id.HasValue
            ? await db.Skills.FindAsync([id.Value], ct) ?? throw new EmployeeProblem(404, "Skill not found.")
            : new Skill { Code = code! };
        if (id.HasValue && row.Code != code) throw new EmployeeProblem(400, "Existing codes cannot be changed.");
        if (await db.Skills.AnyAsync(x => x.Id != row.Id && (x.Code == code || x.Name.ToLower() == name.ToLower()), ct))
            throw new EmployeeProblem(409, "Skill already exists.");
        row.Name = name;
        row.Category = input.Category?.Trim();
        row.Description = input.Description?.Trim();
        row.IsActive = input.IsActive;
        row.UpdatedAt = DateTimeOffset.UtcNow;
        if (!id.HasValue) db.Skills.Add(row);
        return row;
    }

    private async Task<object> SaveRoleAsync(long? id, string name, string? code, SetupInput input,
        CancellationToken ct)
    {
        if (name.Length > 150) throw new EmployeeProblem(400, "Role names must be at most 150 characters.");
        if (code!.Length > 50) throw new EmployeeProblem(400, "Role codes must be at most 50 characters.");
        var row = id.HasValue
            ? await db.Roles.SingleOrDefaultAsync(x => x.Id == id.Value && x.TenantId == actor.TenantId, ct) ??
              throw new EmployeeProblem(404, "Role not found.")
            : new Role(actor.TenantId, code, name, input.Description);
        if (id.HasValue && row.Code != code) throw new EmployeeProblem(400, "Existing role codes cannot be changed.");
        if (await db.Roles.AnyAsync(x => x.TenantId == actor.TenantId && x.Id != row.Id && x.Code == code, ct))
            throw new EmployeeProblem(400, "A role with this code already exists.");
        row.Update(name, input.Description);
        if (!id.HasValue) db.Roles.Add(row);
        return row;
    }

    private async Task<object> SavePermissionAsync(long? id, string name, string? code, SetupInput input,
        CancellationToken ct)
    {
        if (name.Length > 200) throw new EmployeeProblem(400, "Permission names must be at most 200 characters.");
        if (code!.Length > 150) throw new EmployeeProblem(400, "Permission codes must be at most 150 characters.");
        var normCode = code.Trim().ToUpperInvariant();
        var row = id.HasValue
            ? await db.Permissions.FindAsync([id.Value], ct) ?? throw new EmployeeProblem(404, "Permission not found.")
            : new Permission(normCode, name, input.Description);
        if (id.HasValue && row.Code != normCode)
            throw new EmployeeProblem(400, "Existing permission codes cannot be changed.");
        if (await db.Permissions.AnyAsync(x => x.Id != row.Id && x.Code == normCode, ct))
            throw new EmployeeProblem(409, "A permission with this code already exists.");
        if (id.HasValue) row.Update(name, input.Description);
        else db.Permissions.Add(row);
        return row;
    }


    private async Task ReplacePermissionLinksAsync(Permission perm, long[] roleIds, CancellationToken ct)
    {
        var requestedRoleIds = roleIds.Distinct().ToArray();
        var validRoleIds = await db.Roles.Where(r => r.TenantId == actor.TenantId && requestedRoleIds.Contains(r.Id))
            .Select(r => r.Id).ToListAsync(ct);
        var existingRps = await db.RolePermissions.Where(rp =>
                rp.PermissionId == perm.Id && db.Roles.Any(r => r.Id == rp.RoleId && r.TenantId == actor.TenantId))
            .ToListAsync(ct);
        db.RolePermissions.RemoveRange(existingRps);
        foreach (var rid in validRoleIds) db.RolePermissions.Add(new RolePermission(rid, perm.Id));
        await db.SaveChangesAsync(ct);
    }


    private async Task ReplaceRoleLinksAsync(Role role, long[] permissionIds, CancellationToken ct)
    {
        var requestedPermIds = permissionIds.Distinct().ToArray();
        var validPermIds = await db.Permissions.Where(p => requestedPermIds.Contains(p.Id)).Select(p => p.Id)
            .ToListAsync(ct);
        var existingRps = await db.RolePermissions.Where(rp => rp.RoleId == role.Id).ToListAsync(ct);
        db.RolePermissions.RemoveRange(existingRps);
        foreach (var pid in validPermIds) db.RolePermissions.Add(new RolePermission(role.Id, pid));
        await db.SaveChangesAsync(ct);
    }


    private async Task GiveStarterPermissionsAsync(Role role, CancellationToken ct)
    {
        if (role.Code == "TALENT_ACQUISITION")
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO role_permissions(role_id,permission_id,created_at) SELECT {role.Id},id,CURRENT_TIMESTAMP FROM permissions WHERE code IN ('ISSUE_CREATE','ISSUE_VIEW','PROJECT_VIEW') ON CONFLICT DO NOTHING",
                ct);
        else if (role.Code is "HR" or "HUMAN_RESOURCE")
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO role_permissions(role_id,permission_id,created_at) SELECT {role.Id},id,CURRENT_TIMESTAMP FROM permissions WHERE code LIKE 'EMPLOYEE_%' OR code IN ('USER_VIEW','TEAM_VIEW','PROJECT_VIEW','ISSUE_VIEW','WORKLOG_VIEW','TIMESHEET_VIEW') ON CONFLICT DO NOTHING",
                ct);
        else if (role.Code == "DEVELOPER")
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO role_permissions(role_id,permission_id,created_at) SELECT {role.Id},id,CURRENT_TIMESTAMP FROM permissions WHERE code IN ('USER_VIEW','TEAM_VIEW','PROJECT_VIEW','ISSUE_VIEW','ISSUE_CREATE','ISSUE_UPDATE','ISSUE_ASSIGN','ISSUE_TRANSITION','ISSUE_COMMENT_CREATE','WORKLOG_VIEW','WORKLOG_CREATE','WORKLOG_UPDATE','WORKLOG_DELETE','TIMESHEET_VIEW','EMPLOYEE_VIEW','EMPLOYEE_UPDATE_SELF') ON CONFLICT DO NOTHING",
                ct);
        else if (role.Code is "MANAGER" or "ENGINEERING_MANAGER")
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO role_permissions(role_id,permission_id,created_at) SELECT {role.Id},id,CURRENT_TIMESTAMP FROM permissions WHERE code IN ('USER_VIEW','TEAM_VIEW','PROJECT_VIEW','PROJECT_MANAGE','ISSUE_VIEW','ISSUE_CREATE','ISSUE_UPDATE','ISSUE_ASSIGN','ISSUE_TRANSITION','ISSUE_COMMENT_CREATE','WORKLOG_VIEW','WORKLOG_CREATE','WORKLOG_UPDATE','TIMESHEET_VIEW','TIMESHEET_TEAM_VIEW','EMPLOYEE_VIEW','EMPLOYEE_VIEW_TEAM','EMPLOYEE_UPDATE_SELF') ON CONFLICT DO NOTHING",
                ct);
    }


    private async Task RecordSkillChangeAsync(Skill skill, CancellationToken ct)
    {
        var audit = new IssueAuditEvent
        {
            EventId = Guid.NewGuid(), EventType = "SKILL_UPDATED", TenantId = actor.TenantId, EntityType = "SKILL",
            EntityId = skill.Id, EntityKey = skill.Code, PerformedByUserId = actor.UserId,
            OccurredAt = DateTimeOffset.UtcNow, NewValues = JsonSerializer.Serialize(skill)
        };
        var payload = JsonSerializer.Serialize(audit);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO employee_outbox(event_id,payload,occurred_at) VALUES({audit.EventId},{payload}::jsonb,{audit.OccurredAt})",
            ct);
    }


    private async Task LockAndRequireSetupAccessAsync(CancellationToken token)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({actor.TenantId})", token);
        if (!await db.UserRoles.AnyAsync(
                ur => ur.TenantId == actor.TenantId && ur.UserId == actor.UserId && db.Roles.Any(r =>
                    r.Id == ur.RoleId && r.TenantId == actor.TenantId &&
                    (r.Code == "ADMIN" || r.Code == "HR" || r.Code == "HUMAN_RESOURCE")), token))
            throw new EmployeeProblem(403, "Administrator or HR access is required.");
    }


    private Task<bool> AnotherActiveAdminAsync(long userId, long adminRoleId, CancellationToken token)
    {
        return db.UserRoles.AnyAsync(
            r => r.TenantId == actor.TenantId && r.UserId != userId && r.RoleId == adminRoleId && db.Users.Any(u =>
                u.Id == r.UserId && u.TenantId == actor.TenantId && u.Status == UserStatus.Active), token);
    }

    private static SkillDto ToDto(Skill s)
    {
        return new SkillDto(s.Id, s.Code, s.Name, s.Category, s.Description, s.IsActive, s.CreatedAt, s.UpdatedAt);
    }

    private static DepartmentDto ToDto(CareerProfile d)
    {
        return new DepartmentDto(d.Id, d.Code, d.Name, d.IsActive, d.CreatedAt, d.UpdatedAt);
    }

    private static GradeDto ToDto(Grade g)
    {
        return new GradeDto(g.Id, g.Code, g.Family, g.Name, g.DisplayOrder, g.IsActive, g.CreatedAt, g.UpdatedAt);
    }
}