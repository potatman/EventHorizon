using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Insperex.EventHorizon.EventStore.InMemory.Extensions;

public static class EventHorizonConfiguratorExtensions
{
    public static EventHorizonConfigurator AddInMemorySnapshotStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStore(configurator);
        configurator.Collection.Replace(ServiceDescriptor.Describe(
            typeof(ISnapshotStoreFactory<>),
            typeof(InMemoryEventStoreFactory<>),
            ServiceLifetime.Singleton));
        return configurator;
    }

    public static EventHorizonConfigurator AddInMemoryLockStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStore(configurator);
        configurator.Collection.Replace(ServiceDescriptor.Describe(
            typeof(ILockStoreFactory<>),
            typeof(InMemoryEventStoreFactory<>),
            ServiceLifetime.Singleton));
        return configurator;
    }

    public static EventHorizonConfigurator AddInMemoryViewStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStore(configurator);
        configurator.Collection.Replace(ServiceDescriptor.Describe(
            typeof(IViewStoreFactory<>),
            typeof(InMemoryEventStoreFactory<>),
            ServiceLifetime.Singleton));
        return configurator;
    }

    private static void AddInMemoryStore(EventHorizonConfigurator configurator)
    {
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton<AttributeUtil>();
    }
}
