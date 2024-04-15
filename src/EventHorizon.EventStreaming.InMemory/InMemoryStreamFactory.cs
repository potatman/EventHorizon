using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.InMemory.Databases;
using EventHorizon.EventStreaming.InMemory.Failure;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Readers;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming.InMemory;

public class InMemoryStreamFactory : IStreamFactory
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

    public ITopicProducer<TMessage> CreateProducer<TMessage>(PublisherConfig config) where TMessage : ITopicMessage
    {
        return new InMemoryTopicProducer<TMessage>(config, _messageDatabase);
    }

    public ITopicConsumer<TMessage> CreateConsumer<TMessage>(SubscriptionConfig<TMessage> config) where TMessage : ITopicMessage
    {
        return new InMemoryTopicConsumer<TMessage>(config, _messageDatabase, _indexDatabase, _consumerDatabase,
            _failureHandlerFactory, _loggerFactory);
    }

    public ITopicReader<TMessage> CreateReader<TMessage>(ReaderConfig config) where TMessage : ITopicMessage
    {
        return new InMemoryTopicReader<TMessage>(config, _messageDatabase);
    }

    public ITopicAdmin<TMessage> CreateAdmin<TMessage>() where TMessage : ITopicMessage
    {
        return new InMemoryTopicAdmin<TMessage>(_messageDatabase, _indexDatabase, _consumerDatabase);
    }
}
