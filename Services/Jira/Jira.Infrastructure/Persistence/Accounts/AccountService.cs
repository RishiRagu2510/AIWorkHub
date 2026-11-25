using Jira.Application.Services;
using Jira.Contracts.Projects;
using Jira.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using TenantContext;

namespace Jira.Infrastructure.Persistence.Accounts;





public sealed class AccountService(JiraDbContext db, JiraResourceScope scope, ITenantContext context, WritePrecondition precondition)
{
    private Task Lock(CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-context.TenantId})", ct);

    private async Task<Account> Find(long id, CancellationToken ct) =>
        await db.Accounts.SingleOrDefaultAsync(a => a.TenantId == context.TenantId && a.Id == id, ct) ?? throw new ResourceNotFoundException();

    private async Task EnsureUnique(string name, long id, CancellationToken ct)
    {
        if (await db.Accounts.AnyAsync(a => a.TenantId == context.TenantId && a.Id != id && a.Name.ToUpper() == name.Trim().ToUpper(), ct))
            throw new ProjectConflictException("An account with this name already exists.");
    }

    private static AccountDto ToDto(Account a) => new(a.Id, a.TenantId, a.Name, a.Status, a.Version);

    
    public async Task<IReadOnlyList<AccountSummaryDto>> List(CancellationToken ct)
    {
        var projects = scope.Projects(context.TenantId);
        var admin = context.Roles.Contains("ADMIN");
        return await db.Accounts.AsNoTracking()
            .Where(a => a.TenantId == context.TenantId && (admin || projects.Any(p => p.AccountId == a.Id)))
            .OrderBy(a => a.Name)
            .Select(a => new AccountSummaryDto(a.Id, a.Name, a.Status, a.Version, projects.Count(p => p.AccountId == a.Id)))
            .ToArrayAsync(ct);
    }

    public async Task<AccountDto> Create(AccountRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var account = new Account(context.TenantId, request.Name);
        account.Update(request.Name, request.Status);
        await EnsureUnique(account.Name, 0, ct);
        db.Accounts.Add(account);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(account);
    }

    public async Task<AccountDto> Update(long id, AccountRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var account = await Find(id, ct);
        precondition.Check(account.Version);
        account.Update(request.Name, request.Status);
        await EnsureUnique(account.Name, id, ct);
        
        await db.Projects.Where(p => p.TenantId == context.TenantId && p.AccountId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Name, account.Name).SetProperty(p => p.UpdatedAt, DateTimeOffset.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToDto(account);
    }

    public async Task Delete(long id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(ct);
        var account = await Find(id, ct);
        precondition.Check(account.Version);
        if (await db.Projects.AnyAsync(p => p.TenantId == context.TenantId && p.AccountId == id, ct))
            throw new ProjectConflictException("The account contains projects. Archive it or move its projects before deleting.");
        db.Accounts.Remove(account);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
