using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Insperex.EventHorizon.EventStreaming.Util;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicProducer<T> : ITopicProducer<T>
    where T : class, ITopicMessage
{
    private readonly PulsarClientResolver _clientResolver;
    private readonly PublisherConfig _config;
    private readonly ITopicAdmin<T> _admin;
    private readonly OTelProducerInterceptor.OTelProducerInterceptor<T> _intercept;
    private readonly string _publisherName;
    private IProducer<T> _producer;

    public PulsarTopicProducer(
        PulsarClientResolver clientResolver,
        PublisherConfig config,
        ITopicAdmin<T> admin)
    {
        _clientResolver = clientResolver;
        _config = config;
        _admin = admin;
        _publisherName = NameUtil.AssemblyNameWithGuid;
        _intercept = new OTelProducerInterceptor.OTelProducerInterceptor<T>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task SendAsync(params T[] messages)
    {
        var producer = await GetProducerAsync();
        foreach (var message in messages)
        {
            var func = AssemblyUtil.PropertyDict.GetValueOrDefault(message.Type)?
                .FirstOrDefault(x => x.GetCustomAttribute<StreamKeyAttribute>(true) != null);

            var key = func?.GetValue(message)?.ToString() ?? message.StreamId;
            var msg = producer.NewMessage(message, key);
            await producer.SendAndForgetAsync(msg);
        }
    }

    public void Dispose()
    {
        _producer?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _producer = null;
    }

    private async Task<IProducer<T>> GetProducerAsync()
    {
        if (_producer != null) return _producer;

        // Ensure Topic Exists
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _admin.RequireTopicAsync(_config.Topic, cts.Token);

        var client = await _clientResolver.GetPulsarClientAsync();
        var builder = client.NewProducer(Schema.JSON<T>())
            .ProducerName(_publisherName)
            .BlockIfQueueFull(true)
            .BatchBuilder(BatchBuilder.KeyBased)
            .CompressionType(CompressionType.LZ4)
            .MaxPendingMessages(10000)
            .MaxPendingMessagesAcrossPartitions(50000)
            .Intercept(_intercept)
            .Topic(_config.Topic);

        _producer = await builder.CreateAsync();

        return _producer;
    }

}
