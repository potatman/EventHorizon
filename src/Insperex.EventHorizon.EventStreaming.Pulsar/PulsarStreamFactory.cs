using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pulsar.Client.Api;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarStreamFactory : IStreamFactory
{
    private readonly PulsarClient _client;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<PulsarConfig> _options;

    public PulsarStreamFactory(
        PulsarClient client,
        AttributeUtil attributeUtil,
        ILoggerFactory loggerFactory, 
        IOptions<PulsarConfig> options)
    {
        _client = client;
        _attributeUtil = attributeUtil;
        _loggerFactory = loggerFactory;
        _options = options;
    }

    public ITopicProducer<T> CreateProducer<T>(PublisherConfig config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicProducer<T>(_client, config, _loggerFactory.CreateLogger<PulsarTopicProducer<T>>());
    }

    public ITopicConsumer<T> CreateConsumer<T>(SubscriptionConfig<T> config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicConsumer<T>(_client, config);
    }

    public ITopicReader<T> CreateReader<T>(ReaderConfig config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicReader<T>(_client, config);
    }

    public ITopicAdmin CreateAdmin()
    {
        return new PulsarTopicAdmin(_options);
    }

    public ITopicResolver GetTopicResolver()
    {
        return new PulsarTopicResolver(_attributeUtil);
    }
}