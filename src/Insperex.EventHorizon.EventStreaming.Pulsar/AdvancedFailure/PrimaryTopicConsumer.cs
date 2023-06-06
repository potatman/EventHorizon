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
using Microsoft.Extensions.Logging;
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
    private readonly PulsarClientResolver _clientResolver;
    private readonly ILogger<PrimaryTopicConsumer<T>> _logger;
    private readonly SubscriptionConfig<T> _config;
    private readonly ITopicAdmin<T> _admin;
    private readonly string _consumerName;
    private readonly OtelConsumerInterceptor.OTelConsumerInterceptor<T> _intercept;
    private MessageId[] _messageIds;
    private IConsumer<T> _consumer;

    public PrimaryTopicConsumer(
        StreamFailureState<T> streamFailureState,
        PulsarClientResolver clientResolver,
        ILogger<PrimaryTopicConsumer<T>> logger,
        SubscriptionConfig<T> config,
        ITopicAdmin<T> admin,
        string consumerName)
    {
        _streamFailureState = streamFailureState;
        _clientResolver = clientResolver;
        _logger = logger;
        _config = config;
        _admin = admin;
        _consumerName = consumerName;
        _intercept = new OtelConsumerInterceptor.OTelConsumerInterceptor<T>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task InitializeAsync()
    {
        await GetConsumerAsync();
    }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        try
        {
            var messagesToRelay = await NextNormalBatch(ct);

            if (!messagesToRelay.Any())
            {
                await Task.Delay(_config.NoBatchDelay, ct);
                return Array.Empty<MessageContext<T>>();
            }

            _messageIds = messagesToRelay
                .Select(m => m.OriginalMessage.MessageId)
                .ToArray();

            var contexts = messagesToRelay
                .Select(x =>
                    new MessageContext<T>
                    {
                        Data = x.Data,
                        TopicData = PulsarMessageMapper.MapTopicData(
                            x.OriginalMessage.SequenceId.ToString(CultureInfo.InvariantCulture),
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

    private async Task<(T Data, Message<T> OriginalMessage, string Topic)[]> NextNormalBatch(CancellationToken ct)
    {
        var consumer = await GetConsumerAsync();
        var messages = await consumer.BatchReceiveAsync(ct);

        var messageDetails = messages
            .Select(m =>
            (
                Data: m.GetValue(),
                OriginalMessage: m,
                Topic: _config.Topics.Length == 1 ? _config.Topics.First() : m.MessageId.TopicName
            ))
            .ToArray();

        var streamIdsFromMessages = messageDetails
            .Select(c => c.Data.StreamId)
            .Distinct()
            .ToArray();

        var streamIdsInNonNormalState =
            _streamFailureState.FindStreams(streamIdsFromMessages)
            .Select(s => s.StreamId)
            .ToHashSet();

        var messagesToRelay = messageDetails
            .Where(m => !streamIdsInNonNormalState.Contains(m.Data.StreamId))
            .ToArray();

        return messagesToRelay;
    }

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        var results = acks.Select(a => (IsSuccess: true, Message: a))
            .Concat(nacks.Select(n => (IsSuccess: false, Message: n)));
        var resultsByTopicStream = results
            .ToLookup(r => (r.Message.TopicData.Topic, r.Message.Data.StreamId));

        foreach (var topicStreamResults in resultsByTopicStream)
        {
            var firstFailedMessage = topicStreamResults
                .Where(r => !r.IsSuccess)
                .Select(r => r.Message)
                .FirstOrDefault();

            if (firstFailedMessage != null)
            {
                await _streamFailureState.MessageFailed(firstFailedMessage);
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
