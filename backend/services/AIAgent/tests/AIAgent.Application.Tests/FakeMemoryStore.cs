using AIAgent.Application.Interfaces;

namespace AIAgent.Application.Tests;


public sealed class FakeMemoryStore : IUserMemoryStore
{
    private readonly List<(long Tenant, long User, MemoryItem Item)> _rows = [];
    private long _clock = 1_000;

    public bool Down { get; set; }
    public int Reads { get; private set; }

    private void Check()
    {
        if (Down) throw new UserMemoryUnavailableException("Memory is not reachable right now.");
    }

    public Task<IReadOnlyList<MemoryItem>> ListAsync(long tenantId, long userId, CancellationToken cancellationToken = default)
    {
        Check();
        Reads++;
        return Task.FromResult<IReadOnlyList<MemoryItem>>(_rows.Where(r => r.Tenant == tenantId && r.User == userId).Select(r => r.Item).OrderBy(i => i.CreatedAt).ToList());
    }

    public Task<MemoryAddOutcome> AddAsync(long tenantId, long userId, string text, CancellationToken cancellationToken = default)
    {
        Check();
        var note = UserMemoryRules.Normalize(text);
        var id = UserMemoryRules.IdFor(note);
        var mine = _rows.Where(r => r.Tenant == tenantId && r.User == userId).ToList();
        if (mine.Any(r => r.Item.Id == id)) return Task.FromResult(new MemoryAddOutcome(MemoryAddStatus.Duplicate, null));
        if (mine.Count >= UserMemoryRules.MaxItems) return Task.FromResult(new MemoryAddOutcome(MemoryAddStatus.Full, null));
        var item = new MemoryItem(id, note, ++_clock);
        _rows.Add((tenantId, userId, item));
        return Task.FromResult(new MemoryAddOutcome(MemoryAddStatus.Added, item));
    }

    public Task<bool> DeleteAsync(long tenantId, long userId, string id, CancellationToken cancellationToken = default)
    {
        Check();
        return Task.FromResult(_rows.RemoveAll(r => r.Tenant == tenantId && r.User == userId && r.Item.Id == id) > 0);
    }

    public Task<long> ClearAsync(long tenantId, long userId, CancellationToken cancellationToken = default)
    {
        Check();
        return Task.FromResult((long)_rows.RemoveAll(r => r.Tenant == tenantId && r.User == userId));
    }
}
