using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jira.Application.Abstractions.Persistence;

public interface IJiraUnitOfWork
{
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}