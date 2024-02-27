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
    public class ElasticViewStore<T> : AbstractElasticCrudStore<View<T>>, IViewStore<T>
        where T : IState
    {
        private static readonly Type Type = typeof(T);

        public ElasticViewStore(AttributeUtil attributeUtil, ElasticClientResolver clientResolver, ILogger<ElasticViewStore<T>> logger)
            : base(attributeUtil.GetOne<ElasticIndexAttribute>(Type), clientResolver.GetClient(), attributeUtil.GetOne<ViewStoreAttribute>(Type).Database, logger) { }
    }
}