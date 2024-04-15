using EventHorizon.Abstractions;
using EventHorizon.EventStore.InMemory.Stores;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Locks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventHorizon.EventStore.InMemory.Extensions;

public static class EventHorizonConfiguratorExtensions
{

    public static EventHorizonConfigurator AddInMemoryStoreClient(this EventHorizonConfigurator configurator)
    {
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton(typeof(InMemoryStoreClient));
        return configurator;
    }

    public static EventHorizonConfigurator AddInMemorySnapshotStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStoreClient(configurator);
        configurator.Collection.Replace(ServiceDescriptor.Describe(
            typeof(ISnapshotStore<>),
            typeof(InMemorySnapshotStore<>),
            ServiceLifetime.Singleton));
        configurator.Collection.Replace(ServiceDescriptor.Describe(
            typeof(ILockStore<>),
            typeof(InMemoryLockStore<>),
            ServiceLifetime.Singleton));
        return configurator;
    }

    public static EventHorizonConfigurator AddInMemoryViewStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStoreClient(configurator);
        configurator.Collection.Replace(ServiceDescriptor.Describe(
            typeof(IViewStore<>),
            typeof(InMemoryViewStore<>),
            ServiceLifetime.Singleton));
        return configurator;
    }
}
