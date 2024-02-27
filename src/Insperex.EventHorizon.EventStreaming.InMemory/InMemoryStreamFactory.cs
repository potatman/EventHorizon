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

public class InMemoryStreamFactory : IStreamFactory
{
    private readonly AttributeUtil _attributeUtil;
    private readonly IndexDatabase _indexDatabase;
    private readonly MessageDatabase _messageDatabase;
    private readonly ConsumerDatabase _consumerDatabase;
    private readonly FailureHandlerFactory _failureHandlerFactory;
    private readonly ILoggerFactory _loggerFactory;

    public InMemoryStreamFactory(AttributeUtil attributeUtil, MessageDatabase messageDatabase,
        IndexDatabase indexDatabase, ConsumerDatabase consumerDatabase, FailureHandlerFactory failureHandlerFactory,
        ILoggerFactory loggerFactory)
    {
        _attributeUtil = attributeUtil;
        _messageDatabase = messageDatabase;
        _indexDatabase = indexDatabase;
        _consumerDatabase = consumerDatabase;
        _failureHandlerFactory = failureHandlerFactory;
        _loggerFactory = loggerFactory;
    }

    public ITopicProducer<T> CreateProducer<T>(PublisherConfig config) where T : class, ITopicMessage, new()
    {
        return new InMemoryTopicProducer<T>(config, _messageDatabase);
    }

    public ITopicConsumer<T> CreateConsumer<T>(SubscriptionConfig<T> config) where T : class, ITopicMessage, new()
    {
        return new InMemoryTopicConsumer<T>(config, _messageDatabase, _indexDatabase, _consumerDatabase,
            _failureHandlerFactory, _loggerFactory);
    }

    public ITopicReader<T> CreateReader<T>(ReaderConfig config) where T : class, ITopicMessage, new()
    {
        return new InMemoryTopicReader<T>(config, _messageDatabase);
    }

    public ITopicAdmin<T> CreateAdmin<T>() where T : ITopicMessage
    {
        return new InMemoryTopicAdmin<T>(_attributeUtil, _messageDatabase, _indexDatabase, _consumerDatabase);
    }
}
