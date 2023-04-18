using System;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Nest;

namespace Insperex.EventHorizon.EventStore.ElasticSearch;

public class ElasticStoreFactory<T> : ISnapshotStoreFactory<T>, IViewStoreFactory<T>, ILockStoreFactory<T>
    where T : class, IState
{
    private readonly IElasticClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly Type _type;

    public ElasticStoreFactory(IElasticClient client, AttributeUtil attributeUtil)
    {
        _type = typeof(T);
        _client = client;
        _attributeUtil = attributeUtil;
    }

    public ICrudStore<Lock> GetLockStore()
    {
        return new ElasticCrudStore<Lock>(_client, _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId);
    }

    public ICrudStore<Snapshot<T>> GetSnapshotStore()
    {
        return new ElasticCrudStore<Snapshot<T>>(_client, _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId);
    }

    public ICrudStore<View<T>> GetViewStore()
    {
        return new ElasticCrudStore<View<T>>(_client, _attributeUtil.GetOne<ViewStoreAttribute>(_type).BucketId);
    }
}