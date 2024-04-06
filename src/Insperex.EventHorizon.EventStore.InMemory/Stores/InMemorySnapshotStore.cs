using System;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;

namespace Insperex.EventHorizon.EventStore.InMemory.Stores
{
    public class InMemorySnapshotStore<TState> : AbstractInMemoryCrudStore<Snapshot<TState>>, ISnapshotStore<TState> where TState : class, IState
    {
        private static readonly Type Type = typeof(TState);
        public InMemorySnapshotStore(Formatter formatter, InMemoryStoreClient crudDb) : base(crudDb, formatter.GetDatabase<Snapshot<TState>>(Type)) { }
    }
}
