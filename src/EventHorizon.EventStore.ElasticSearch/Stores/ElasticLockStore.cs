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
    public class ElasticLockStore<TState> : AbstractElasticCrudStore<Lock>, ILockStore<TState>
        where TState : IState
    {
        private static readonly Type Type = typeof(TState);

        public ElasticLockStore(Formatter formatter, AttributeUtil attributeUtil, ElasticClientResolver clientResolver, ILogger<ElasticLockStore<TState>> logger)
            : base(attributeUtil.GetOne<ElasticIndexAttribute>(Type),
                clientResolver.GetClient(),
                formatter.GetDatabase<Lock>(Type),
                logger) { }
    }
}
