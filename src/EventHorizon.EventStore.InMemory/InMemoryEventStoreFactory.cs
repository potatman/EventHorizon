using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.InMemory.Databases;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.InMemory;

public class InMemoryEventStoreFactory<T> : ISnapshotStoreFactory<T>, IViewStoreFactory<T>, ILockStoreFactory<T>
    where T : class, IState
{
    private readonly CrudDatabase _crudDb;
    private readonly ILoggerFactory _loggerFactory;

    public InMemoryEventStoreFactory(CrudDatabase crudDb, ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _crudDb = crudDb;
    }

    public ICrudStore<Lock> GetLockStore()
    {
        return new InMemoryCrudStore<Lock>(_crudDb);
    }

    public ICrudStore<Snapshot<T>> GetSnapshotStore()
    {
        return new InMemoryCrudStore<Snapshot<T>>(_crudDb);
    }

    public ICrudStore<View<T>> GetViewStore()
    {
        return new InMemoryCrudStore<View<T>>(_crudDb);
    }
}
