using System;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStore.ElasticSearch.Stores
{
    public class ElasticLockStore<T> : AbstractElasticCrudStore<Lock>, ILockStore<T>
        where T : IState
    {
        private static readonly Type Type = typeof(T);

        public ElasticLockStore(AttributeUtil attributeUtil, ElasticClientResolver clientResolver, ILogger<ElasticLockStore<T>> logger)
            : base(attributeUtil.GetOne<ElasticIndexAttribute>(Type), clientResolver.GetClient(), attributeUtil.GetOne<SnapshotStoreAttribute>(Type).Database, logger) { }
    }
}
