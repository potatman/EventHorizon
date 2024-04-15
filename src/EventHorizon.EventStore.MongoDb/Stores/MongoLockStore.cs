using System;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.MongoDb.Attributes;

namespace EventHorizon.EventStore.MongoDb.Stores
{
    public class MongoLockStore<TState> : AbstractMongoCrudStore<Lock>, ILockStore<TState> where TState : IState
    {
        private static readonly Type Type = typeof(TState);
        public MongoLockStore(Formatter formatter, AttributeUtil attributeUtil, MongoClientResolver clientResolver)
            : base(clientResolver.GetClient(),
                attributeUtil.GetOne<MongoCollectionAttribute>(Type),
                formatter.GetDatabase<Lock>(Type)) { }
    }
}
