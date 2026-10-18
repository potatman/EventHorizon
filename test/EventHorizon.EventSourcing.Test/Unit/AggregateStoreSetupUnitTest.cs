using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.EventSourcing.Extensions;
using EventHorizon.EventSourcing.Samples.Models.Snapshots;
using EventHorizon.EventStore.InMemory;
using EventHorizon.EventStore.InMemory.Databases;
using EventHorizon.EventStore.InMemory.Extensions;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStreaming.InMemory.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventHorizon.EventSourcing.Test.Unit;

[Trait("Category", "Unit")]
public class AggregateStoreSetupUnitTest
{
    private readonly CountingSnapshotStoreFactory _factory = new();
    private readonly ServiceProvider _provider;
    private readonly EventSourcingClient<Account> _client;

    public AggregateStoreSetupUnitTest()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventHorizon(x => x
            .AddEventSourcing()
            .AddInMemorySnapshotStore()
            .AddInMemoryViewStore()
            .AddInMemoryEventStream());

        // A closed registration wins over the open-generic InMemory factory.
        services.AddSingleton<ISnapshotStoreFactory<Account>>(_factory);

        _provider = services.BuildServiceProvider();
        _client = _provider.GetRequiredService<EventSourcingClient<Account>>();
    }

    [Fact]
    public void BuildPerformsNoStoreSetup()
    {
        for (var i = 0; i < 50; i++)
            _client.Aggregator().Build();

        Assert.Equal(0, _factory.SetupCount);
    }

    [Fact]
    public void AggregatorReturnsANewBuilderPerCall()
    {
        var first = _client.Aggregator().BatchSize(5);
        var second = _client.Aggregator();

        Assert.NotSame(first, second);
        Assert.Null(second.Build().GetConfig().BatchSize);
    }

    [Fact]
    public async Task SetupRunsOncePerProcessAcrossAggregators()
    {
        var aggregators = Enumerable.Range(0, 20).Select(_ => _client.Aggregator().Build()).ToArray();

        await Task.WhenAll(aggregators.Select(x => x.GetAggregateFromStateAsync("stream-1", CancellationToken.None)));
        await aggregators[0].EnsureStoreSetupAsync(CancellationToken.None);

        Assert.Equal(1, _factory.SetupCount);
    }

    [Fact]
    public async Task FailedSetupIsRetriedOnNextUse()
    {
        _factory.FailNextSetup = true;
        var aggregator = _client.Aggregator().Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => aggregator.EnsureStoreSetupAsync(CancellationToken.None));
        await aggregator.EnsureStoreSetupAsync(CancellationToken.None);
        await aggregator.EnsureStoreSetupAsync(CancellationToken.None);

        Assert.Equal(2, _factory.SetupCount);
    }

    [Fact]
    public async Task SnapshotAndViewStoresAreSetUpIndependently()
    {
        await _client.Aggregator().Build().EnsureStoreSetupAsync(CancellationToken.None);

        var viewSetup = _provider.GetRequiredService<Aggregates.AggregateStoreSetup<View<Account>, Account>>();
        var snapshotSetup = _provider.GetRequiredService<Aggregates.AggregateStoreSetup<Snapshot<Account>, Account>>();

        Assert.True(snapshotSetup.IsComplete);
        Assert.False(viewSetup.IsComplete);
    }

    private sealed class CountingSnapshotStoreFactory : ISnapshotStoreFactory<Account>
    {
        private readonly CrudDatabase _db = new();
        private int _setupCount;

        public int SetupCount => _setupCount;
        public bool FailNextSetup { get; set; }

        public ICrudStore<Snapshot<Account>> GetSnapshotStore() => new CountingStore(this, new InMemoryCrudStore<Snapshot<Account>>(_db));

        private sealed class CountingStore : ICrudStore<Snapshot<Account>>
        {
            private readonly CountingSnapshotStoreFactory _owner;
            private readonly ICrudStore<Snapshot<Account>> _inner;

            public CountingStore(CountingSnapshotStoreFactory owner, ICrudStore<Snapshot<Account>> inner)
            {
                _owner = owner;
                _inner = inner;
            }

            public async Task SetupAsync(CancellationToken ct)
            {
                Interlocked.Increment(ref _owner._setupCount);

                // Widens the window for concurrent callers to race past the gate if it were broken.
                await Task.Delay(50, ct);
                if (_owner.FailNextSetup)
                {
                    _owner.FailNextSetup = false;
                    throw new InvalidOperationException("setup failed");
                }
            }

            public Task<Snapshot<Account>[]> GetAllAsync(string[] ids, CancellationToken ct) => _inner.GetAllAsync(ids, ct);
            public Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct) => _inner.GetLastUpdatedDateAsync(ct);
            public Task<DbResult> InsertAsync(Snapshot<Account>[] objs, CancellationToken ct) => _inner.InsertAsync(objs, ct);
            public Task<DbResult> UpsertAsync(Snapshot<Account>[] objs, CancellationToken ct) => _inner.UpsertAsync(objs, ct);
            public Task DeleteAsync(string[] ids, CancellationToken ct) => _inner.DeleteAsync(ids, ct);
            public Task DropDatabaseAsync(CancellationToken ct) => _inner.DropDatabaseAsync(ct);
        }
    }
}
