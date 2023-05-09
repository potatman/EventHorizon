using System;
using System.Linq;
using Elasticsearch.Net;
using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.ElasticSearch.Models;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Locks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Nest;

namespace Insperex.EventHorizon.EventStore.ElasticSearch.Extensions;

public static class EventHorizonConfiguratorExtensions
{
    public static EventHorizonConfigurator AddElasticSnapshotStore(this EventHorizonConfigurator configurator, IConfiguration config)
    {
        AddElasticStore(configurator, config);
        configurator.Collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(ElasticStoreFactory<>));
        configurator.Collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(ElasticStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddElasticViewStore(this EventHorizonConfigurator configurator, IConfiguration config)
    {
        AddElasticStore(configurator, config);
        configurator.Collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(ElasticStoreFactory<>));
        return configurator;
    }

    private static void AddElasticStore(this EventHorizonConfigurator configurator, IConfiguration config)
    {
        configurator.Collection.Configure<ElasticConfig>(config.GetSection("ElasticSearch"));
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton<AttributeUtil>();

        configurator.Collection.AddHealthChecks()
            .AddElasticsearch(config.GetSection("ElasticSearch").Get<ElasticConfig>().Uris.First());
    }
}
