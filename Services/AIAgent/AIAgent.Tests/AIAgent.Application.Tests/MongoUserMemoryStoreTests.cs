using AIAgent.Application.Interfaces;
using AIAgent.Infrastructure.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.MongoDb;
using Xunit;

namespace AIAgent.Application.Tests;


public sealed class MongoFixture : IAsyncLifetime
{
    private readonly MongoDbContainer mongo = new MongoDbBuilder("mongo:8.0").Build();
    public string ConnectionString => mongo.GetConnectionString();
    public Task InitializeAsync() => mongo.StartAsync();
    public Task DisposeAsync() => mongo.DisposeAsync().AsTask();
}

[Trait("Category", "Docker")]
public class MongoUserMemoryStoreTests(MongoFixture mongo) : IClassFixture<MongoFixture>
{
    
    private MongoUserMemoryStore Store() =>
        new(new UserMemoryOptions(mongo.ConnectionString, "memory_" + Guid.NewGuid().ToString("N")), NullLogger<MongoUserMemoryStore>.Instance);

    [DockerFact]
    public async Task ANoteIsSaved_AndListedOldestFirst()
    {
        var store = Store();
        await store.AddAsync(1, 7, "First note");
        await Task.Delay(5);
        await store.AddAsync(1, 7, "Second note");

        var notes = await store.ListAsync(1, 7);

        Assert.Equal(["First note", "Second note"], notes.Select(n => n.Text));
        Assert.All(notes, n => Assert.True(UserMemoryRules.IsValidId(n.Id)));
    }

    [DockerFact]
    public async Task TheSameNote_IsKeptOnce_HoweverItIsWritten()
    {
        var store = Store();

        var first = await store.AddAsync(1, 7, "Main project is FLP360");
        var second = await store.AddAsync(1, 7, "  main PROJECT is flp360. ");

        Assert.Equal(MemoryAddStatus.Added, first.Status);
        Assert.Equal(MemoryAddStatus.Duplicate, second.Status);
        Assert.Single(await store.ListAsync(1, 7));
    }

    [DockerFact]
    public async Task TwoRequestsSavingTheSameNoteAtOnce_KeepOne()
    {
        var store = Store();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => store.AddAsync(1, 7, "Same note")));

        Assert.Equal(1, outcomes.Count(o => o.Status == MemoryAddStatus.Added));
        Assert.Single(await store.ListAsync(1, 7));
    }

    [DockerFact]
    public async Task TheListStopsAtTheLimit()
    {
        var store = Store();
        for (var i = 0; i < UserMemoryRules.MaxItems; i++) Assert.Equal(MemoryAddStatus.Added, (await store.AddAsync(1, 7, $"Fact {i}")).Status);

        var over = await store.AddAsync(1, 7, "One too many");

        Assert.Equal(MemoryAddStatus.Full, over.Status);
        Assert.Equal(UserMemoryRules.MaxItems, (await store.ListAsync(1, 7)).Count);
        Assert.Equal(MemoryAddStatus.Added, (await store.AddAsync(1, 8, "Another person is not affected")).Status);
    }

    [DockerFact]
    public async Task Notes_AreSeparatedByTenantAndByPerson()
    {
        var store = Store();
        await store.AddAsync(1, 7, "Asha");
        await store.AddAsync(1, 8, "Ben, same tenant");
        await store.AddAsync(2, 7, "Same user id, other tenant");

        Assert.Equal(["Asha"], (await store.ListAsync(1, 7)).Select(n => n.Text));
        Assert.Equal(["Ben, same tenant"], (await store.ListAsync(1, 8)).Select(n => n.Text));
        Assert.Equal(["Same user id, other tenant"], (await store.ListAsync(2, 7)).Select(n => n.Text));
    }

    [DockerFact]
    public async Task TheSameWords_CanBeKeptByTwoPeople()
    {
        var store = Store();

        Assert.Equal(MemoryAddStatus.Added, (await store.AddAsync(1, 7, "Main project is FLP360")).Status);
        Assert.Equal(MemoryAddStatus.Added, (await store.AddAsync(1, 8, "Main project is FLP360")).Status);
    }

    [DockerFact]
    public async Task ANoteCanBeDeleted_OnlyByItsOwner()
    {
        var store = Store();
        await store.AddAsync(1, 7, "Mine");
        var id = (await store.ListAsync(1, 7))[0].Id;

        Assert.False(await store.DeleteAsync(1, 8, id));    
        Assert.False(await store.DeleteAsync(2, 7, id));    
        Assert.Single(await store.ListAsync(1, 7));
        Assert.True(await store.DeleteAsync(1, 7, id));
        Assert.Empty(await store.ListAsync(1, 7));
    }

    [DockerTheory]
    [InlineData("not-an-id")]
    [InlineData("")]
    [InlineData("1:7:abcdefabcdef")]
    public async Task DeletingSomethingThatIsNotANoteId_DoesNothing(string id)
    {
        var store = Store();
        await store.AddAsync(1, 7, "Mine");

        Assert.False(await store.DeleteAsync(1, 7, id));
        Assert.Single(await store.ListAsync(1, 7));
    }

    [DockerFact]
    public async Task Clear_RemovesOnlyThatPersonsNotes_AndCountsThem()
    {
        var store = Store();
        await store.AddAsync(1, 7, "One");
        await store.AddAsync(1, 7, "Two");
        await store.AddAsync(1, 8, "Not mine");

        Assert.Equal(2, await store.ClearAsync(1, 7));
        Assert.Empty(await store.ListAsync(1, 7));
        Assert.Single(await store.ListAsync(1, 8));
        Assert.Equal(0, await store.ClearAsync(1, 7));
    }

    [DockerFact]
    public async Task AnUnreachableDatabase_ReportsMemoryUnavailable_InsteadOfHanging()
    {
        var down = new MongoUserMemoryStore(new UserMemoryOptions("mongodb://127.0.0.1:1/?serverSelectionTimeoutMS=300&connectTimeoutMS=300"), NullLogger<MongoUserMemoryStore>.Instance);

        await Assert.ThrowsAsync<UserMemoryUnavailableException>(() => down.ListAsync(1, 7));
    }

    [DockerFact]
    public async Task WithNoDatabaseConfigured_EverythingSaysSo()
    {
        var none = new UnavailableUserMemoryStore();

        await Assert.ThrowsAsync<UserMemoryUnavailableException>(() => none.ListAsync(1, 7));
        await Assert.ThrowsAsync<UserMemoryUnavailableException>(() => none.AddAsync(1, 7, "x"));
    }
}
