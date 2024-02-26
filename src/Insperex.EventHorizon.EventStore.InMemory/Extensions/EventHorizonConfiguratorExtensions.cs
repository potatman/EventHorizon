using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.EventStore.InMemory.Databases;
using Insperex.EventHorizon.EventStore.InMemory.Stores;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
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
            typeof(ISnapshotStore<>),
            typeof(InMemorySnapshotStore<>),
            ServiceLifetime.Singleton));
        configurator.Collection.Replace(ServiceDescriptor.Describe(
            typeof(ILockStore),
            typeof(InMemoryLockStore<>),
            ServiceLifetime.Singleton));
        return configurator;
    }

    public static EventHorizonConfigurator AddInMemoryViewStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStore(configurator);
        configurator.Collection.Replace(ServiceDescriptor.Describe(
            typeof(IViewStore<>),
            typeof(InMemoryViewStore<>),
            ServiceLifetime.Singleton));
        return configurator;
    }

    private static void AddInMemoryStore(EventHorizonConfigurator configurator)
    {
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton(typeof(CrudDatabase));
    }
}
