using System;
using Insperex.EventHorizon.Abstractions;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Interfaces;
using Insperex.EventHorizon.EventSourcing.AggregateWorkflows.Workflows;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventSourcing.Util;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventSourcing.Extensions;

public static class ServiceCollectionExtensions
{
    public static EventHorizonConfigurator AddEventSourcing(this EventHorizonConfigurator configurator)
    {
        configurator.Collection.TryAddSingleton(typeof(EventSourcingClient<>));
        configurator.Collection.TryAddSingleton(typeof(AggregatorBuilder<,>));
        configurator.Collection.TryAddSingleton(typeof(SenderBuilder<>));
        configurator.Collection.TryAddSingleton(typeof(WorkflowFactory<>));
        configurator.Collection.TryAddSingleton(typeof(WorkflowService<,,>));
        configurator.Collection.TryAddSingleton<SenderSubscriptionTracker>();
        configurator.Collection.TryAddSingleton<ValidationUtil>();
        configurator.Collection.TryAddSingleton<IHostedService, WorkflowHostedService>();

        return configurator;
    }

    public static EventHorizonConfigurator HandleRequests<TState>(this EventHorizonConfigurator configurator,
        Action<WorkflowConfigurator<TState>> onConfig = null)
        where TState : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddSingleton<IWorkflow>(x => x.GetRequiredService<WorkflowFactory<TState>>().HandleRequests(onConfig));

        return configurator;
    }

    public static EventHorizonConfigurator HandleCommands<TState>(this EventHorizonConfigurator configurator,
        Action<WorkflowConfigurator<TState>> onConfig = null)
        where TState : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddSingleton<IWorkflow>(x => x.GetRequiredService<WorkflowFactory<TState>>().HandleCommands(onConfig));

        return configurator;
    }

    public static EventHorizonConfigurator HandleEvents<TState>(this EventHorizonConfigurator configurator,
        Action<WorkflowConfigurator<TState>> onConfig = null)
        where TState : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddSingleton<IWorkflow>(x => x.GetRequiredService<WorkflowFactory<TState>>().HandleEvents(onConfig));

        return configurator;
    }

    public static EventHorizonConfigurator ApplyEvents<TState>(this EventHorizonConfigurator configurator, Action<WorkflowConfigurator<TState>> onConfig = null)
        where TState : class, IState
    {
        configurator.AddEventSourcing();
        configurator.Collection.AddSingleton<IWorkflow>(x => x.GetRequiredService<WorkflowFactory<TState>>().ApplyEvents(onConfig));

        return configurator;
    }
}
