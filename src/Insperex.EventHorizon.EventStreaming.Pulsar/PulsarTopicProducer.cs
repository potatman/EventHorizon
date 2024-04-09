using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicProducer<TMessage> : ITopicProducer<TMessage>
    where TMessage : ITopicMessage
{
    private readonly PulsarClient _pulsarClient;
    private readonly PublisherConfig _config;
    private readonly ITopicAdmin<TMessage> _admin;
    private readonly OTelProducerInterceptor.OTelProducerInterceptor<TMessage> _intercept;
    private readonly string _publisherName;
    private IProducer<TMessage> _producer;
    private readonly SemaphoreSlim _semaphoreSlim;

    public PulsarTopicProducer(
        PulsarClient pulsarClient,
        PublisherConfig config,
        ITopicAdmin<TMessage> admin)
    {
        _pulsarClient = pulsarClient;
        _config = config;
        _admin = admin;
        _publisherName = AssemblyUtil.AssemblyNameWithGuid;
        _intercept = new OTelProducerInterceptor.OTelProducerInterceptor<TMessage>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
        _semaphoreSlim = new SemaphoreSlim(1, 1);
    }

    public async Task SendAsync(params TMessage[] messages)
    {
        var producer = await GetProducerAsync().ConfigureAwait(false);

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
                            await producer.SendAsync(msg).ConfigureAwait(false);
                        else
                            await producer.SendAndForgetAsync(msg).ConfigureAwait(false);
                    }
                })
                .ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);
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
                        await producer.SendAsync(msg).ConfigureAwait(false);
                    else
                        await producer.SendAndForgetAsync(msg).ConfigureAwait(false);
                })
                .ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

    }

    private async Task<IProducer<TMessage>> GetProducerAsync()
    {
        // Defensive
        if (_producer != null) return _producer;

        // Lock is for Parallel Requests for some Producer
        await _semaphoreSlim.WaitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);

        // Second Release is if they got past first
        if (_producer != null) return _producer;

        // Ensure Topic Exists
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await _admin.RequireTopicAsync(_config.Topic, cts.Token).ConfigureAwait(false);

        var builder = _pulsarClient.NewProducer(Schema.JSON<TMessage>())
            .ProducerName(_publisherName)
            .BlockIfQueueFull(true)
            .BatchBuilder(BatchBuilder.KeyBased)
            .CompressionType(CompressionType.LZ4)
            .SendTimeout(_config.SendTimeout)
            .MaxPendingMessages(100000)
            .MaxPendingMessagesAcrossPartitions(500000)
            .Intercept(_intercept)
            .Topic(_config.Topic);

        _producer = await builder.CreateAsync().ConfigureAwait(false);

        _semaphoreSlim.Release();
        return _producer;
    }

    public async ValueTask DisposeAsync()
    {
        if(_producer != null) await _producer.DisposeAsync().ConfigureAwait(false);
        _producer = null;
    }
}
