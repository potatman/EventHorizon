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

    public ITopicProducer<TMessage> CreateProducer<TMessage>(PublisherConfig config) where TMessage : ITopicMessage
    {
        return new PulsarTopicProducer<TMessage>(_pulsarClient, config,  CreateAdmin<TMessage>());
    }

    public ITopicConsumer<TMessage> CreateConsumer<TMessage>(SubscriptionConfig<TMessage> config)
        where TMessage : ITopicMessage
    {
        if (config.IsMessageOrderGuaranteedOnFailure)
            return new OrderGuaranteedPulsarTopicConsumer<TMessage>(_pulsarClient, config, this, _loggerFactory);
        return new PulsarTopicConsumer<TMessage>(_pulsarClient, config, CreateAdmin<TMessage>());
    }

    public ITopicReader<TMessage> CreateReader<TMessage>(ReaderConfig config) where TMessage : ITopicMessage
    {
        return new PulsarTopicReader<TMessage>(_pulsarClient, config, CreateAdmin<TMessage>());
    }

    public ITopicAdmin<TTMessage> CreateAdmin<TTMessage>() where TTMessage : ITopicMessage
    {
        return new PulsarTopicAdmin<TTMessage>(_pulsarClientResolver, _attributeUtil, _loggerFactory.CreateLogger<PulsarTopicAdmin<TTMessage>>());
    }
}
