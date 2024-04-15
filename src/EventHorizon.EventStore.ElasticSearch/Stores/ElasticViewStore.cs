using System;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.ElasticSearch.Attributes;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStore.ElasticSearch.Stores
{
    public class ElasticViewStore<TState> : AbstractElasticCrudStore<View<TState>>, IViewStore<TState>
        where TState : class, IState
    {
        private static readonly Type Type = typeof(TState);
        public ElasticViewStore(Formatter formatter, AttributeUtil attributeUtil, ElasticClientResolver clientResolver, ILogger<ElasticViewStore<TState>> logger)
            : base(attributeUtil.GetOne<ElasticIndexAttribute>(Type),
                clientResolver.GetClient(),
                formatter.GetDatabase<Snapshot<TState>>(Type),
                logger) { }
    }
}
