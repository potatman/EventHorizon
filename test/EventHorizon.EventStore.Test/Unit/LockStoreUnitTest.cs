using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Locks;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.Test.Models;
using EventHorizon.EventStore.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventStore.Test.Unit;

[Trait("Category", "Unit")]
public class LockStoreUnitTest : IAsyncLifetime
{
    private readonly LockFactory<ExampleStoreState> _lockFactory;
    private readonly ITestOutputHelper _outputHelper;
    private Stopwatch _stopwatch;
    private readonly Faker _faker;
    private readonly ICrudStore<Lock> _lockStore;

    public LockStoreUnitTest(ITestOutputHelper outputHelper)
    {
        var provider = HostTestUtil.GetInMemoryHost(outputHelper).Services;
        _outputHelper = outputHelper;
        _lockStore = provider.GetRequiredService<ILockStoreFactory<ExampleStoreState>>().GetLockStore();
        _lockFactory = provider.GetRequiredService<LockFactory<ExampleStoreState>>();
        _stopwatch = Stopwatch.StartNew();
        _faker = new Faker();
    }

    [Fact]
    public async Task TestLockWaits()
    {
        var sw = Stopwatch.StartNew();

        // Act
        var id = _faker.Random.AlphaNumeric(10);
        await using var lock1 = _lockFactory.CreateLock(id, "host-1", TimeSpan.FromSeconds(5));
        await using var lock2 = _lockFactory.CreateLock(id, "host-2", TimeSpan.FromSeconds(1));
        await lock1.WaitForLockAsync();
        await lock2.WaitForLockAsync();

        // Asset
        Assert.True(sw.ElapsedMilliseconds > 3000);
    }

    [Fact]
    public async Task TestLockHolds()
    {
        // Act
        var id = _faker.Random.AlphaNumeric(10);
        await using var lock1 = _lockFactory.CreateLock(id, "host-1", TimeSpan.FromSeconds(120));
        await using var lock2 = _lockFactory.CreateLock(id, "host-2", TimeSpan.FromSeconds(120));
        var isLocked1 = await lock1.TryLockAsync();
        var isLocked2 = await lock2.TryLockAsync();

        // Asset
        Assert.True(isLocked1);
        Assert.False(isLocked2);
    }

    [Fact]
    public async Task TestAllowsSameHost()
    {
        // Act
        var id = _faker.Random.AlphaNumeric(10);
        await using var lock1 = _lockFactory.CreateLock(id, "host-1", TimeSpan.FromSeconds(120));
        await using var lock2 = _lockFactory.CreateLock(id, "host-1", TimeSpan.FromSeconds(120));
        var isLocked1 = await lock1.TryLockAsync();
        var isLocked2 = await lock2.TryLockAsync();

        // Asset
        Assert.True(isLocked1);
        Assert.True(isLocked2);
    }

    [Fact]
    public async Task TestLockReleases()
    {
        // Act
        var id = _faker.Random.AlphaNumeric(10);
        await using var lock1 = _lockFactory.CreateLock(id, "host-1", TimeSpan.FromSeconds(120));
        await using var lock2 = _lockFactory.CreateLock(id, "host-2", TimeSpan.FromSeconds(120));
        var isLocked1 = await lock1.TryLockAsync();
        await lock1.ReleaseAsync();
        var isLocked2 = await lock1.TryLockAsync();

        // Asset
        Assert.True(isLocked1);
        Assert.True(isLocked2);
    }


    [Fact]
    public async Task TestLockTimeoutReleases()
    {
        // Act
        var id = _faker.Random.AlphaNumeric(10);
        await using var lock1 = _lockFactory.CreateLock(id, "host-1", TimeSpan.Zero);
        await using var lock2 = _lockFactory.CreateLock(id, "host-2", TimeSpan.Zero);
        var isLocked1 = await lock1.TryLockAsync();
        var isLocked2 = await lock1.TryLockAsync();

        // Asset
        Assert.True(isLocked1);
        Assert.True(isLocked2);
    }

    [Fact]
    public async Task TestLockDifferent()
    {
        var id1 = _faker.Random.AlphaNumeric(10);
        var id2 = _faker.Random.AlphaNumeric(10);
        await using var lock1 = _lockFactory.CreateLock(id1, "host-1", TimeSpan.FromSeconds(120));
        await using var lock2 = _lockFactory.CreateLock(id2, "host-2", TimeSpan.FromSeconds(120));

        // Act
        var isLocked1 = await lock1.TryLockAsync();
        var isLocked2 = await lock2.TryLockAsync();

        // Asset
        Assert.True(isLocked1);
        Assert.True(isLocked2);
    }

    public Task InitializeAsync()
    {
        _stopwatch = Stopwatch.StartNew();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _lockStore.DropDatabaseAsync(CancellationToken.None);
    }
}
