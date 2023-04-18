using System;
using System.Linq;
using Elasticsearch.Net;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.ElasticSearch.Models;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Locks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nest;

namespace Insperex.EventHorizon.EventStore.ElasticSearch.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddElasticSnapshotStore(this IServiceCollection collection,
        IConfiguration configuration)
    {
        collection.AddElasticStore(configuration);
        collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(ElasticStoreFactory<>));
        collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(ElasticStoreFactory<>));
        return collection;
    }

    public static IServiceCollection AddElasticViewStore(this IServiceCollection collection,
        IConfiguration configuration)
    {
        collection.AddElasticStore(configuration);
        collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(ElasticStoreFactory<>));
        return collection;
    }

    private static IServiceCollection AddElasticStore(this IServiceCollection collection, IConfiguration configuration)
    {
        var config = configuration.GetSection("ElasticSearch").Get<ElasticConfig>();

        var connectionPool = new StickyConnectionPool(config.Uris.Select(u => new Uri(u)));
        var settings = new ConnectionSettings(connectionPool)
            .PingTimeout(TimeSpan.FromSeconds(10))
            .DeadTimeout(TimeSpan.FromSeconds(60))
            .RequestTimeout(TimeSpan.FromSeconds(60))
            .DisableDirectStreaming();

        if (config.UserName != null)
            settings = settings.BasicAuthentication(config.UserName, config.Password);

        var client = new ElasticClient(settings);
        collection.TryAddSingleton<IElasticClient>(x => client);
        collection.AddSingleton(typeof(LockFactory<>));
        collection.AddSingleton<AttributeUtil>();

        return collection;
    }
}