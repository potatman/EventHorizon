using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Locks;
using Insperex.EventHorizon.EventStore.MongoDb.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Insperex.EventHorizon.EventStore.MongoDb.Extensions;

public static class ServiceCollectionExtensions
{
    static ServiceCollectionExtensions()
    {
        // Allow all to serialize
        BsonSerializer.RegisterSerializer(new ObjectSerializer(_ => true));
    }

    public static IServiceCollection AddMongoDbSnapshotStore(this IServiceCollection collection,
        IConfiguration configuration)
    {
        collection.AddMongoDbStore(configuration);
        collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(MongoStoreFactory<>));
        collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(MongoStoreFactory<>));
        return collection;
    }

    public static IServiceCollection AddMongoDbViewStore(this IServiceCollection collection,
        IConfiguration configuration)
    {
        collection.AddMongoDbStore(configuration);
        collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(MongoStoreFactory<>));
        return collection;
    }

    private static IServiceCollection AddMongoDbStore(this IServiceCollection collection, IConfiguration configuration)
    {
        var config = configuration.GetSection("MongoDb").Get<MongoConfig>();
        collection.TryAddSingleton<IMongoClient>(x => new MongoClient(MongoUrl.Create(config.ConnectionString)));
        collection.AddSingleton(typeof(LockFactory<>));
        collection.AddSingleton<AttributeUtil>();
        return collection;
    }
}