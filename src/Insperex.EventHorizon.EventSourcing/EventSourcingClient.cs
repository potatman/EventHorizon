using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventSourcing;

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
    public AggregateBuilder<Snapshot<TState>, TState> Aggregator() => _serviceProvider.GetRequiredService<AggregateBuilder<Snapshot<TState>, TState>>();
    public AggregateBuilder<View<TState>, TState> ViewAggregator() => _serviceProvider.GetRequiredService<AggregateBuilder<View<TState>, TState>>();
    public ICrudStore<Snapshot<TState>> GetSnapshotStore() => _serviceProvider.GetRequiredService<ISnapshotStore<TState>>();
    public ICrudStore<View<TState>> GetViewStore() => _serviceProvider.GetRequiredService<IViewStore<TState>>();
}
