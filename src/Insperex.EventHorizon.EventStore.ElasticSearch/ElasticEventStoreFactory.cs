using System;
using System.Linq;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Elastic.Transport;
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

namespace Insperex.EventHorizon.EventStore.ElasticSearch;

public class ElasticStoreFactory<T> : ISnapshotStoreFactory<T>, IViewStoreFactory<T>, ILockStoreFactory<T>
    where T : class, IState
{
    private readonly ElasticsearchClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILoggerFactory _loggerFactory;
    private readonly Type _type;
    private readonly ElasticIndexAttribute _elasticAttr;

    public ElasticStoreFactory(IOptions<ElasticConfig> options, AttributeUtil attributeUtil, ILoggerFactory loggerFactory)
    {
        _type = typeof(T);
        _attributeUtil = attributeUtil;
        _loggerFactory = loggerFactory;
        _elasticAttr = _attributeUtil.GetOne<ElasticIndexAttribute>(_type);

        // Client Configuration
        var connectionPool = new StickyNodePool(options.Value.Uris.Select(u => new Uri(u)));
        var settings = new ElasticsearchClientSettings(connectionPool)
            .PingTimeout(TimeSpan.FromSeconds(10))
            .DeadTimeout(TimeSpan.FromSeconds(60))
            .RequestTimeout(TimeSpan.FromSeconds(60))
            ;

        if (options.Value.UserName != null && options.Value.Password != null)
            settings = settings.Authentication(new BasicAuthentication(options.Value.UserName, options.Value.Password));

        _client = new ElasticsearchClient(settings);
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
            _attributeUtil.GetOne<ViewStoreAttribute>(_type).Database,
            _loggerFactory.CreateLogger<ElasticCrudStore<View<T>>>());
    }
}
