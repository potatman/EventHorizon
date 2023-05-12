using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Part of the advanced failure handling logic orchestrated by <see cref="OrderGuaranteedPulsarTopicConsumer{T}"/>.
/// Handles consuming messages from the primary topic, during the normal batch phase.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
internal sealed class PrimaryTopicConsumer<T>: ITopicConsumer<T> where T : ITopicMessage, new()
{
    private readonly StreamFailureState<T> _streamFailureState;
    private readonly MessageRecoveryTopic<T> _messageRecoveryTopic;
    private readonly PulsarClientResolver _clientResolver;
    private readonly SubscriptionConfig<T> _config;
    private readonly ITopicAdmin<T> _admin;
    private readonly string _consumerName;
    private readonly OtelConsumerInterceptor.OTelConsumerInterceptor<T> _intercept;
    private MessageId[] _messageIds;
    private IConsumer<T> _consumer;

    public PrimaryTopicConsumer(
        StreamFailureState<T> streamFailureState,
        MessageRecoveryTopic<T> messageRecoveryTopic,
        PulsarClientResolver clientResolver,
        SubscriptionConfig<T> config,
        ITopicAdmin<T> admin,
        string consumerName)
    {
        _streamFailureState = streamFailureState;
        _messageRecoveryTopic = messageRecoveryTopic;
        _clientResolver = clientResolver;
        _config = config;
        _admin = admin;
        _consumerName = consumerName;
        _intercept = new OtelConsumerInterceptor.OTelConsumerInterceptor<T>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _messageRecoveryTopic.InitializeAsync(ct);
    }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        try
        {
            var consumer = await GetConsumerAsync();
            var messages = await consumer.BatchReceiveAsync(ct);

            var triagedMessages = messages
                .Select(m =>
                (
                    Data: m.GetValue(),
                    OriginalMessage: m,
                    Topic: _config.Topics.Length == 1 ? _config.Topics.First() : m.MessageId.TopicName
                ))
                .ToLookup(m => _streamFailureState.IsStreamInNormalState(m.Data.StreamId));

            if (triagedMessages[false].Any())
            {
                await ForwardRecoveryMessages(triagedMessages[false].ToArray());
            }

            var messagesForConsumer = triagedMessages[true].ToArray();

            if (!messagesForConsumer.Any())
            {
                await Task.Delay(_config.NoBatchDelay, ct);
                return null;
            }

            _messageIds = messagesForConsumer
                .Select(m => m.OriginalMessage.MessageId)
                .ToArray();

            var contexts = messagesForConsumer
                .Select((x,i) =>
                    new MessageContext<T>
                    {
                        Data = x.Data,
                        TopicData = PulsarMessageMapper.MapTopicData(i.ToString(CultureInfo.InvariantCulture),
                            x.OriginalMessage, x.Topic)
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

    private async Task ForwardRecoveryMessages((T Data, Message<T> OriginalMessage, string Topic)[] recoveryMessages)
    {
        var messagesForRecoveryTopic = recoveryMessages
            .Select(m =>
                new RecoveryMessage<T>
                {
                    StreamId = m.Data.StreamId,
                    Payload = m.Data,
                    PublishTime = new DateTime(m.OriginalMessage.PublishTime),
                    Topic = m.Topic,
                })
            .ToArray();

        await _messageRecoveryTopic.PublishMessages(messagesForRecoveryTopic);
    }

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        var results = acks.Select(a => (IsSuccess: true, Message: a))
            .Concat(nacks.Select(n => (IsSuccess: false, Message: n)));
        var resultsByStream = results
            .ToLookup(r => r.Message.Data.StreamId);

        foreach (var streamResults in resultsByStream)
        {
            var firstFailedMessage = streamResults
                .Where(r => !r.IsSuccess)
                .Select(r => r.Message)
                .FirstOrDefault();
            var anyFailed = firstFailedMessage != null;

            if (anyFailed)
            {
                await _streamFailureState.MessageProcessingFailed(firstFailedMessage);
            }
        }

        await AckToPulsarAsync();
    }

    private async Task AckToPulsarAsync()
    {
        var consumer = await GetConsumerAsync();
        foreach (var messageId in _messageIds)
            await consumer.AcknowledgeAsync(messageId);
    }

    public async ValueTask DisposeAsync()
    {
        if(_consumer != null) await _consumer.DisposeAsync();
        _consumer = null;
    }

    private async Task<IConsumer<T>> GetConsumerAsync()
    {
        if (_consumer != null)
            return _consumer;

        // Ensure Topic Exists
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var topic in _config.Topics)
            await _admin.RequireTopicAsync(topic, cts.Token);

        var client = await _clientResolver.GetPulsarClientAsync();
        var builder = client.NewConsumer(Schema.JSON<T>())
            .ConsumerName(_consumerName)
            .SubscriptionType(SubscriptionType.KeyShared)
            .SubscriptionName(_config.SubscriptionName)
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

        if (_config.BatchSize != null)
            builder = builder.ReceiverQueueSize(_config.BatchSize.Value);

        var consumer = await builder.SubscribeAsync();

        if (_config.StartDateTime != null)
            await consumer.SeekAsync(_config.StartDateTime.Value.Ticks);

        // Return
        return _consumer = consumer;
    }
}
