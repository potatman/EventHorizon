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
using SharpPulsar.Admin.v2;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarStreamFactory : IStreamFactory
{
    private readonly PulsarClient _client;
    private readonly IPulsarAdminRESTAPIClient _admin;
    private readonly AttributeUtil _attributeUtil;
    private readonly IOptions<PulsarConfig> _options;
    private readonly ILoggerFactory _loggerFactory;

    public PulsarStreamFactory(
        PulsarClient client,
        IPulsarAdminRESTAPIClient admin,
        AttributeUtil attributeUtil,
        IOptions<PulsarConfig> options,
        ILoggerFactory loggerFactory)
    {
        _client = client;
        _admin = admin;
        _attributeUtil = attributeUtil;
        _options = options;
        _loggerFactory = loggerFactory;
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
        return new PulsarTopicAdmin(_admin, _options.Value, _loggerFactory.CreateLogger<PulsarTopicAdmin>());
    }

    public ITopicResolver GetTopicResolver()
    {
        return new PulsarTopicResolver(_attributeUtil);
    }
}
