using System;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventSourcing.Aggregates;
using EventHorizon.EventSourcing.Senders;
using EventHorizon.EventStore.Interfaces.Factory;
using EventHorizon.EventStore.Interfaces.Stores;
using EventHorizon.EventStore.Models;
using EventHorizon.EventStreaming.Readers;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

namespace EventHorizon.EventSourcing;

public class EventSourcingClient<T> where T : class, IState, new()
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SenderBuilder _senderBuilder;

    public EventSourcingClient(
        SenderBuilder senderBuilder,
        IServiceProvider serviceProvider)
    {
        _senderBuilder = senderBuilder;
        _serviceProvider = serviceProvider;
    }

    public SenderBuilder CreateSender() => _senderBuilder;
    public AggregateBuilder<Snapshot<T>, T> Aggregator() => _serviceProvider.GetRequiredService<AggregateBuilder<Snapshot<T>, T>>();
    public ICrudStore<Snapshot<T>> GetSnapshotStore() => _serviceProvider.GetRequiredService<ISnapshotStoreFactory<T>>().GetSnapshotStore();
    public ICrudStore<View<T>> GetViewStore() => _serviceProvider.GetRequiredService<IViewStoreFactory<T>>().GetViewStore();
}
