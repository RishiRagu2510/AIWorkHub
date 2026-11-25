using Persona.Domain.Employees;
using IdentityTenant.Domain.Enums;
using Persona.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TenantContext;

namespace Persona.Api.Services.Employees;

public sealed class EmployeeProblem(int status, string message, string? code = null) : Exception(message)
{
    public int Status { get; } = status;
    public string? Code { get; } = code;
}

public sealed class EmployeeErrorsAttribute : ExceptionFilterAttribute, IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var principal = context.HttpContext.User;
        var users = principal.FindAll(TenantClaimTypes.UserId).ToArray();
        var tenants = principal.FindAll(TenantClaimTypes.TenantId).ToArray();
        if (users.Length != 1 || tenants.Length != 1 || !long.TryParse(users[0].Value, out var user) ||
            !long.TryParse(tenants[0].Value, out var tenant) || user <= 0 || tenant <= 0)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        context.HttpContext.RequestServices.GetRequiredService<TenantContext.TenantContext>().Set(user, tenant,
            principal.FindAll(TenantClaimTypes.Role).Select(x => x.Value));
        await next();
    }

    public override void OnException(ExceptionContext c)
    {
        var status = c.Exception switch
        {
            EmployeeProblem e => e.Status,
            DbUpdateConcurrencyException => 409,
            DbUpdateException { InnerException: PostgresException { SqlState: "23505" } } => 409,
            DbUpdateException { InnerException: PostgresException { SqlState: "23503" or "23514" } } => 400,
            _ => 0
        };
        if (status == 0) return;
        c.Result = new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = c.Exception is EmployeeProblem { Code: "ABSENCE_CONFLICT" } absence ? absence.Message :
                status == 409 ? "The record changed or already exists. Reload and retry." :
                c.Exception is EmployeeProblem p ? p.Message : "Invalid employee relationship."
        }) { StatusCode = status };
        if (c.Exception is EmployeeProblem { Code: not null } problem &&
            c.Result is ObjectResult { Value: ProblemDetails details }) details.Extensions["code"] = problem.Code;
        c.ExceptionHandled = true;
    }
}

public sealed class EmployeeAccess(PersonaDbContext db, ITenantContext actor, IHttpContextAccessor http)
{
    public IQueryable<Employee> Query => db.Set<Employee>().Where(e => e.TenantId == actor.TenantId && !e.IsDeleted);

    public bool Can(string permission)
    {
        return http.HttpContext!.User.HasClaim(TenantClaimTypes.Permission, permission);
    }

    public IQueryable<Employee> ManagerCandidates()
    {
        return Query.Where(e => e.EmploymentStatus == "ACTIVE" &&
                                db.Users.Any(u =>
                                    u.Id == e.UserId && u.TenantId == actor.TenantId &&
                                    u.Status == UserStatus.Active) && db.UserRoles.Any(ur =>
                                    ur.TenantId == actor.TenantId && ur.UserId == e.UserId && db.Roles.Any(r =>
                                        r.Id == ur.RoleId && r.TenantId == actor.TenantId &&
                                        (r.Code == "PEOPLE_MANAGER" ||
                                         r.Code == "MANAGER" || r.Code == "ADMIN" ||
                                         (r.Code == "HR" || r.Code == "HUMAN_RESOURCE") || r.Code == "EM" ||
                                         r.Code == "ENGINEERING_MANAGER" || r.Code == "CEO" || r.Code == "CTO" ||
                                         r.Code == "PM" ||
                                         r.Code == "PGM"))));
    }

    public Task<Employee> Get(long id, CancellationToken ct)
    {
        return Find(Query.Where(e => e.Id == id), ct);
    }

    public Task<Employee> Me(CancellationToken ct)
    {
        return Find(Query.Where(e => e.UserId == actor.UserId), ct);
    }

    private static async Task<Employee> Find(IQueryable<Employee> q, CancellationToken ct)
    {
        return await q.SingleOrDefaultAsync(ct) ?? throw new EmployeeProblem(404,
            "Employee profile not found. Ask your administrator to link your user account.");
    }

    public async Task RequireProfile(Employee e, bool write, CancellationToken ct)
    {
        if (Can("EMPLOYEE_UPDATE")) return;
        if (e.UserId == actor.UserId && (!write || Can("EMPLOYEE_UPDATE_SELF"))) return;
        if (!write && Can("EMPLOYEE_VIEW_TEAM") &&
            await Query.AnyAsync(m => m.Id == e.ManagerEmployeeId && m.UserId == actor.UserId, ct)) return;
        throw new EmployeeProblem(403, "You do not have access to this profile section.");
    }

    public void CheckVersion(Employee e)
    {
        var match = http.HttpContext!.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(match)) throw new EmployeeProblem(428, "If-Match is required.");
        if (match != $"\"{e.Version}\"") throw new EmployeeProblem(409, "The employee changed. Reload and retry.");
    }

    public Task Lock(CancellationToken ct)
    {
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({actor.TenantId})", ct);
    }

    public async Task Reference(long? id, string kind, CancellationToken ct)
    {
        if (id.HasValue && !await db.Set<EmployeeReference>()
                .AnyAsync(r => r.Id == id && r.TenantId == actor.TenantId && r.Kind == kind, ct))
            throw new EmployeeProblem(400, $"Invalid {kind.ToLowerInvariant()}.");
    }

    public static void Dates(DateOnly start, DateOnly? end)
    {
        if (start == default || end < start) throw new EmployeeProblem(400, "Invalid date range.");
    }

    public static void Url(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            (!Uri.TryCreate(value, UriKind.Absolute, out var u) || u.Scheme != "https"))
            throw new EmployeeProblem(400, "Use an HTTPS URL.");
    }
}