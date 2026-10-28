using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Models;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventSourcing.Extensions;
using EventHorizon.EventSourcing.Samples.Models.Actions;
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
public class AggregatorStoreFailureUnitTest
{
    private readonly FaultyStoreFactory _factory = new();
    private readonly Aggregator<Snapshot<Account>, Account> _aggregator;
    private readonly List<MessageContext<Event>> _nacked = new();

    public AggregatorStoreFailureUnitTest()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddEventHorizon(x => x
            .AddEventSourcing()
            .AddInMemorySnapshotStore()
            .AddInMemoryViewStore()
            .AddInMemoryEventStream());
        services.AddSingleton<ISnapshotStoreFactory<Account>>(_factory);

        var client = services.BuildServiceProvider().GetRequiredService<EventSourcingClient<Account>>();
        _aggregator = client.Aggregator().Build();

        var config = _aggregator.GetConfig();
        config.RetryBaseDelay = TimeSpan.FromMilliseconds(1);
        config.RetryMaxDelay = TimeSpan.FromMilliseconds(5);
        config.MaxDocumentAttempts = 3;
    }

    [Fact]
    public async Task WholeStoreFailureIsRetriedUntilSaved()
    {
        _factory.FailingUpserts = 4;

        await HandleAsync(Credit("a", 1, 100), Credit("b", 1, 50));

        Assert.Empty(_nacked);
        Assert.Equal(5, _factory.UpsertCalls);
        Assert.Equal(100, (await LoadAsync("a")).State.Amount);
        Assert.Equal(50, (await LoadAsync("b")).State.Amount);
    }

    [Fact]
    public async Task RejectedDocumentIsNackedWithoutReapplyingSavedStreams()
    {
        _factory.RejectedIds.Add("bad");

        await HandleAsync(Credit("good", 1, 100), Credit("bad", 1, 10), Credit("bad", 2, 10));

        Assert.Equal(new[] { "bad", "bad" }, _nacked.Select(x => x.Data.StreamId));
        Assert.Equal(3, _factory.UpsertCalls);
        Assert.Equal(100, (await LoadAsync("good")).State.Amount);
        Assert.Null(await LoadAsync("bad"));
    }

    [Fact]
    public async Task StoppingDuringRetryNacksPendingMessages()
    {
        _factory.FailingUpserts = int.MaxValue;
        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        var batch = new[] { Credit("a", 1, 100) };

        await _aggregator.HandleWithRetryAsync(batch, x => _nacked.AddRange(x), stopping.Token);

        Assert.Equal(batch, _nacked);
    }

    [Fact]
    public async Task HandleAsyncReportsStoreFailureAs503()
    {
        _factory.FailingUpserts = 1;

        var responses = await _aggregator.HandleAsync(new[] { new Request("a", new Deposit(5)) }, CancellationToken.None);

        Assert.Equal((int)HttpStatusCode.ServiceUnavailable, Assert.Single(responses).StatusCode);
    }

    private Task<Response[]> HandleAsync(params MessageContext<Event>[] batch) =>
        _aggregator.HandleWithRetryAsync(batch, x => _nacked.AddRange(x), CancellationToken.None);

    private async Task<Snapshot<Account>> LoadAsync(string id) =>
        (await _factory.Inner.GetAllAsync(new[] { id }, CancellationToken.None)).SingleOrDefault();

    private static MessageContext<Event> Credit(string streamId, long sequenceId, int amount) => new()
    {
        Data = new Event(streamId, sequenceId, new AccountCredited(amount)),
        TopicData = new TopicData(Guid.NewGuid().ToString(), "topic", DateTime.UtcNow)
    };

    private sealed class FaultyStoreFactory : ISnapshotStoreFactory<Account>
    {
        public ICrudStore<Snapshot<Account>> Inner { get; } = new InMemoryCrudStore<Snapshot<Account>>(new CrudDatabase());
        public int FailingUpserts { get; set; }
        public HashSet<string> RejectedIds { get; } = new();
        public int UpsertCalls { get; private set; }

        public ICrudStore<Snapshot<Account>> GetSnapshotStore() => new FaultyStore(this);

        private sealed class FaultyStore : ICrudStore<Snapshot<Account>>
        {
            private readonly FaultyStoreFactory _owner;

            public FaultyStore(FaultyStoreFactory owner)
            {
                _owner = owner;
            }

            public async Task<DbResult> UpsertAsync(Snapshot<Account>[] objs, CancellationToken ct)
            {
                _owner.UpsertCalls++;
                if (_owner.FailingUpserts-- > 0)
                    throw new InvalidOperationException("store unavailable");

                var accepted = objs.Where(x => !_owner.RejectedIds.Contains(x.Id)).ToArray();
                await _owner.Inner.UpsertAsync(accepted, ct);
                return new DbResult
                {
                    PassedIds = accepted.Select(x => x.Id).ToArray(),
                    FailedIds = objs.Where(x => _owner.RejectedIds.Contains(x.Id)).Select(x => x.Id).ToArray()
                };
            }

            public Task SetupAsync(CancellationToken ct) => Task.CompletedTask;
            public Task<Snapshot<Account>[]> GetAllAsync(string[] ids, CancellationToken ct) => _owner.Inner.GetAllAsync(ids, ct);
            public Task<DateTime> GetLastUpdatedDateAsync(CancellationToken ct) => _owner.Inner.GetLastUpdatedDateAsync(ct);
            public Task<DbResult> InsertAsync(Snapshot<Account>[] objs, CancellationToken ct) => _owner.Inner.InsertAsync(objs, ct);
            public Task DeleteAsync(string[] ids, CancellationToken ct) => _owner.Inner.DeleteAsync(ids, ct);
            public Task DropDatabaseAsync(CancellationToken ct) => _owner.Inner.DropDatabaseAsync(ct);
        }
    }
}
