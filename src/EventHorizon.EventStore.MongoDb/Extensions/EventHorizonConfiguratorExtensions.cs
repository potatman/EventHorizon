using System;
using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Locks;
using EventHorizon.EventStore.MongoDb.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace EventHorizon.EventStore.MongoDb.Extensions;

public static class EventHorizonConfiguratorExtensions
{
    static EventHorizonConfiguratorExtensions()
    {
        // Allow all to serialize
        BsonSerializer.RegisterSerializer(new ObjectSerializer(_ => true));
    }

    public static EventHorizonConfigurator AddMongoDbSnapshotStore(this EventHorizonConfigurator configurator, Action<MongoConfig> onConfig)
    {
        AddMongoDbStore(configurator, onConfig);
        configurator.Collection.AddSingleton(typeof(ISnapshotStoreFactory<>), typeof(MongoStoreFactory<>));
        configurator.Collection.AddSingleton(typeof(ILockStoreFactory<>), typeof(MongoStoreFactory<>));
        return configurator;
    }

    public static EventHorizonConfigurator AddMongoDbViewStore(this EventHorizonConfigurator configurator, Action<MongoConfig> onConfig)
    {
        AddMongoDbStore(configurator, onConfig);
        configurator.Collection.AddSingleton(typeof(IViewStoreFactory<>), typeof(MongoStoreFactory<>));
        return configurator;
    }

    private static void AddMongoDbStore(this EventHorizonConfigurator configurator, Action<MongoConfig> onConfig)
    {
        configurator.Collection.Configure(onConfig);
        configurator.Collection.AddSingleton(typeof(LockFactory<>));
        configurator.Collection.AddSingleton<AttributeUtil>();
    }
}
