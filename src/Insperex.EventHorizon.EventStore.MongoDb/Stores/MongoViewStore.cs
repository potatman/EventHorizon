using System;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStore.MongoDb.Attributes;

namespace Insperex.EventHorizon.EventStore.MongoDb.Stores
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
