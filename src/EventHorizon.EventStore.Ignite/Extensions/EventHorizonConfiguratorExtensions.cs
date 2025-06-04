using System;
using Apache.Ignite.Core;
using Apache.Ignite.Core.Client;
using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Ignite.Models;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Locks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventHorizon.EventStore.Ignite.Extensions;

public static class EventHorizonConfiguratorExtensions
{
    public static EventHorizonConfigurator AddIgniteSnapshotStore(this EventHorizonConfigurator configurator, Action<IgniteConfig> onConfig)
    {
        AddIgniteStore(configurator, onConfig);
        configurator.Collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(IgniteEventStoreFactory<>));
        configurator.Collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(IgniteEventStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddIgniteViewStore(this EventHorizonConfigurator configurator, Action<IgniteConfig> onConfig)
    {
        AddIgniteStore(configurator, onConfig);
        configurator.Collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(IgniteEventStoreFactory<>));
        return configurator;
    }

    private static void AddIgniteStore(this EventHorizonConfigurator configurator, Action<IgniteConfig> onConfig)
    {
        configurator.Collection.Configure(onConfig);
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton<AttributeUtil>();
    }
}
