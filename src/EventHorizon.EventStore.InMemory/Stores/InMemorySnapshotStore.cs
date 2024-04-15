using System;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;

namespace EventHorizon.EventStore.InMemory.Stores
{
    public class InMemorySnapshotStore<TState> : AbstractInMemoryCrudStore<Snapshot<TState>>, ISnapshotStore<TState> where TState : class, IState
    {
        private static readonly Type Type = typeof(TState);
        public InMemorySnapshotStore(Formatter formatter, InMemoryStoreClient crudDb) : base(crudDb, formatter.GetDatabase<Snapshot<TState>>(Type)) { }
    }
}
