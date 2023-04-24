using System;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;
using Nest;

namespace Insperex.EventHorizon.EventStore.ElasticSearch;

public class ElasticStoreFactory<T> : ISnapshotStoreFactory<T>, IViewStoreFactory<T>, ILockStoreFactory<T>
    where T : class, IState
{
    private readonly IElasticClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Type _type;
    private readonly ElasticConfigAttribute _elasticAttr;

    public ElasticStoreFactory(IElasticClient client, AttributeUtil attributeUtil, ILoggerFactory loggerFactory)
    {
        _type = typeof(T);
        _client = client;
        _attributeUtil = attributeUtil;
        _loggerFactory = loggerFactory;
        _elasticAttr = _attributeUtil.GetOne<ElasticConfigAttribute>(_type);
    }

    public ICrudStore<Lock> GetLockStore()
    {
        var store = new ElasticCrudStore<Lock>(_elasticAttr, _client,
            _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId,
            _loggerFactory.CreateLogger<ElasticCrudStore<Lock>>());
        return store;
    }

    public ICrudStore<Snapshot<T>> GetSnapshotStore()
    {
        var store = new ElasticCrudStore<Snapshot<T>>(_elasticAttr, _client,
            _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId,
            _loggerFactory.CreateLogger<ElasticCrudStore<Snapshot<T>>>());

        return store;
    }

    public ICrudStore<View<T>> GetViewStore()
    {
        return new ElasticCrudStore<View<T>>(_elasticAttr, _client,
            _attributeUtil.GetOne<ViewStoreAttribute>(_type).BucketId,
            _loggerFactory.CreateLogger<ElasticCrudStore<View<T>>>());
    }
}
