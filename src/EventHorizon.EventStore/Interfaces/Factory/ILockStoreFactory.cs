using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.Interfaces.Factory;

public interface ILockStoreFactory<T> where T : class, IState
{
    public ICrudStore<Lock> GetLockStore();
}
