using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Locks;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventStore.InMemory.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInMemorySnapshotStore(this IServiceCollection collection)
    {
        collection.AddInMemoryStore();
        collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(InMemoryEventStoreFactory<>));
        collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(InMemoryEventStoreFactory<>));
        return collection;
    }

    public static IServiceCollection AddInMemoryViewStore(this IServiceCollection collection)
    {
        collection.AddInMemoryStore();
        collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(InMemoryEventStoreFactory<>));
        return collection;
    }

    private static IServiceCollection AddInMemoryStore(this IServiceCollection collection)
    {
        collection.AddSingleton(typeof(LockFactory<>));
        collection.AddSingleton<AttributeUtil>();
        return collection;
    }
}