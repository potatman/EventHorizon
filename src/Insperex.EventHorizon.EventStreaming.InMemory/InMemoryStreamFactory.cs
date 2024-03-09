using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.InMemory.Databases;
using Insperex.EventHorizon.EventStreaming.InMemory.Failure;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.InMemory;

public class InMemoryStreamFactory<TMessage> : IStreamFactory<TMessage>
    where TMessage : ITopicMessage
{
    private readonly IndexDatabase _indexDatabase;
    private readonly MessageDatabase _messageDatabase;
    private readonly ConsumerDatabase _consumerDatabase;
    private readonly FailureHandlerFactory _failureHandlerFactory;
    private readonly ILoggerFactory _loggerFactory;

    public InMemoryStreamFactory(MessageDatabase messageDatabase,
        IndexDatabase indexDatabase, ConsumerDatabase consumerDatabase, FailureHandlerFactory failureHandlerFactory,
        ILoggerFactory loggerFactory)
    {
        _messageDatabase = messageDatabase;
        _indexDatabase = indexDatabase;
        _consumerDatabase = consumerDatabase;
        _failureHandlerFactory = failureHandlerFactory;
        _loggerFactory = loggerFactory;
    }

    public ITopicProducer<TMessage> CreateProducer(PublisherConfig config)
    {
        return new InMemoryTopicProducer<TMessage>(config, _messageDatabase);
    }

    public ITopicConsumer<TMessage> CreateConsumer(SubscriptionConfig<TMessage> config)
    {
        return new InMemoryTopicConsumer<TMessage>(config, _messageDatabase, _indexDatabase, _consumerDatabase,
            _failureHandlerFactory, _loggerFactory);
    }

    public ITopicReader<TMessage> CreateReader(ReaderConfig config)
    {
        return new InMemoryTopicReader<TMessage>(config, _messageDatabase);
    }

    public ITopicAdmin<TMessage> CreateAdmin()
    {
        return new InMemoryTopicAdmin<TMessage>(_messageDatabase, _indexDatabase, _consumerDatabase);
    }
}
