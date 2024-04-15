using System;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.InMemory.Stores
{
    public class InMemoryLockStore<TState> : AbstractInMemoryCrudStore<Lock>, ILockStore<TState> where TState : IState
    {
        private static readonly Type Type = typeof(TState);
        public InMemoryLockStore(Formatter formatter, InMemoryStoreClient crudDb) : base(crudDb, formatter.GetDatabase<Lock>(Type)) { }
    }
}
