using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Test.Fakers;
using EventHorizon.EventStore.Test.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventStore.Test.Integration.Base;

[Collection("Integration")]
public abstract class BaseCrudStoreIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private Stopwatch _stopwatch;
    private readonly ICrudStore<Snapshot<ExampleStoreState>> _snapshotStore;
    private readonly List<ExampleStoreState> _states;
    private CancellationTokenSource _cts;

    protected BaseCrudStoreIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        _outputHelper = outputHelper;

        _snapshotStore = provider.GetRequiredService<ISnapshotStoreFactory<ExampleStoreState>>().GetSnapshotStore();
        _stopwatch = Stopwatch.StartNew();
        _states = EventStoreFakers.StateFaker.Generate(1000);
    }

    public Task InitializeAsync()
    {
        _stopwatch = Stopwatch.StartNew();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
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
        var result = await _snapshotStore.UpsertAsync(new [] {expected}, _cts.Token);
        var snapshots = await _snapshotStore.GetAllAsync(new [] {expected.Id}, _cts.Token);

        // Assert
        var actual = snapshots.First();
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.SequenceId, actual.SequenceId);
        Assert.Equal(expected.State.Id, actual.State.Id);
        Assert.Equal(expected.State.Name, actual.State.Name);
        Assert.Single(result.PassedIds);
    }

    [Fact]
    public async Task TestGetLastUpdatedDateAsync()
    {
        // Act
        var past = DateTime.UtcNow.AddDays(-1);
        var now = DateTime.UtcNow;
        var expected = new Snapshot<ExampleStoreState>("NormalSnapshot", 1, _states.First(), past, now);
        await _snapshotStore.UpsertAsync(new [] {expected}, _cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(1), _cts.Token); // NOTE: Delay is for elastic refresh
        var minDateTime = await _snapshotStore.GetLastUpdatedDateAsync(_cts.Token);

        // Assert
        Assert.True(Math.Truncate((expected.UpdatedDate - minDateTime).TotalMilliseconds) == 0, $"expected {expected.UpdatedDate}, actual {minDateTime}");
    }

    [Fact]
    public async Task TestDeleteAndLoad()
    {
        // Act
        var past = DateTime.UtcNow.AddDays(-1);
        var now = DateTime.UtcNow;
        var expected = new Snapshot<ExampleStoreState>("MultipleSnapshots", 1, _states.First(), past, now);
        var result = await _snapshotStore.UpsertAsync(new [] {expected}, _cts.Token);
        await _snapshotStore.DeleteAsync(new [] {expected.Id}, _cts.Token);
        var snapshots = await _snapshotStore.GetAllAsync(new [] {expected.Id}, _cts.Token);

        // Assert
        Assert.Null(snapshots.FirstOrDefault());
        Assert.Single(result.PassedIds);
    }
}
