using System.Linq;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Extensions;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Pulsar.AdvancedFailure;
using EventHorizon.EventStreaming.Pulsar.Utils;
using EventHorizon.EventStreaming.Readers;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming.Pulsar;

public class PulsarStreamFactory : IStreamFactory
{
    private readonly PulsarClientResolver _clientResolver;
    private readonly AttributeUtil _attributeUtil;
    private readonly ILoggerFactory _loggerFactory;

    public PulsarStreamFactory(
        PulsarClientResolver clientResolver,
        AttributeUtil attributeUtil,
        ILoggerFactory loggerFactory)
    {
        _clientResolver = clientResolver;
        _attributeUtil = attributeUtil;
        _loggerFactory = loggerFactory;
    }

    public ITopicProducer<T> CreateProducer<T>(PublisherConfig config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicProducer<T>(_clientResolver, config, _attributeUtil, CreateAdmin<T>());
    }

    public ITopicConsumer<T> CreateConsumer<T>(SubscriptionConfig<T> config) where T : class, ITopicMessage, new()
    {
        if (config.IsMessageOrderGuaranteedOnFailure)
        {
            return new OrderGuaranteedPulsarTopicConsumer<T>(_clientResolver, config,
                this, _loggerFactory);
        }
        return new PulsarTopicConsumer<T>(_clientResolver, config, CreateAdmin<T>());
    }

    public ITopicReader<T> CreateReader<T>(ReaderConfig config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicReader<T>(_clientResolver, config, CreateAdmin<T>());
    }

    public ITopicAdmin<T> CreateAdmin<T>() where T : ITopicMessage
    {
        return new PulsarTopicAdmin<T>(_clientResolver, _attributeUtil, _loggerFactory.CreateLogger<PulsarTopicAdmin<T>>());
    }

    public ITopicResolver GetTopicResolver()
    {
        return new PulsarTopicResolver(_attributeUtil);
    }
}
