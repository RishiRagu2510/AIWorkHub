using DotNet.Testcontainers.Builders;
using Jira.Api.Collaboration;
using StackExchange.Redis;
namespace Backend.Authorization.Tests;
public sealed class RedisPresenceTests
{
    [Fact]
    public async Task IndependentInstancesSharePresenceAndTenantKeysRemainIsolated()
    {
        await using var redis=new ContainerBuilder("redis:7.4").WithPortBinding(6379,true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379)).Build();
        await redis.StartAsync();
        using var a=await ConnectionMultiplexer.ConnectAsync($"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}");
        using var b=await ConnectionMultiplexer.ConnectAsync($"{redis.Hostname}:{redis.GetMappedPublicPort(6379)}");
        var writer=new RedisPresenceStore(a);var reader=new RedisPresenceStore(b);
        Assert.Empty(await reader.List(1,10));
        await writer.Pulse(1,10,"connection",new PresenceEntry(101,["DEVELOPER"],true,DateTimeOffset.UtcNow.AddSeconds(5)));
        var entry=Assert.Single(await reader.List(1,10));Assert.Equal(101,entry.UserId);Assert.True(entry.Editing);
        Assert.Empty(await reader.List(2,10));Assert.Empty(await reader.List(1,11));
        var ttl=await a.GetDatabase().KeyTimeToLiveAsync("aiworkhub:presence:1:10:connection");Assert.True(ttl>TimeSpan.Zero&&ttl<=TimeSpan.FromSeconds(45));
        await a.GetDatabase().KeyDeleteAsync("aiworkhub:presence:1:10:connection");Assert.Empty(await reader.List(1,10));
    }
}
