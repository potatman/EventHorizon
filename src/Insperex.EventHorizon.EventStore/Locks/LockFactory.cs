using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStore.Locks;

public class LockFactory<T> where T : class, IState
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ICrudStore<Lock> _lockStore;

    public LockFactory(ILockStoreFactory<T> lockStore, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _lockStore = lockStore.GetLockStore();
    }

    public LockDisposable CreateLock(string id, string hostname)
    {
        return new LockDisposable(_lockStore, id, hostname, TimeSpan.FromMinutes(5), _loggerFactory.CreateLogger<LockDisposable>());
    }

    public LockDisposable CreateLock(string id, string hostname, TimeSpan timeout)
    {
        // Create Lock
        return new LockDisposable(_lockStore, id, hostname, timeout, _loggerFactory.CreateLogger<LockDisposable>());
    }
}
