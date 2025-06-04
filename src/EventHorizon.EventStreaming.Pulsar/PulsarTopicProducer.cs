using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Pulsar.Models;
using EventHorizon.EventStreaming.Tracing;
using EventHorizon.EventStreaming.Util;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;

namespace EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicProducer<T> : ITopicProducer<T>
    where T : class, ITopicMessage
{
    private readonly PulsarClientResolver _clientResolver;
    private readonly PublisherConfig _config;
    private readonly AttributeUtil _attributeUtil;
    private readonly ITopicAdmin<T> _admin;
    private readonly OTelProducerInterceptor.OTelProducerInterceptor<T> _intercept;
    private readonly string _publisherName;
    private IProducer<T> _producer;
    private readonly SemaphoreSlim _semaphoreSlim;

    public PulsarTopicProducer(
        PulsarClientResolver clientResolver,
        PublisherConfig config,
        AttributeUtil attributeUtil,
        ITopicAdmin<T> admin)
    {
        _clientResolver = clientResolver;
        _config = config;
        _attributeUtil = attributeUtil;
        _admin = admin;
        _publisherName = NameUtil.AssemblyNameWithGuid;
        _intercept = new OTelProducerInterceptor.OTelProducerInterceptor<T>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
        _semaphoreSlim = new SemaphoreSlim(1, 1);
    }

    public async Task SendAsync(params T[] messages)
    {
        var producer = await GetProducerAsync();

        if (_config.IsOrderGuaranteed)
        {
            var tasks = messages
                .GroupBy(x => x.StreamId)
                .AsParallel()
                .Select(async grouping =>
                {
                    foreach (var message in grouping)
                    {
                        // var type = AssemblyUtil.ActionDict[message.Type];
                        // var func = _attributeUtil.GetOnePropertyInfo<StreamPartitionKeyAttribute>(type);
                        // var key = func?.GetValue(message)?.ToString() ?? message.StreamId;
                        var msg = producer.NewMessage(message, message.StreamId);

                        // Send Message
                        if (_config.IsGuaranteed)
                            await producer.SendAsync(msg);
                        else
                            await producer.SendAndForgetAsync(msg);
                    }
                })
                .ToArray();

            await Task.WhenAll(tasks);
        }
        else
        {
            var tasks = messages
                .AsParallel()
                .Select(async message =>
                {
                    // var type = AssemblyUtil.ActionDict[message.Type];
                    // var func = _attributeUtil.GetOnePropertyInfo<StreamPartitionKeyAttribute>(type);
                    // var key = func?.GetValue(message)?.ToString() ?? message.StreamId;
                    var msg = producer.NewMessage(message, message.StreamId);

                    // Send Message
                    if (_config.IsGuaranteed)
                        await producer.SendAsync(msg);
                    else
                        await producer.SendAndForgetAsync(msg);
                })
                .ToArray();

            await Task.WhenAll(tasks);
        }

    }

    private async Task<IProducer<T>> GetProducerAsync()
    {
        // Defensive
        if (_producer != null) return _producer;

        // Lock is for Parallel Requests for some Producer
        await _semaphoreSlim.WaitAsync(TimeSpan.FromSeconds(10));

        // Second Release is if they got past first
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
            .SendTimeout(_config.SendTimeout)
            .MaxPendingMessages(100000)
            .MaxPendingMessagesAcrossPartitions(500000)
            .Intercept(_intercept)
            .Topic(_config.Topic);

        _producer = await builder.CreateAsync();

        _semaphoreSlim.Release();
        return _producer;
    }

    public async ValueTask DisposeAsync()
    {
        if(_producer != null) await _producer.DisposeAsync();
        _producer = null;
    }
}
