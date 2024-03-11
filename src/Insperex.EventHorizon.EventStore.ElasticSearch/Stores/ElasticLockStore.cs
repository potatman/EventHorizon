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
