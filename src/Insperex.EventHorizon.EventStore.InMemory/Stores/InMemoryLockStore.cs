using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.InMemory.Databases;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.InMemory.Stores
{
    public class InMemoryLockStore<T> : AbstractInMemoryCrudStore<Lock>, ILockStore<T> where T : IState
    {
        public InMemoryLockStore(CrudDatabase crudDb) : base(crudDb) { }
    }
}
