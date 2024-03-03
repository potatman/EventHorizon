using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Extensions;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;
using NotSupportedException = System.NotSupportedException;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicConsumer<TMessage> : ITopicConsumer<TMessage>
    where TMessage : ITopicMessage
{
    private readonly PulsarClient _pulsarClient;
    private readonly SubscriptionConfig<TMessage> _config;
    private readonly ITopicAdmin<TMessage> _admin;
    private readonly OtelConsumerInterceptor.OTelConsumerInterceptor<TMessage> _intercept;
    private IConsumer<TMessage> _consumer;
    private Dictionary<string, Message<TMessage>> _unackedMessages = new();

    public PulsarTopicConsumer(
        PulsarClient pulsarClient,
        SubscriptionConfig<TMessage> config,
        ITopicAdmin<TMessage> admin)
    {
        _pulsarClient = pulsarClient;
        _config = config;
        _admin = admin;
        _intercept = new OtelConsumerInterceptor.OTelConsumerInterceptor<TMessage>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task InitAsync()
    {
        _consumer = await GetConsumerAsync();
    }

    public async Task<MessageContext<TMessage>[]> NextBatchAsync(CancellationToken ct)
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

                    var topicData = PulsarMessageMapper.MapTopicData(x.Key, x.Value, topic);
                    return new MessageContext<TMessage>(x.Value.GetValue(), topicData, _config.TypeDict);
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

    private async Task<Message<TMessage>[]> GetNextCustomAsync(CancellationToken ct)
    {
        var list = new List<Message<TMessage>>();

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

    private async Task<Message<TMessage>[]> GetNextBatchAsync(CancellationToken ct)
    {
        var list = new List<Message<TMessage>>();

        while (list.Count < _config.BatchSize && !ct.IsCancellationRequested)
        {
            var message = await _consumer.BatchReceiveAsync(ct);
            if (!message.Any())
                return list.ToArray();
            list.AddRange(message);
        }

        return list.ToArray();
    }

    public async Task FinalizeBatchAsync(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks)
    {
        await AckAsync(acks);
        await NackAsync(nacks);
    }

    private async Task AckAsync(params MessageContext<TMessage>[] messages)
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

    private async Task NackAsync(params MessageContext<TMessage>[] messages)
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

    private async Task<IConsumer<TMessage>> GetConsumerAsync()
    {
        // Ensure Topic Exists
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var topic in _config.Topics)
            await _admin.RequireTopicAsync(topic, cts.Token);

        var builder = _pulsarClient.NewConsumer(Schema.JSON<TMessage>())
            .ConsumerName(AssemblyUtil.AssemblyNameWithGuid)
            .SubscriptionType(_config.SubscriptionType.ToPulsarSubscriptionType())
            .SubscriptionName(_config.SubscriptionName)
            .ReceiverQueueSize(1000000000)
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
