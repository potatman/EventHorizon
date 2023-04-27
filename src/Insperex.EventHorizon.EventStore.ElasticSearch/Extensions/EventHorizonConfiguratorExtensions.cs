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
    public static EventHorizonConfigurator AddElasticSnapshotStore(this EventHorizonConfigurator configurator)
    {
        AddElasticStore(configurator);
        configurator.Collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(ElasticStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddElasticLockStore(this EventHorizonConfigurator configurator)
    {
        AddElasticStore(configurator);
        configurator.Collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(ElasticStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddElasticViewStore(this EventHorizonConfigurator configurator)
    {
        AddElasticStore(configurator);
        configurator.Collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(ElasticStoreFactory<>));
        return configurator;
    }

    private static void AddElasticStore(this EventHorizonConfigurator configurator)
    {
        var config = configurator.Config.GetSection("ElasticSearch").Get<ElasticConfig>();

        var connectionPool = new StickyConnectionPool(config.Uris.Select(u => new Uri(u)));
        var settings = new ConnectionSettings(connectionPool)
            .PingTimeout(TimeSpan.FromSeconds(10))
            .DeadTimeout(TimeSpan.FromSeconds(60))
            .RequestTimeout(TimeSpan.FromSeconds(60))
            .DisableDirectStreaming();

        if (config.UserName != null)
            settings = settings.BasicAuthentication(config.UserName, config.Password);

        var client = new ElasticClient(settings);
        configurator.Collection.TryAddSingleton<IElasticClient>(x => client);
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton<AttributeUtil>();
    }
}
