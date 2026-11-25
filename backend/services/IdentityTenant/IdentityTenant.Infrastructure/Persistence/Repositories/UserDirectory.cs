using IdentityTenant.Application.Abstractions.Persistence;
using IdentityTenant.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IdentityTenant.Infrastructure.Persistence.Repositories;

public sealed class UserDirectory(IdentityTenantDbContext db) : IUserDirectory
{
    public async Task<AssignmentUserDto?> GetAssignmentUserAsync(long tenantId, long userId, CancellationToken ct)
    {
        var user = await db.Users.Where(u => u.Id == userId && u.TenantId == tenantId && u.Status == UserStatus.Active).Select(u => new { u.DisplayName }).SingleOrDefaultAsync(ct);
        if (user is null) return null;
        var roles = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ur.TenantId == tenantId && ur.UserId == userId && r.TenantId == tenantId
            select r.Code).ToListAsync(ct);
        return roles.Count == 0 ? null : new(userId, tenantId, roles.Contains("ADMIN"), user.DisplayName);
    }

    public async Task<CollaborationUserDto?> GetCollaborationUserAsync(long tenantId,long userId,CancellationToken ct)
    {
        if(!await db.Users.AnyAsync(u=>u.Id==userId&&u.TenantId==tenantId&&u.Status==UserStatus.Active,ct))return null;
        var roles=await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ur.TenantId==tenantId&&ur.UserId==userId&&r.TenantId==tenantId select r.Code).Distinct().ToArrayAsync(ct);
        return roles.Length==0?null:new(userId,tenantId,roles);
    }
    private IQueryable<IdentityTenant.Domain.Entities.User> Query(long tenantId) => db.Users.AsNoTracking()
        .Where(u => u.TenantId == tenantId && u.Status == UserStatus.Active &&
            db.UserRoles.Any(ur => ur.TenantId == tenantId && ur.UserId == u.Id &&
                db.Roles.Any(r => r.Id == ur.RoleId && r.TenantId == tenantId && r.Code == "DEVELOPER")));

    public async Task<DeveloperPage> ListDevelopersAsync(long tenantId, int page, int pageSize, CancellationToken ct)
    {
        var query = Query(tenantId);
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.DisplayName).ThenBy(u => u.Id)
            .Skip(checked((page - 1) * pageSize)).Take(pageSize)
            .Select(u => new DeveloperDto(u.Id, u.TenantId, u.Email, u.DisplayName)).ToListAsync(ct);
        return new(items, count, page, pageSize);
    }
    public async Task<DeveloperDto?> GetDeveloperAsync(long tenantId, long userId, CancellationToken ct)
    {
        var user = await Query(tenantId).SingleOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? null : new DeveloperDto(user.Id, user.TenantId, user.Email, user.DisplayName);
    }
}
