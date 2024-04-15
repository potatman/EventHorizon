using System;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStore.MongoDb.Attributes;

namespace EventHorizon.EventStore.MongoDb.Stores
{
    public class MongoViewStore<T> : AbstractMongoCrudStore<View<T>>, IViewStore<T> where T : class, IState
    {
        private static readonly Type Type = typeof(T);
        public MongoViewStore(Formatter formatter, AttributeUtil attributeUtil, MongoClientResolver clientResolver)
            : base(clientResolver.GetClient(),
                attributeUtil.GetOne<MongoCollectionAttribute>(Type),
                formatter.GetDatabase<View<T>>(Type)) { }
    }
}
