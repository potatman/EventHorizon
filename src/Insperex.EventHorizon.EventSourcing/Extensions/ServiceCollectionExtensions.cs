using System;
using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventSourcing.Util;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Insperex.EventHorizon.EventSourcing.Extensions;

public static class ServiceCollectionExtensions
{
    public static EventHorizonConfigurator AddEventSourcing(this EventHorizonConfigurator configurator)
    {
        configurator.Collection.TryAddSingleton(typeof(EventSourcingClient<>));
        configurator.Collection.TryAddSingleton(typeof(AggregateBuilder<,>));
        configurator.Collection.TryAddSingleton(typeof(SenderBuilder<>));
        configurator.Collection.TryAddSingleton<SenderSubscriptionTracker>();
        configurator.Collection.TryAddSingleton<ValidationUtil>();

        return configurator;
    }

    public static EventHorizonConfigurator ApplyRequestsToSnapshot<T>(this EventHorizonConfigurator configurator,
        Action<AggregateBuilder<Snapshot<T>, T>> onBuild = null,
        Func<SubscriptionBuilder<Request>, SubscriptionBuilder<Request>> onBuildSubscription = null)
        where T : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddHostedService(x =>
        {
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var builder = x.GetRequiredService<AggregateBuilder<Snapshot<T>, T>>();
            onBuild?.Invoke(builder);
            return new AggregateConsumerHostedService<Snapshot<T>, Request, T>(streamingClient,
                builder.Build(), onBuildSubscription);
        });

        return configurator;
    }

    public static EventHorizonConfigurator ApplyCommandsToSnapshot<T>(this EventHorizonConfigurator configurator,
        Action<AggregateBuilder<Snapshot<T>, T>> onBuild = null,
        Func<SubscriptionBuilder<Command>, SubscriptionBuilder<Command>> onBuildSubscription = null)
        where T : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddHostedService(x =>
        {
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var builder = x.GetRequiredService<AggregateBuilder<Snapshot<T>, T>>();
            onBuild?.Invoke(builder);
            return new AggregateConsumerHostedService<Snapshot<T>, Command, T>(streamingClient,
                builder.Build(), onBuildSubscription);
        });

        return configurator;
    }

    public static EventHorizonConfigurator ApplyEventsToView<T>(this EventHorizonConfigurator configurator,
        Action<AggregateBuilder<View<T>, T>> onBuild = null,
        Func<SubscriptionBuilder<Event>, SubscriptionBuilder<Event>> onBuildSubscription = null)
        where T : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddHostedService(x =>
        {
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var builder = x.GetRequiredService<AggregateBuilder<View<T>, T>>();
            onBuild?.Invoke(builder);
            var aggregator = builder.Build();
            return new AggregateConsumerHostedService<View<T>, Event, T>(streamingClient, aggregator,
                onBuildSubscription);
        });

        return configurator;
    }

    public static EventHorizonConfigurator AddMigrationHostedService<TSource, TTarget>(this EventHorizonConfigurator configurator,
        Action<AggregateBuilder<Snapshot<TTarget>, TTarget>> onBuild = null,
        Func<SubscriptionBuilder<Event>, SubscriptionBuilder<Event>> onBuildSubscription = null)
        where TSource : class, IState, new()
        where TTarget : class, IState, new()
    {
        configurator.AddEventSourcing();

        configurator.Collection.AddHostedService(x =>
        {
            var streamingClient = x.GetRequiredService<StreamingClient>();
            var builder = x.GetRequiredService<AggregateBuilder<Snapshot<TTarget>, TTarget>>();
            onBuild?.Invoke(builder);
            var aggregator = builder.Build();
            return new AggregateMigrationHostedService<TSource,TTarget>(aggregator, streamingClient,
                onBuildSubscription);
        });

        return configurator;
    }
}
