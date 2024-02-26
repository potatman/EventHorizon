using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.InMemory.Stores
{
    public class InMemoryViewStore<T> : AbstractInMemoryCrudStore<View<T>>, IViewStore<T> where T : IState
    {
        public InMemoryViewStore(InMemoryStoreClient crudDb) : base(crudDb) { }
    }
}
