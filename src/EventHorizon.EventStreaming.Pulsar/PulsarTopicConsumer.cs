using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Pulsar.Extensions;
using EventHorizon.EventStreaming.Pulsar.Utils;
using EventHorizon.EventStreaming.Subscriptions;
using EventHorizon.EventStreaming.Subscriptions.Backoff;
using EventHorizon.EventStreaming.Tracing;
using EventHorizon.EventStreaming.Util;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;
using NotSupportedException = System.NotSupportedException;
using SubscriptionType = Pulsar.Client.Common.SubscriptionType;

namespace EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicConsumer<T> : ITopicConsumer<T> where T : ITopicMessage, new()
{
    private readonly PulsarClientResolver _clientResolver;
    private readonly SubscriptionConfig<T> _config;
    private readonly ITopicAdmin<T> _admin;
    private readonly OtelConsumerInterceptor.OTelConsumerInterceptor<T> _intercept;
    private IConsumer<T> _consumer;
    private Dictionary<string, Message<T>> _unackedMessages = new();

    public PulsarTopicConsumer(
        PulsarClientResolver clientResolver,
        SubscriptionConfig<T> config,
        ITopicAdmin<T> admin)
    {
        _clientResolver = clientResolver;
        _config = config;
        _admin = admin;
        _intercept = new OtelConsumerInterceptor.OTelConsumerInterceptor<T>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task InitAsync()
    {
        _consumer = await GetConsumerAsync();
    }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        try
        {
            var messages = await GetNextCustomAsync(ct);
            if (!messages.Any())
                return null;

            _unackedMessages = messages.ToDictionary(x => Guid.NewGuid().ToString());

            var contexts =  _unackedMessages
                .Select((x) =>
                {
                    // Note: x.MessageId.TopicName is null, when single tropic
                    var topic = _config.Topics.Length == 1 ? _config.Topics.First() : x.Value.MessageId.TopicName;

                    return new MessageContext<T>
                    {
                        Data = x.Value.GetValue(),
                        TopicData = PulsarMessageMapper.MapTopicData(x.Key, x.Value, topic)
                    };
                })
                .ToArray();

            return contexts;
        }
        catch (AlreadyClosedException)
        {
            // Ignore AlreadyClosedException
            return null;
        }

    }

    private async Task<Message<T>[]> GetNextCustomAsync(CancellationToken ct)
    {
        var list = new List<Message<T>>();

        try
        {
            while (list.Count < _config.BatchSize && !ct.IsCancellationRequested)
            {
                var cts = new CancellationTokenSource(25);
                var message = await _consumer.ReceiveAsync(cts.Token);
                list.Add(message);
            }
        }
        catch (TaskCanceledException)
        {
            // ignore
        }

        return list.ToArray();
    }

    private async Task<Message<T>[]> GetNextBatchAsync(CancellationToken ct)
    {
        var list = new List<Message<T>>();

        while (list.Count < _config.BatchSize && !ct.IsCancellationRequested)
        {
            var message = await _consumer.BatchReceiveAsync(ct);
            if (!message.Any())
                return list.ToArray();
            list.AddRange(message);
        }

        return list.ToArray();
    }

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        await AckAsync(acks);
        await NackAsync(nacks);
    }

    private async Task AckAsync(params MessageContext<T>[] messages)
    {
        if (messages?.Any() != true) return;
        var tasks = messages
            .AsParallel()
            .Select(async message =>
            {

                var unackedMessage = _unackedMessages[message.TopicData.Id];
                await _consumer.AcknowledgeAsync(unackedMessage.MessageId);
                _unackedMessages.Remove(message.TopicData.Id);
            })
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task NackAsync(params MessageContext<T>[] messages)
    {
        if (messages?.Any() != true) return;
        var tasks = messages
            .AsParallel()
            .Select(async message =>
            {
                var unackedMessage = _unackedMessages[message.TopicData.Id];

                if (_config.RedeliverFailedMessages)
                    await _consumer.NegativeAcknowledge(unackedMessage.MessageId);
                else
                    await _consumer.AcknowledgeAsync(unackedMessage.MessageId);

                _unackedMessages.Remove(message.TopicData.Id);
            })
            .ToArray();

        await Task.WhenAll(tasks);
    }

    private async Task<IConsumer<T>> GetConsumerAsync()
    {
        // Ensure Topic Exists
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var topic in _config.Topics)
            await _admin.RequireTopicAsync(topic, cts.Token);

        var client = await _clientResolver.GetPulsarClientAsync();
        var builder = client.NewConsumer(Schema.JSON<T>())
            .ConsumerName(NameUtil.AssemblyNameWithGuid)
            .SubscriptionType(_config.SubscriptionType.ToPulsarSubscriptionType())
            .SubscriptionName(_config.SubscriptionName)
            .ReceiverQueueSize(1000000000) // Allows non-persistent queues to not lose messages
            .Intercept(_intercept);

        if (_config.Topics != null)
            builder = _config.Topics.Length == 1
                ? builder.Topic(_config.Topics.First())
                : builder.Topics(_config.Topics);

        if (_config.IsBeginning != null)
            builder = builder.SubscriptionInitialPosition(
                _config.IsBeginning == true
                    ? SubscriptionInitialPosition.Earliest
                    : SubscriptionInitialPosition.Latest);

        if (_config.BackoffStrategy != null)
        {
            if (_config.BackoffStrategy is IConstantBackoffStrategy strategy)
            {
                builder = builder.NegativeAckRedeliveryDelay(strategy.Delay);
            }
            else
            {
                throw new NotSupportedException(
                    "Only constant backoff strategies supported for the default consumer.");
            }
        }

        var consumer = await builder.SubscribeAsync();

        if (_config.StartDateTime != null)
            await consumer.SeekAsync(_config.StartDateTime.Value.Ticks);

        // Return
        return consumer;
    }

    public async ValueTask DisposeAsync()
    {
        if(_consumer != null) await _consumer.DisposeAsync();
        _consumer = null;
    }
}
