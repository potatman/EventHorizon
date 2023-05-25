using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.Locks;

public class LockFactory<T> where T : class, IState
{
    private readonly ICrudStore<Lock> _lockStore;

    public LockFactory(ILockStoreFactory<T> lockStore)
    {
        _lockStore = lockStore.GetLockStore();
    }

    public LockDisposable CreateLock(string id)
    {
        return new LockDisposable(_lockStore, id, TimeSpan.FromMinutes(5));
    }

    public LockDisposable CreateLock(string id, TimeSpan timeout)
    {
        // Create Lock
        return new LockDisposable(_lockStore, id, timeout);
    }
}