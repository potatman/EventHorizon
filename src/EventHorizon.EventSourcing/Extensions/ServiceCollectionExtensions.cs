using System;
using EventHorizon.Abstractions;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventSourcing.AggregateWorkflows;
using EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using EventHorizon.EventSourcing.Senders;
using EventHorizon.EventSourcing.Util;
using EventHorizon.EventStore;
using EventHorizon.EventStore.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace EventHorizon.EventSourcing.Extensions;

public static class ServiceCollectionExtensions
{
    public static EventHorizonConfigurator AddEventSourcing(this EventHorizonConfigurator configurator)
    {
        configurator.Collection.TryAddSingleton(typeof(EventSourcingClient<>));
        configurator.Collection.TryAddSingleton(typeof(AggregatorBuilder<,>));
        configurator.Collection.TryAddSingleton(typeof(SenderBuilder<>));
        configurator.Collection.TryAddSingleton(typeof(StoreBuilder<,>));
        configurator.Collection.TryAddSingleton(typeof(WorkflowFactory<>));
        configurator.Collection.TryAddSingleton(typeof(WorkflowService<,,>));
        configurator.Collection.TryAddSingleton<SenderSubscriptionTracker>();
        configurator.Collection.TryAddSingleton<ValidationUtil>();
        configurator.Collection.TryAddSingleton<IHostedService, WorkflowHostedService>();

        return configurator;
    }

    public static EventHorizonConfigurator HandleRequests<TState>(this EventHorizonConfigurator configurator,
        Action<WorkflowConfigurator<Snapshot<TState>, TState>> onConfig = null)
        where TState : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddSingleton<IWorkflow>(x => x.GetRequiredService<WorkflowFactory<TState>>().HandleRequests(onConfig));

        return configurator;
    }

    public static EventHorizonConfigurator HandleCommands<TState>(this EventHorizonConfigurator configurator,
        Action<WorkflowConfigurator<Snapshot<TState>, TState>> onConfig = null)
        where TState : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddSingleton<IWorkflow>(x => x.GetRequiredService<WorkflowFactory<TState>>().HandleCommands(onConfig));

        return configurator;
    }

    public static EventHorizonConfigurator HandleEvents<TState>(this EventHorizonConfigurator configurator,
        Action<WorkflowConfigurator<Snapshot<TState>, TState>> onConfig = null)
        where TState : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddSingleton<IWorkflow>(x => x.GetRequiredService<WorkflowFactory<TState>>().HandleEvents(onConfig));

        return configurator;
    }

    public static EventHorizonConfigurator ApplyEvents<TState>(this EventHorizonConfigurator configurator, Action<WorkflowConfigurator<View<TState>, TState>> onConfig = null)
        where TState : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddSingleton<IWorkflow>(x => x.GetRequiredService<WorkflowFactory<TState>>().ApplyEvents(onConfig));

        return configurator;
    }
}
