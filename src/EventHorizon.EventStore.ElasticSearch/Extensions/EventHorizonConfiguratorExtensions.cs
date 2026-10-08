using System;
using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.ElasticSearch.Models;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Locks;
using Microsoft.Extensions.DependencyInjection;

namespace EventHorizon.EventStore.ElasticSearch.Extensions;

public static class EventHorizonConfiguratorExtensions
{
    public static EventHorizonConfigurator AddElasticSnapshotStore(this EventHorizonConfigurator configurator, Action<ElasticConfig> onConfig)
    {
        AddElasticStore(configurator, onConfig);
        configurator.Collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(ElasticStoreFactory<>));
        configurator.Collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(ElasticStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddElasticViewStore(this EventHorizonConfigurator configurator, Action<ElasticConfig> onConfig)
    {
        AddElasticStore(configurator, onConfig);
        configurator.Collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(ElasticStoreFactory<>));
        return configurator;
    }

    private static void AddElasticStore(this EventHorizonConfigurator configurator, Action<ElasticConfig> onConfig)
    {
        configurator.Collection.Configure(onConfig);
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton<AttributeUtil>();
    }
}
