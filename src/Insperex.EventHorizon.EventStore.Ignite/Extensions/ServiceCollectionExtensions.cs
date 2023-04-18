using Apache.Ignite.Core;
using Apache.Ignite.Core.Client;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Ignite.Models;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Locks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Insperex.EventHorizon.EventStore.Ignite.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddIgniteSnapshotStore(this IServiceCollection collection,
        IConfiguration configuration)
    {
        collection.AddIgniteStore(configuration);
        collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(IgniteEventStoreFactory<>));
        collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(IgniteEventStoreFactory<>));

        return collection;
    }

    public static IServiceCollection AddIgniteViewStore(this IServiceCollection collection,
        IConfiguration configuration)
    {
        collection.AddIgniteStore(configuration);
        collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(IgniteEventStoreFactory<>));
        return collection;
    }

    private static IServiceCollection AddIgniteStore(this IServiceCollection collection, IConfiguration configuration)
    {
        var config = configuration.GetSection("Ignite").Get<IgniteConfig>();

        var cfg = new IgniteClientConfiguration
        {
            Endpoints = config.Endpoints
        };

        collection.TryAddSingleton(x => Ignition.StartClient(cfg));
        collection.AddSingleton(typeof(LockFactory<>));
        collection.AddSingleton<AttributeUtil>();
        return collection;
    }
}