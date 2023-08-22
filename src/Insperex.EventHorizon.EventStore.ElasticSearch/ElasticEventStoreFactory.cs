using System;
using System.Linq;
using Elasticsearch.Net;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.ElasticSearch.Attributes;
using Insperex.EventHorizon.EventStore.ElasticSearch.Models;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nest;

namespace Insperex.EventHorizon.EventStore.ElasticSearch;

public class ElasticStoreFactory<T> : ISnapshotStoreFactory<T>, IViewStoreFactory<T>, ILockStoreFactory<T>
    where T : class, IState
{
    private readonly IElasticClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Type _type;
    private readonly ElasticIndexAttribute _elasticAttr;

    public ElasticStoreFactory(IOptions<ElasticConfig> options, AttributeUtil attributeUtil, ILoggerFactory loggerFactory)
    {
        _type = typeof(T);
        _attributeUtil = attributeUtil;
        _loggerFactory = loggerFactory;
        _elasticAttr = _attributeUtil.GetOneRequired<ElasticIndexAttribute>(_type);

        // Client Configuration
        var connectionPool = new StickyConnectionPool(options.Value.Uris.Select(u => new Uri(u)));
        var settings = new ConnectionSettings(connectionPool)
            .PingTimeout(TimeSpan.FromSeconds(10))
            .DeadTimeout(TimeSpan.FromSeconds(60))
            .RequestTimeout(TimeSpan.FromSeconds(60))
            .DisableDirectStreaming();

        if (options.Value.UserName != null && options.Value.Password != null)
            settings = settings.BasicAuthentication(options.Value.UserName, options.Value.Password);

        _client = new ElasticClient(settings);


        var indexSettings = new IndexSettings
        {
            NumberOfShards = 1,
            NumberOfReplicas = 1,
            ["key"] = "value"
        };
    }

    public ICrudStore<Lock> GetLockStore()
    {
        var store = new ElasticCrudStore<Lock>(_elasticAttr, _client,
            _attributeUtil.GetOneRequired<SnapshotStoreAttribute>(_type).BucketId,
            _loggerFactory.CreateLogger<ElasticCrudStore<Lock>>());
        return store;
    }

    public ICrudStore<Snapshot<T>> GetSnapshotStore()
    {
        var store = new ElasticCrudStore<Snapshot<T>>(_elasticAttr, _client,
            _attributeUtil.GetOneRequired<SnapshotStoreAttribute>(_type).BucketId,
            _loggerFactory.CreateLogger<ElasticCrudStore<Snapshot<T>>>());

        return store;
    }

    public ICrudStore<View<T>> GetViewStore()
    {
        return new ElasticCrudStore<View<T>>(_elasticAttr, _client,
            _attributeUtil.GetOneRequired<ViewStoreAttribute>(_type).Database,
            _loggerFactory.CreateLogger<ElasticCrudStore<View<T>>>());
    }
}
