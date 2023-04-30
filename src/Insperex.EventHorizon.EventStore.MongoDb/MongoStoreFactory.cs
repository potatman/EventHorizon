using System;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using MongoDB.Driver;

namespace Insperex.EventHorizon.EventStore.MongoDb;

public class MongoStoreFactory<T> : ISnapshotStoreFactory<T>, IViewStoreFactory<T>, ILockStoreFactory<T>
    where T : class, IState
{
    private readonly IMongoClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly Type _type;

    public MongoStoreFactory(IMongoClient client, AttributeUtil attributeUtil)
    {
        _type = typeof(T);
        _client = client;
        _attributeUtil = attributeUtil;
    }

    public ICrudStore<Lock> GetLockStore()
    {
        return new MongoCrudStore<Lock>(_client, _attributeUtil, _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId);
    }

    public ICrudStore<Snapshot<T>> GetSnapshotStore()
    {
        return new MongoCrudStore<Snapshot<T>>(_client, _attributeUtil, _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId);
    }

    public ICrudStore<View<T>> GetViewStore()
    {
        return new MongoCrudStore<View<T>>(_client, _attributeUtil, _attributeUtil.GetOne<ViewStoreAttribute>(_type).Database);
    }
}
