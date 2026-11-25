using System;
using System.Threading;
using System.Threading.Tasks;
using Jira.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Jira.Infrastructure.Persistence;

public sealed class JiraUnitOfWork : IJiraUnitOfWork
{
    private readonly JiraDbContext _dbContext;

    public JiraUnitOfWork(JiraDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null) { await action(cancellationToken); return; }
        await using var transaction =
            await _dbContext.Database.BeginTransactionAsync(
                cancellationToken);

        try
        {
            await action(cancellationToken);

            await transaction.CommitAsync(
                cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(
                cancellationToken);

            throw;
        }
    }
}