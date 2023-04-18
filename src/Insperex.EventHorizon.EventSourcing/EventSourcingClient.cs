using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventSourcing.Aggregates;
using Insperex.EventHorizon.EventSourcing.Senders;
using Insperex.EventHorizon.EventStore.Models;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventSourcing;

public class EventSourcingClient<T> where T : class, IState, new()
{
    private readonly AggregateBuilder<Snapshot<T>, T> _aggregateBuilder;
    private readonly ReaderBuilder<Event> _readerBuilder;
    private readonly SubscriptionBuilder<Event> _subscriberBuilder;
    private readonly SenderBuilder _senderBuilder;

    public EventSourcingClient(
        SenderBuilder senderBuilder,
        ReaderBuilder<Event> readerBuilder,
        SubscriptionBuilder<Event> subscriberBuilder,
        AggregateBuilder<Snapshot<T>, T> aggregateBuilder)
    {
        _senderBuilder = senderBuilder;
        _readerBuilder = readerBuilder;
        _subscriberBuilder = subscriberBuilder;
        _aggregateBuilder = aggregateBuilder;
    }

    public SenderBuilder CreateSender()
    {
        return _senderBuilder;
    }

    public ReaderBuilder<Event> CreateReader()
    {
        return _readerBuilder;
    }

    public SubscriptionBuilder<Event> CreateSubscription()
    {
        return _subscriberBuilder;
    }

    public AggregateBuilder<Snapshot<T>, T> Aggregator()
    {
        return _aggregateBuilder;
    }
}