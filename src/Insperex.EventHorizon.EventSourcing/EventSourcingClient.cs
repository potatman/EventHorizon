using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventStore.Interfaces.Factory;
using Insperex.EventHorizon.EventStore.Interfaces.Stores;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventSourcing;

public class EventSourcingClient<T> where T : class, IState, new()
{
    private readonly AggregateBuilder<Snapshot<T>, T> _aggregateBuilder;
    private readonly ISnapshotStoreFactory<T> _snapshotStoreFactory;
    private readonly IViewStoreFactory<T> _viewStoreFactory;
    private readonly ReaderBuilder<Event> _readerBuilder;
    private readonly SubscriptionBuilder<Event> _subscriberBuilder;
    private readonly SenderBuilder _senderBuilder;

    public EventSourcingClient(
        SenderBuilder senderBuilder,
        ReaderBuilder<Event> readerBuilder,
        SubscriptionBuilder<Event> subscriberBuilder,
        AggregateBuilder<Snapshot<T>, T> aggregateBuilder,
        ISnapshotStoreFactory<T> snapshotStoreFactory,
        IViewStoreFactory<T> viewStoreFactory)
    {
        _senderBuilder = senderBuilder;
        _readerBuilder = readerBuilder;
        _subscriberBuilder = subscriberBuilder;
        _aggregateBuilder = aggregateBuilder;
        _snapshotStoreFactory = snapshotStoreFactory;
        _viewStoreFactory = viewStoreFactory;
    }

    public SenderBuilder CreateSender() => _senderBuilder;
    public ReaderBuilder<Event> CreateReader() => _readerBuilder;
    public SubscriptionBuilder<Event> CreateSubscription() => _subscriberBuilder;
    public AggregateBuilder<Snapshot<T>, T> Aggregator() => _aggregateBuilder;
    public ICrudStore<Snapshot<T>> GetSnapshotStore() => _snapshotStoreFactory.GetSnapshotStore();
    public ICrudStore<View<T>> GetViewStore() => _viewStoreFactory.GetViewStore();
}