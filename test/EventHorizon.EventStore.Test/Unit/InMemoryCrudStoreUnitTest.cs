using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.InMemory;
using EventHorizon.EventStore.InMemory.Databases;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Test.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class InMemoryCrudStoreUnitTest
{
    private readonly CrudDatabase _db = new();

    [Fact]
    public async Task SnapshotAndViewOfTheSameStateUseSeparateTables()
    {
        var factory = new InMemoryEventStoreFactory<ExampleStoreState>(_db, NullLoggerFactory.Instance);
        var state = new ExampleStoreState { Id = "1" };

        await factory.GetSnapshotStore().UpsertAsync(new[] { new Snapshot<ExampleStoreState>("1", state) }, CancellationToken.None);
        await factory.GetViewStore().UpsertAsync(new[] { new View<ExampleStoreState>("1", 1, state, DateTime.UtcNow, DateTime.UtcNow) }, CancellationToken.None);

        Assert.Single(await factory.GetSnapshotStore().GetAllAsync(new[] { "1" }, CancellationToken.None));
        Assert.Single(await factory.GetViewStore().GetAllAsync(new[] { "1" }, CancellationToken.None));
    }

    [Fact]
    public async Task StatesWithTheSameSimpleNameUseSeparateTables()
    {
        var first = new InMemoryEventStoreFactory<First.Widget>(_db, NullLoggerFactory.Instance).GetSnapshotStore();
        var second = new InMemoryEventStoreFactory<Second.Widget>(_db, NullLoggerFactory.Instance).GetSnapshotStore();

        await first.UpsertAsync(new[] { new Snapshot<First.Widget>("1", new First.Widget { Id = "1" }) }, CancellationToken.None);

        Assert.Empty(await second.GetAllAsync(new[] { "1" }, CancellationToken.None));
    }

    [Fact]
    public async Task ConcurrentInsertsOfOneIdHaveExactlyOneWinner()
    {
        var store = new InMemoryCrudStore<Lock>(_db);

        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() =>
            store.InsertAsync(new[] { new Lock { Id = "lock", Owner = $"host-{i}", Expiration = DateTime.UtcNow.AddMinutes(1) } }, CancellationToken.None))));

        Assert.Equal(1, results.Count(x => x.PassedIds.Length == 1));
        Assert.Equal(31, results.Count(x => x.FailedIds.Length == 1));
    }
}

public static class First
{
    public class Widget : IState
    {
        public string Id { get; set; }
    }
}

public static class Second
{
    public class Widget : IState
    {
        public string Id { get; set; }
    }
}
