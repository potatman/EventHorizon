using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.Interfaces.Stores
{
    public interface ISnapshotStore<T> : ICrudStore<Snapshot<T>> where T : class, IState
    {

    }
}
