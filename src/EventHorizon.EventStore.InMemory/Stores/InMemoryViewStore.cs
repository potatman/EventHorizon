using System;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.InMemory.Stores
{
    public class InMemoryViewStore<TState> : AbstractInMemoryCrudStore<View<TState>>, IViewStore<TState> where TState : class, IState
    {
        private static readonly Type Type = typeof(TState);
        public InMemoryViewStore(Formatter formatter, InMemoryStoreClient crudDb) : base(crudDb, formatter.GetDatabase<View<TState>>(Type)) { }
    }
}
