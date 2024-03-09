using System;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.InMemory.Stores
{
    public class InMemoryLockStore<TState> : AbstractInMemoryCrudStore<Lock>, ILockStore<TState> where TState : IState
    {
        private static readonly Type Type = typeof(TState);
        public InMemoryLockStore(Formatter formatter, InMemoryStoreClient crudDb) : base(crudDb, formatter.GetDatabase<Lock>(Type)) { }
    }
}
