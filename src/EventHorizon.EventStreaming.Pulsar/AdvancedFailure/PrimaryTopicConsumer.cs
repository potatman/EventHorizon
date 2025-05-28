﻿using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Pulsar.Extensions;
using EventHorizon.EventStreaming.Pulsar.Models;
using EventHorizon.EventStreaming.Pulsar.Utils;
using EventHorizon.EventStreaming.Subscriptions;
using EventHorizon.EventStreaming.Tracing;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;

namespace EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

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
    private PulsarKeyHashRanges _keyHashRanges;
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
        this.KeyHashRangeOutlierFound = false;
    }

    public PulsarKeyHashRanges KeyHashRanges
    {
        set
        {
            _keyHashRanges = value;
            KeyHashRangeOutlierFound = false; // Reset the flag.
        }
    }

    public bool KeyHashRangeOutlierFound { get; private set; }

    public async Task InitAsync()
    {
        await GetConsumerAsync();
    }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        try
        {
            var messagesToRelay = await NextNormalBatch(ct);

            //_logger.LogInformation("Normal batch: {messageCount} messages", messagesToRelay.Length);

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

        var topicStreamsFromMessages = messageDetails
            .Select(c => (c.Topic, c.Data.StreamId))
            .Distinct()
            .ToArray();

        var topicStreamState = _streamFailureState
            .FindTopicStreams(topicStreamsFromMessages)
            .ToDictionary(ts => (ts.Topic, ts.StreamId));

        var messagesToRelay = messageDetails
            .Where(m =>
                // Topic/stream not in failure or recovery state
                !topicStreamState.ContainsKey((m.Topic, m.Data.StreamId))
                || (
                    // It's finished recovery and we've passed the point where recovery reader reached.
                    topicStreamState[(m.Topic, m.Data.StreamId)].IsUpToDate
                    && topicStreamState[(m.Topic, m.Data.StreamId)].LastSequenceId < m.OriginalMessage.SequenceId
                ))
            .ToArray();

        CheckForStreamIdsOutsideKeyHashRange(messagesToRelay);

        var topicStreamsToResolve = messagesToRelay
            .GroupBy(m => (m.Data.StreamId, m.Topic))
            .Where(ts =>
                topicStreamState.ContainsKey((ts.Key.Topic, ts.Key.StreamId)))
            .Select(ts => ts.Key)
            .ToArray();

        await ResolveUpToDateTopicStreams(topicStreamsToResolve);

        return messagesToRelay;
    }

    private void CheckForStreamIdsOutsideKeyHashRange((T Data, Message<T> OriginalMessage, string Topic)[] messagesToRelay)
    {
        if (!KeyHashRangeOutlierFound && _keyHashRanges != null)
        {
            if (messagesToRelay.Any(m => !_keyHashRanges.IsMatch(m.Data.StreamId)))
            {
                KeyHashRangeOutlierFound = true;
            }
        }
    }

    private async Task ResolveUpToDateTopicStreams((string Topic, string StreamId)[] topicStreams)
    {
        foreach (var topicStream in topicStreams)
        {
            await _streamFailureState.TopicStreamResolved(topicStream.Topic, topicStream.StreamId);
        }
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
            .SubscriptionType(_config.SubscriptionType.ToPulsarSubscriptionType())
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
