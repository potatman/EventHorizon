using System;
using System.Threading.Tasks;
using Apache.Ignite;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Ignite.Models;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Lock = EventHorizon.EventStore.Models.Lock;

namespace EventHorizon.EventStore.Ignite;

public class IgniteEventStoreFactory<T> : ISnapshotStoreFactory<T>, IViewStoreFactory<T>, ILockStoreFactory<T>
    where T : class, IState
{
    private readonly IgniteClientConfiguration _clientConfig;
    private readonly object _clientGate = new();
    private Task<IIgniteClient> _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Type _type;

    public IgniteEventStoreFactory(IOptions<IgniteConfig> options, AttributeUtil attributeUtil, ILoggerFactory loggerFactory)
    {
        _type = typeof(T);
        _clientConfig = new IgniteClientConfiguration(options.Value.Endpoints);
        _attributeUtil = attributeUtil;
        _loggerFactory = loggerFactory;
    }

    public ICrudStore<Lock> GetLockStore()
    {
        return new IgniteCrudStore<Lock>(GetClientAsync, _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId);
    }

    public ICrudStore<Snapshot<T>> GetSnapshotStore()
    {
        return new IgniteCrudStore<Snapshot<T>>(GetClientAsync, _attributeUtil.GetOne<SnapshotStoreAttribute>(_type).BucketId);
    }

    public ICrudStore<View<T>> GetViewStore()
    {
        return new IgniteCrudStore<View<T>>(GetClientAsync, _attributeUtil.GetOne<ViewStoreAttribute>(_type).Database);
    }

    // Connects on first use rather than blocking the constructor; a failed start is not cached, so the next call retries.
    private Task<IIgniteClient> GetClientAsync()
    {
        lock (_clientGate)
        {
            if (_client is null || _client.IsFaulted || _client.IsCanceled)
                _client = IgniteClient.StartAsync(_clientConfig);

            return _client;
        }
    }
}
