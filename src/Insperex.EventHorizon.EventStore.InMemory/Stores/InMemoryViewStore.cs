using System;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.InMemory.Stores
{
    public class InMemoryViewStore<TState> : AbstractInMemoryCrudStore<View<TState>>, IViewStore<TState> where TState : class, IState
    {
        private static readonly Type Type = typeof(TState);
        public InMemoryViewStore(Formatter formatter, InMemoryStoreClient crudDb) : base(crudDb, formatter.GetDatabase<View<TState>>(Type)) { }
    }
}
