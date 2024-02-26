using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.InMemory.Stores
{
    public class InMemorySnapshotStore<T> : AbstractInMemoryCrudStore<Snapshot<T>>, ISnapshotStore<T> where T : IState
    {
        public InMemorySnapshotStore(InMemoryStoreClient crudDb) : base(crudDb) { }
    }
}
