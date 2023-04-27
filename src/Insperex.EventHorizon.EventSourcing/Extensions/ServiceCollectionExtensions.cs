using System;
using Insperex.EventHorizon.Abstractions;
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
    public static EventHorizonConfigurator AddEventSourcing(this EventHorizonConfigurator configurator)
    {
        configurator.Collection.AddSingleton(x => x.GetRequiredService<IStreamFactory>().GetTopicResolver());
        configurator.Collection.AddSingleton(typeof(EventSourcingClient<>));
        configurator.Collection.AddSingleton(typeof(AggregateBuilder<,>));
        configurator.Collection.AddSingleton<SenderBuilder>();
        configurator.Collection.AddSingleton<SenderSubscriptionTracker>();
        configurator.Collection.AddSingleton<ValidationUtil>();

        return configurator;
    }

    public static EventHorizonConfigurator AddHostedSnapshot<T>(this EventHorizonConfigurator configurator,
        Action<AggregateBuilder<Snapshot<T>, T>> onBuild = null)
        where T : class, IState
    {
        // Handle Commands
        configurator.Collection.AddHostedService(x =>
        {
            var serviceProvider = x.GetRequiredService<IServiceProvider>();
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var loggerFactory = x.GetRequiredService<ILoggerFactory>();
            var builder = new AggregateBuilder<Snapshot<T>, T>(serviceProvider, streamingClient, loggerFactory);
            onBuild?.Invoke(builder);
            return new AggregateStateHostedService<Snapshot<T>, Command, T>(streamingClient, builder.Build());
        });

        // Handle Requests
        configurator.Collection.AddHostedService(x =>
        {
            var serviceProvider = x.GetRequiredService<IServiceProvider>();
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var loggerFactory = x.GetRequiredService<ILoggerFactory>();
            var builder = new AggregateBuilder<Snapshot<T>, T>(serviceProvider, streamingClient, loggerFactory);
            onBuild?.Invoke(builder);
            return new AggregateStateHostedService<Snapshot<T>, Request, T>(streamingClient, builder.Build());
        });

        return configurator;
    }

    public static EventHorizonConfigurator AddHostedViewIndexer<T>(this EventHorizonConfigurator configurator,
        Action<AggregateBuilder<View<T>, T>> onBuild = null)
        where T : class, IState
    {
        // Handle Events
        configurator.Collection.AddHostedService(x =>
        {
            var serviceProvider = x.GetRequiredService<IServiceProvider>();
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var loggerFactory = x.GetRequiredService<ILoggerFactory>();
            var builder = new AggregateBuilder<View<T>, T>(serviceProvider, streamingClient, loggerFactory);
            onBuild?.Invoke(builder);
            var aggregator = builder.Build();
            return new AggregateStateHostedService<View<T>, Event, T>(streamingClient, aggregator);
        });

        return configurator;
    }

    public static EventHorizonConfigurator AddHostedMigration<TSource, TTarget>(this EventHorizonConfigurator configurator,
        Action<AggregateBuilder<Snapshot<TTarget>, TTarget>> onBuild = null)
        where TSource : class, IState, new()
        where TTarget : class, IState, new()
    {
        configurator.Collection.AddHostedService(x =>
        {
            var serviceProvider = x.GetRequiredService<IServiceProvider>();
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var loggerFactory = x.GetRequiredService<ILoggerFactory>();
            var builder = new AggregateBuilder<Snapshot<TTarget>, TTarget>(serviceProvider, streamingClient, loggerFactory);
            onBuild?.Invoke(builder);
            var aggregator = builder.Build();
            return new AggregateMigrationHostedService<TSource,TTarget>(aggregator, streamingClient);
        });

        return configurator;
    }
}
