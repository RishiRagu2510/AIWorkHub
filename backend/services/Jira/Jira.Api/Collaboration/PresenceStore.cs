using System.Text.Json;
using StackExchange.Redis;
namespace Jira.Api.Collaboration;
public sealed record PresenceEntry(long UserId,string[] Roles,bool Editing,DateTimeOffset TypingUntil);
public interface IPresenceStore
{
    Task Pulse(long tenant,long issue,string connection,PresenceEntry entry);
    Task<IReadOnlyList<PresenceEntry>> List(long tenant,long issue);
}
public sealed class RedisPresenceStore(IConnectionMultiplexer redis):IPresenceStore
{
    private static string Key(long tenant,long issue)=>$"aiworkhub:presence:{tenant}:{issue}";
    public async Task Pulse(long tenant,long issue,string connection,PresenceEntry entry)
    {
        var db=redis.GetDatabase();var key=Key(tenant,issue);
        await db.StringSetAsync(key+":"+connection,JsonSerializer.Serialize(entry),TimeSpan.FromSeconds(45));
        await db.SortedSetAddAsync(key,connection,DateTimeOffset.UtcNow.AddSeconds(45).ToUnixTimeMilliseconds());
        await db.KeyExpireAsync(key,TimeSpan.FromSeconds(60));
    }
    public async Task<IReadOnlyList<PresenceEntry>> List(long tenant,long issue)
    {
        var db=redis.GetDatabase();var key=Key(tenant,issue);var now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await db.SortedSetRemoveRangeByScoreAsync(key,double.NegativeInfinity,now);
        var connections=await db.SortedSetRangeByScoreAsync(key,now,double.PositiveInfinity,take:100);
        if(connections.Length==0)return [];
        var values=await db.StringGetAsync(connections.Select(c=>(RedisKey)(key+":"+c)).ToArray());
        return values.Where(v=>v.HasValue).Select(v=>JsonSerializer.Deserialize<PresenceEntry>(v.ToString())!).ToArray();
    }
}
