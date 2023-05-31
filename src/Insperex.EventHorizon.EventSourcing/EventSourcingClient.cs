using System;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.DependencyInjection;

namespace Insperex.EventHorizon.EventSourcing;

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
