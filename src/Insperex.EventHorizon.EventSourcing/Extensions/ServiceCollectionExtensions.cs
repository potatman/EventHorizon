using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventSourcing.Util;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventSourcing.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEventSourcing(this IServiceCollection collection)
    {
        collection.AddSingleton(x => x.GetRequiredService<IStreamFactory>().GetTopicResolver());
        collection.AddSingleton(typeof(EventSourcingClient<>));
        collection.AddSingleton(typeof(AggregateBuilder<,>));
        collection.AddSingleton<SenderBuilder>();
        collection.AddSingleton<SenderSubscriptionTracker>();
        collection.AddSingleton<ValidationUtil>();

        return collection;
    }

    public static IServiceCollection AddHostedAggregate<T>(this IServiceCollection collection,
        Action<AggregateBuilder<Snapshot<T>, T>> onBuild = null)
        where T : class, IState
    {
        collection.AddSingleton(x =>
        {
            var serviceProvider = x.GetRequiredService<IServiceProvider>();
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var loggerFactory = x.GetRequiredService<ILoggerFactory>();
            var builder = new AggregateBuilder<Snapshot<T>, T>(serviceProvider, streamingClient, loggerFactory);
            onBuild?.Invoke(builder);
            return builder.Build();
        });

        collection.AddScoped<T>();
        collection.AddHostedService<AggregateHostedService<Snapshot<T>, Command, T>>();
        collection.AddHostedService<AggregateHostedService<Snapshot<T>, Request, T>>();

        return collection;
    }

    public static IServiceCollection AddHostedViewIndexer<T>(this IServiceCollection collection,
        Action<AggregateBuilder<View<T>, T>> onBuild = null)
        where T : class, IState
    {
        collection.AddSingleton(x =>
        {
            var serviceProvider = x.GetRequiredService<IServiceProvider>();
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var loggerFactory = x.GetRequiredService<ILoggerFactory>();
            var builder = new AggregateBuilder<View<T>, T>(serviceProvider, streamingClient, loggerFactory);
            onBuild?.Invoke(builder);
            return builder.Build();
        });

        collection.AddScoped<T>();
        collection.AddHostedService<AggregateHostedService<View<T>, Event, T>>();

        return collection;
    }
}
