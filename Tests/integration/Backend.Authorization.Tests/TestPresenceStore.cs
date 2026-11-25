using System.Collections.Concurrent;
using Jira.Api.Collaboration;
namespace Backend.Authorization.Tests;
public sealed class TestPresenceStore:IPresenceStore
{
    private readonly ConcurrentDictionary<(long,long,string),PresenceEntry> entries=new();
    public Task Pulse(long tenant,long issue,string connection,PresenceEntry entry){entries[(tenant,issue,connection)]=entry;return Task.CompletedTask;}
    public Task<IReadOnlyList<PresenceEntry>> List(long tenant,long issue)=>Task.FromResult<IReadOnlyList<PresenceEntry>>(entries.Where(e=>e.Key.Item1==tenant&&e.Key.Item2==issue).Select(e=>e.Value).ToArray());
}
