using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Locks;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStore.Test.Models;
using Insperex.EventHorizon.EventStore.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStore.Test.Unit;

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
        using var lock1 = _lockFactory.CreateLock(id, TimeSpan.FromSeconds(5));
        using var lock2 = _lockFactory.CreateLock(id, TimeSpan.FromSeconds(1));
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
        using var lock1 = _lockFactory.CreateLock(id, TimeSpan.FromSeconds(120));
        using var lock2 = _lockFactory.CreateLock(id, TimeSpan.FromSeconds(120));
        var isLocked1 = await lock1.TryLockAsync();
        var isLocked2 = await lock1.TryLockAsync();
        
        // Asset
        Assert.True(isLocked1);
        Assert.False(isLocked2);
    }

    [Fact]
    public async Task TestLockReleases()
    {
        // Act
        var id = _faker.Random.AlphaNumeric(10);
        using var lock1 = _lockFactory.CreateLock(id, TimeSpan.FromSeconds(120));
        using var lock2 = _lockFactory.CreateLock(id, TimeSpan.FromSeconds(120));
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
        using var lock1 = _lockFactory.CreateLock(id, TimeSpan.Zero);
        using var lock2 = _lockFactory.CreateLock(id, TimeSpan.Zero);
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
        using var lock1 = _lockFactory.CreateLock(id1, TimeSpan.FromSeconds(120));
        using var lock2 = _lockFactory.CreateLock(id2, TimeSpan.FromSeconds(120));
        
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