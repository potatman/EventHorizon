using System;
using System.Linq;
using System.Threading;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Test.Fakers;
using EventHorizon.EventStore.Test.Models;
using Microsoft.Extensions.DependencyInjection;
using NBench;

namespace EventHorizon.EventStore.Benchmark.Base;

public abstract class BaseUpsertBenchmark
{
    private readonly IServiceProvider _provider;
    private Counter _counter;
    private ISnapshotStore<ExampleStoreState> _snapshotStore;
    private ExampleStoreState[] _states;
    private Snapshot<ExampleStoreState>[] _snapshots;

    protected BaseUpsertBenchmark(IServiceProvider provider)
    {
        _provider = provider;
    }

    [PerfSetup]
    public void Setup(BenchmarkContext context)
    {
        _snapshotStore = _provider.GetRequiredService<ISnapshotStore<ExampleStoreState>>();
        _states = EventStoreFakers.StateFaker.Generate(1000).ToArray();
        _snapshots = _states.Select(x => new Snapshot<ExampleStoreState>(x.Id, x)).ToArray();
        _counter = context.GetCounter("TestCounter");
    }

    [PerfCleanup]
    public void Cleanup()
    {
        _snapshotStore.DropDatabaseAsync(CancellationToken.None).Wait();
    }

    [PerfBenchmark(Description = "Test Save Throughput",
        NumberOfIterations = 3, RunMode = RunMode.Throughput, RunTimeMilliseconds = 1000, TestMode = TestMode.Test)]
    [CounterThroughputAssertion("TestCounter", MustBe.GreaterThan, 0.5d)]
    public void BenchmarkBulkSave()
    {
        _snapshotStore.UpsertAllAsync(_snapshots, CancellationToken.None).Wait();
        _counter.Increment();
    }
}
