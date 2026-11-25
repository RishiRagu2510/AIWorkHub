using IdentityTenant.Contracts;
using IdentityTenant.Domain.Enums;
using IdentityTenant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Api.Services.Timesheets;

public sealed class TimesheetApproverService(IdentityTenantDbContext db)
{
    public const int PageSize = 100;

    public async Task<IReadOnlyList<TimesheetApproverDto>> List(long tenant, int page, CancellationToken ct)
    {
        var grants = from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            join rp in db.RolePermissions on r.Id equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.TenantId == tenant && r.TenantId == tenant
            select new { ur.UserId, p.Code };
        var users = await db.Users.Where(u => u.TenantId == tenant && u.Status == UserStatus.Active &&
                                              grants.Any(g => g.UserId == u.Id && g.Code == "TIMESHEET_APPROVE") &&
                                              grants.Any(g => g.UserId == u.Id && g.Code == "TIMESHEET_TEAM_VIEW"))
            .OrderBy(u => u.Id).Skip((page - 1) * PageSize).Take(PageSize).Select(u => new { u.Id, u.DisplayName })
            .ToListAsync(ct);
        var ids = users.Select(u => u.Id).ToArray();
        var roles = await (from ur in db.UserRoles
            join r in db.Roles on ur.RoleId equals r.Id
            where ur.TenantId == tenant && r.TenantId == tenant && ids.Contains(ur.UserId)
            select new { ur.UserId, r.Code }).ToListAsync(ct);
        return users.Select(u =>
            new TimesheetApproverDto(u.Id, u.DisplayName,
                roles.Where(r => r.UserId == u.Id).Select(r => r.Code).ToArray())).ToList();
    }
}