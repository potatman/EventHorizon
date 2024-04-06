using System;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStore.ElasticSearch.Stores
{
    public class ElasticSnapshotStore<TState> : AbstractElasticCrudStore<Snapshot<TState>>, ISnapshotStore<TState> where TState : class, IState
    {
        private static readonly Type Type = typeof(TState);
        public ElasticSnapshotStore(Formatter formatter, AttributeUtil attributeUtil, ElasticClientResolver clientResolver, ILogger<ElasticSnapshotStore<TState>> logger)
            : base(attributeUtil.GetOne<ElasticIndexAttribute>(Type),
                clientResolver.GetClient(),
                formatter.GetDatabase<Snapshot<TState>>(Type),
                logger) { }
    }
}
