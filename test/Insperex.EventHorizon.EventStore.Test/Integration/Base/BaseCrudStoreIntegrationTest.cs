using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStore.Test.Fakers;
using Insperex.EventHorizon.EventStore.Test.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStore.Test.Integration.Base;

[Collection("Integration")]
public abstract class BaseCrudStoreIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private Stopwatch _stopwatch;
    private readonly ISnapshotStore<ExampleStoreState> _snapshotStore;
    private readonly List<ExampleStoreState> _states;
    private CancellationTokenSource _cts;

    protected BaseCrudStoreIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        _outputHelper = outputHelper;

        _snapshotStore = provider.GetRequiredService<ISnapshotStore<ExampleStoreState>>();
        _stopwatch = Stopwatch.StartNew();
        _states = EventStoreFakers.StateFaker.Generate(1000);
    }

    public Task InitializeAsync()
    {
        _stopwatch = Stopwatch.StartNew();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        _snapshotStore.MigrateAsync(CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _snapshotStore.DropDatabaseAsync(_cts.Token);
    }

    [Fact]
    public async Task TestSaveAndLoad()
    {
        // Act
        var past = DateTime.UtcNow.AddDays(-1);
        var now = DateTime.UtcNow;
        var expected = new Snapshot<ExampleStoreState>("MultipleSnapshots", 1, _states.First(), past, now);
        var result = await _snapshotStore.UpsertAllAsync(new [] {expected}, _cts.Token);
        var snapshots = await _snapshotStore.GetAllAsync(new [] {expected.Id}, _cts.Token);

        // Assert
        var actual = snapshots.First();
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.SequenceId, actual.SequenceId);
        Assert.Equal(expected.Payload.Id, actual.Payload.Id);
        Assert.Equal(expected.Payload.Name, actual.Payload.Name);
        Assert.Single(result.PassedIds);
    }

    [Fact]
    public async Task TestDeleteAndLoad()
    {
        // Act
        var past = DateTime.UtcNow.AddDays(-1);
        var now = DateTime.UtcNow;
        var expected = new Snapshot<ExampleStoreState>("MultipleSnapshots", 1, _states.First(), past, now);
        var result = await _snapshotStore.UpsertAllAsync(new [] {expected}, _cts.Token);
        await _snapshotStore.DeleteAllAsync(new [] {expected.Id}, _cts.Token);
        var snapshots = await _snapshotStore.GetAllAsync(new [] {expected.Id}, _cts.Token);

        // Assert
        Assert.Null(snapshots.FirstOrDefault());
        Assert.Single(result.PassedIds);
    }
}
