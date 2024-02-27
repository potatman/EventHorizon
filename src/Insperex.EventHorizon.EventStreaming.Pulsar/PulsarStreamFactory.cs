using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarStreamFactory : IStreamFactory
{
    private readonly PulsarClientResolver _pulsarClientResolver;
    private readonly PulsarClient _pulsarClient;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILoggerFactory _loggerFactory;

    public PulsarStreamFactory(
        PulsarClientResolver pulsarClientResolver,
        PulsarClient pulsarClient,
        AttributeUtil attributeUtil,
        ILoggerFactory loggerFactory)
    {
        _pulsarClientResolver = pulsarClientResolver;
        _pulsarClient = pulsarClient;
        _attributeUtil = attributeUtil;
        _loggerFactory = loggerFactory;
    }

    public ITopicProducer<T> CreateProducer<T>(PublisherConfig config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicProducer<T>(_pulsarClient, config,  CreateAdmin<T>());
    }

    public ITopicConsumer<T> CreateConsumer<T>(SubscriptionConfig<T> config) where T : class, ITopicMessage, new()
    {
        if (config.IsMessageOrderGuaranteedOnFailure)
            return new OrderGuaranteedPulsarTopicConsumer<T>(_pulsarClient, config, this, _loggerFactory);
        return new PulsarTopicConsumer<T>(_pulsarClient, config, CreateAdmin<T>());
    }

    public ITopicReader<T> CreateReader<T>(ReaderConfig config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicReader<T>(_pulsarClient, config, CreateAdmin<T>());
    }

    public ITopicAdmin<T> CreateAdmin<T>() where T : ITopicMessage
    {
        return new PulsarTopicAdmin<T>(_pulsarClientResolver, _attributeUtil, _loggerFactory.CreateLogger<PulsarTopicAdmin<T>>());
    }
}
