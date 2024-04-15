using System;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventSourcing.AggregateWorkflows;
using EventHorizon.EventSourcing.Senders;
using EventHorizon.EventStore;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using Microsoft.Extensions.DependencyInjection;

namespace EventHorizon.EventSourcing;

public class EventSourcingClient<TState> where TState : class, IState, new()
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SenderBuilder<TState> _senderBuilder;

    public EventSourcingClient(
        SenderBuilder<TState> senderBuilder,
        IServiceProvider serviceProvider)
    {
        _senderBuilder = senderBuilder;
        _serviceProvider = serviceProvider;
    }

    public SenderBuilder<TState> CreateSender() => _senderBuilder;
    public WorkflowFactory<TState> Workflow() => _serviceProvider.GetRequiredService<WorkflowFactory<TState>>();
    public AggregatorBuilder<Snapshot<TState>, TState> Aggregator() => _serviceProvider.GetRequiredService<AggregatorBuilder<Snapshot<TState>, TState>>();
    public AggregatorBuilder<View<TState>, TState> ViewAggregator() => _serviceProvider.GetRequiredService<AggregatorBuilder<View<TState>, TState>>();
    public StoreBuilder<Snapshot<TState>, TState> GetSnapshotStore() => new(_serviceProvider.GetRequiredService<ISnapshotStore<TState>>());
    public StoreBuilder<View<TState>, TState> GetViewStore() => new(_serviceProvider.GetRequiredService<IViewStore<TState>>());
}
