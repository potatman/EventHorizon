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

public class PulsarStreamFactory<TMessage> : IStreamFactory<TMessage>
    where TMessage : ITopicMessage
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

    public ITopicProducer<TMessage> CreateProducer(PublisherConfig config)
    {
        return new PulsarTopicProducer<TMessage>(_pulsarClient, config,  CreateAdmin());
    }

    public ITopicConsumer<TMessage> CreateConsumer(SubscriptionConfig<TMessage> config)
    {
        if (config.IsMessageOrderGuaranteedOnFailure)
            return new OrderGuaranteedPulsarTopicConsumer<TMessage>(_pulsarClient, config, this, _loggerFactory);
        return new PulsarTopicConsumer<TMessage>(_pulsarClient, config, CreateAdmin());
    }

    public ITopicReader<TMessage> CreateReader(ReaderConfig config)
    {
        return new PulsarTopicReader<TMessage>(_pulsarClient, config, CreateAdmin());
    }

    public ITopicAdmin<TMessage> CreateAdmin()
    {
        return new PulsarTopicAdmin<TMessage>(_pulsarClientResolver, _attributeUtil, _loggerFactory.CreateLogger<PulsarTopicAdmin<TMessage>>());
    }
}
