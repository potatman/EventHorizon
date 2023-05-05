using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Pulsar.Attributes;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pulsar.Client.Api;
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

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
        return new PulsarTopicProducer<T>(_clientResolver, config, CreateAdmin<T>());
    }

    public ITopicConsumer<T> CreateConsumer<T>(SubscriptionConfig<T> config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicConsumer<T>(_clientResolver, config, CreateAdmin<T>());
    }

    public ITopicReader<T> CreateReader<T>(ReaderConfig config) where T : class, ITopicMessage, new()
    {
        return new PulsarTopicReader<T>(_clientResolver, config, CreateAdmin<T>());
    }

    public ITopicAdmin<T> CreateAdmin<T>() where T : ITopicMessage
    {
        return new PulsarTopicAdmin<T>(_clientResolver.GetAdminClient(), _attributeUtil, _loggerFactory.CreateLogger<PulsarTopicAdmin<T>>());
    }

    public ITopicResolver GetTopicResolver()
    {
        return new PulsarTopicResolver(_attributeUtil);
    }
}
