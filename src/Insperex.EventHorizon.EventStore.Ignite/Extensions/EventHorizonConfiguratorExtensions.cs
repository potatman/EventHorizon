using Apache.Ignite.Core;
using Apache.Ignite.Core.Client;
using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Ignite.Models;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Locks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Insperex.EventHorizon.EventStore.Ignite.Extensions;

public static class EventHorizonConfiguratorExtensions
{
    public static EventHorizonConfigurator AddIgniteSnapshotStore(this EventHorizonConfigurator configurator)
    {
        AddIgniteStore(configurator);
        configurator.Collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(IgniteEventStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddIgniteLockStore(this EventHorizonConfigurator configurator)
    {
        AddIgniteStore(configurator);
        configurator.Collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(IgniteEventStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddIgniteViewStore(this EventHorizonConfigurator configurator)
    {
        AddIgniteStore(configurator);
        configurator.Collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(IgniteEventStoreFactory<>));
        return configurator;
    }

    private static void AddIgniteStore(this EventHorizonConfigurator configurator)
    {
        var config = configurator.Config.GetSection("Ignite").Get<IgniteConfig>();

        var cfg = new IgniteClientConfiguration
        {
            Endpoints = config.Endpoints
        };

        configurator.Collection.TryAddSingleton(x => Ignition.StartClient(cfg));
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton<AttributeUtil>();
    }
}
