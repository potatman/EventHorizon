﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Test.Fakers;
using EventHorizon.EventStore.Test.Models;
using Microsoft.Extensions.DependencyInjection;
using NBench;

namespace EventHorizon.EventStore.Benchmark.Base;

public abstract class BaseInsertBenchmark
{
    private readonly IServiceProvider _provider;
    private Counter _counter;
    private ICrudStore<Snapshot<ExampleStoreState>> _snapshotStore;
    private ExampleStoreState[] _states;
    private Snapshot<ExampleStoreState>[] _snapshots;

    protected BaseInsertBenchmark(IServiceProvider provider)
    {
        _provider = provider;
    }

    [PerfSetup]
    public void Setup(BenchmarkContext context)
    {
        _snapshotStore = _provider.GetRequiredService<ISnapshotStoreFactory<ExampleStoreState>>().GetSnapshotStore();
        _states = EventStoreFakers.StateFaker.Generate(1000).ToArray();
        _snapshots = _states.Select(x => new Snapshot<ExampleStoreState>(x.Id, x)).ToArray();
        _counter = context.GetCounter("TestCounter");
    }

    [PerfCleanup]
    public async Task Cleanup()
    {
        await _snapshotStore.DropDatabaseAsync(CancellationToken.None);
    }

    [PerfBenchmark(Description = "Test Save Throughput",
        NumberOfIterations = 3, RunMode = RunMode.Throughput, RunTimeMilliseconds = 1000, TestMode = TestMode.Test)]
    [CounterThroughputAssertion("TestCounter", MustBe.GreaterThan, 0.5d)]
    public async Task BenchmarkBulkSave()
    {
        await _snapshotStore.UpsertAsync(_snapshots, CancellationToken.None);
        _counter.Increment();
    }

    [PerfBenchmark(Description = "Test Insert Throughput",
        NumberOfIterations = 3, RunMode = RunMode.Throughput, RunTimeMilliseconds = 1000, TestMode = TestMode.Test)]
    [CounterThroughputAssertion("TestCounter", MustBe.GreaterThan, 0.5d)]
    public async Task BenchmarkBulkInsert()
    {
        await _snapshotStore.InsertAsync(_snapshots, CancellationToken.None);
        _counter.Increment();
    }
}
