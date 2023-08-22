using System;
using Apache.Ignite.Core;
using Apache.Ignite.Core.Client;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Ignite.Models;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Insperex.EventHorizon.EventStore.Ignite;

public class IgniteEventStoreFactory<T> : ISnapshotStoreFactory<T>, IViewStoreFactory<T>, ILockStoreFactory<T>
    where T : class, IState
{
    private readonly IIgniteClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Type _type;

    public IgniteEventStoreFactory(IOptions<IgniteConfig> options, AttributeUtil attributeUtil, ILoggerFactory loggerFactory)
    {
        _type = typeof(T);
        _client = Ignition.StartClient(new IgniteClientConfiguration
        {
            Endpoints = options.Value.Endpoints
        });
        _attributeUtil = attributeUtil;
        _loggerFactory = loggerFactory;
    }

    public ICrudStore<Lock> GetLockStore()
    {
        return new IgniteCrudStore<Lock>(_client, _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId);
    }

    public ICrudStore<Snapshot<T>> GetSnapshotStore()
    {
        return new IgniteCrudStore<Snapshot<T>>(_client, _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId);
    }

    public ICrudStore<View<T>> GetViewStore()
    {
        return new IgniteCrudStore<View<T>>(_client, _attributeUtil.GetOne<ViewStoreAttribute>(_type).Database);
    }
}
