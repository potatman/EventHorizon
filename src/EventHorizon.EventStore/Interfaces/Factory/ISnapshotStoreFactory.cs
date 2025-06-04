using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.Interfaces.Factory;

public interface ISnapshotStoreFactory<T> where T : class, IState
{
    public ICrudStore<Snapshot<T>> GetSnapshotStore();
}
