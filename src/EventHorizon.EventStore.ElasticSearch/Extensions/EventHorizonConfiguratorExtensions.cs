﻿using System;
using Elastic.Clients.Elasticsearch;
using EventHorizon.Abstractions;
using EventHorizon.EventStore.ElasticSearch.Models;
using EventHorizon.EventStore.ElasticSearch.Stores;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Locks;
using Microsoft.Extensions.DependencyInjection;

namespace EventHorizon.EventStore.ElasticSearch.Extensions;

public static class EventHorizonConfiguratorExtensions
{

    public static EventHorizonConfigurator AddElasticClient(this EventHorizonConfigurator configurator, Action<ElasticConfig> onConfig)
    {
        configurator.Collection.Configure(onConfig);
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.AddClientResolver<ElasticClientResolver, ElasticsearchClient>();
        return configurator;
    }

    public static EventHorizonConfigurator AddElasticSnapshotStore(this EventHorizonConfigurator configurator, Action<ElasticConfig> onConfig)
    {
        AddElasticClient(configurator, onConfig);
        configurator.Collection.AddSingleton(typeof(ISnapshotStore<>), typeof(ElasticSnapshotStore<>));
        configurator.Collection.AddSingleton(typeof(ILockStore<>), typeof(ElasticLockStore<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddElasticViewStore(this EventHorizonConfigurator configurator, Action<ElasticConfig> onConfig)
    {
        AddElasticClient(configurator, onConfig);
        configurator.Collection.AddSingleton(typeof(IViewStore<>), typeof(ElasticViewStore<>));
        return configurator;
    }
}