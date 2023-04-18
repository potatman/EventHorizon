using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.InMemory.Databases;
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

    public InMemoryStreamFactory(AttributeUtil attributeUtil)
    {
        _attributeUtil = attributeUtil;
        _messageDatabase = new MessageDatabase();
        _indexDatabase = new IndexDatabase(_messageDatabase);
        _consumerDatabase = new ConsumerDatabase();
    }

    public ITopicProducer<T> CreateProducer<T>(PublisherConfig config) where T : class, ITopicMessage, new()
    {
        return new InMemoryTopicProducer<T>(config, _messageDatabase);
    }

    public ITopicConsumer<T> CreateConsumer<T>(SubscriptionConfig<T> config) where T : class, ITopicMessage, new()
    {
        return new InMemoryTopicConsumer<T>(config, _messageDatabase, _indexDatabase, _consumerDatabase);
    }

    public ITopicReader<T> CreateReader<T>(ReaderConfig config) where T : class, ITopicMessage, new()
    {
        return new InMemoryTopicReader<T>(config, _messageDatabase);
    }

    public ITopicAdmin CreateAdmin()
    {
        return new InMemoryTopicAdmin(_messageDatabase, _indexDatabase, _consumerDatabase);
    }

    public ITopicResolver GetTopicResolver()
    {
        return new InMemoryTopicResolver(_attributeUtil);
    }
}