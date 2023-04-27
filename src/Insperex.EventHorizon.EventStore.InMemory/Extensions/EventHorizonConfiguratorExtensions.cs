using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Locks;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventStore.InMemory.Extensions;

public static class EventHorizonConfiguratorExtensions
{
    public static EventHorizonConfigurator AddInMemorySnapshotStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStore(configurator);
        configurator.Collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(InMemoryEventStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddInMemoryLockStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStore(configurator);
        configurator.Collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(InMemoryEventStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddInMemoryViewStore(this EventHorizonConfigurator configurator)
    {
        AddInMemoryStore(configurator);
        configurator.Collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(InMemoryEventStoreFactory<>));
        return configurator;
    }

    private static void AddInMemoryStore(EventHorizonConfigurator configurator)
    {
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton<AttributeUtil>();
    }
}
