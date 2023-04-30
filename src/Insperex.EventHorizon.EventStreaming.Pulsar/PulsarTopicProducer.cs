﻿using System;
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
    private readonly PulsarClient _client;
    private readonly PublisherConfig _config;
    private readonly ITopicAdmin _admin;
    private readonly OTelProducerInterceptor.OTelProducerInterceptor<T> _intercept;
    private readonly ILogger<PulsarTopicProducer<T>> _logger;
    private readonly string _publisherName;
    private bool _hasSent;
    private IProducer<T> _producer;
    private bool _running;

    public PulsarTopicProducer(
        PulsarClient client,
        PublisherConfig config,
        ITopicAdmin admin,
        ILogger<PulsarTopicProducer<T>> logger)
    {
        _client = client;
        _config = config;
        _admin = admin;
        _logger = logger;
        _publisherName = NameUtil.AssemblyNameWithGuid;
        _intercept = new OTelProducerInterceptor.OTelProducerInterceptor<T>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
        TrackStats();
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
        _running = false;
        _hasSent = false;
        _producer.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _producer = null;
    }

    private async Task<IProducer<T>> GetProducerAsync()
    {
        if (_producer != null) return _producer;

        // Ensure Topic Exists
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _admin.RequireTopicAsync(_config.Topic, cts.Token);

        var builder = _client.NewProducer(Schema.JSON<T>())
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

    private async void TrackStats()
    {
        _running = true;
        while (_running)
        {
            await Task.Delay(TimeSpan.FromMinutes(5));

            // Dont Start until first send
            if (_hasSent)
                continue;

            try
            {
                var stats = await _producer.GetStatsAsync();
                _logger.LogInformation("Publisher Stats {Name} => {@Stats}", _publisherName, stats);
                await Task.Delay(TimeSpan.FromMinutes(5));
            }
            catch
            {
                // ignored
            }
        }
    }
}
