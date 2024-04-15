using System;
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
/// <typeparam name="TMessage">Type of message from the primary topic.</typeparam>
internal sealed class PrimaryTopicConsumer<TMessage>: ITopicConsumer<TMessage>
    where TMessage : ITopicMessage
{
    private readonly StreamFailureState<TMessage> _streamFailureState;
    private readonly PulsarClient _pulsarClient;
    private readonly ILogger<PrimaryTopicConsumer<TMessage>> _logger;
    private readonly SubscriptionConfig<TMessage> _config;
    private readonly ITopicAdmin<TMessage> _admin;
    private readonly string _consumerName;
    private readonly OtelConsumerInterceptor.OTelConsumerInterceptor<TMessage> _intercept;
    private PulsarKeyHashRanges _keyHashRanges;
    private MessageId[] _messageIds;
    private IConsumer<TMessage> _consumer;

    public PrimaryTopicConsumer(
        StreamFailureState<TMessage> streamFailureState,
        PulsarClient pulsarClient,
        ILogger<PrimaryTopicConsumer<TMessage>> logger,
        SubscriptionConfig<TMessage> config,
        ITopicAdmin<TMessage> admin,
        string consumerName)
    {
        _streamFailureState = streamFailureState;
        _pulsarClient = pulsarClient;
        _logger = logger;
        _config = config;
        _admin = admin;
        _consumerName = consumerName;
        _intercept = new OtelConsumerInterceptor.OTelConsumerInterceptor<TMessage>(
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

    public Task InitAsync()
    {
        return GetConsumerAsync();
    }

    public async Task<MessageContext<TMessage>[]> NextBatchAsync(CancellationToken ct)
    {
        try
        {
            var messagesToRelay = await NextNormalBatch(ct).ConfigureAwait(false);

            //_logger.LogInformation("Normal batch: {messageCount} messages", messagesToRelay.Length);

            if (!messagesToRelay.Any())
            {
                await Task.Delay(_config.NoBatchDelay, ct).ConfigureAwait(false);
                return Array.Empty<MessageContext<TMessage>>();
            }

            _messageIds = messagesToRelay
                .Select(m => m.OriginalMessage.MessageId)
                .ToArray();

            var contexts = messagesToRelay
                .Select(x =>
                    new MessageContext<TMessage>(x.Data, PulsarMessageMapper.MapTopicData(
                        x.OriginalMessage.SequenceId.ToString(CultureInfo.InvariantCulture),
                        x.OriginalMessage, x.Topic), _config.TypeDict)
                )
                .ToArray();

            return contexts;
        }
        catch (AlreadyClosedException)
        {
            // Ignore AlreadyClosedException
            return null;
        }
    }

    private async Task<(TMessage Data, Message<TMessage> OriginalMessage, string Topic)[]> NextNormalBatch(CancellationToken ct)
    {
        var consumer = await GetConsumerAsync().ConfigureAwait(false);
        var messages = await consumer.BatchReceiveAsync(ct).ConfigureAwait(false);

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

        await ResolveUpToDateTopicStreamsAsync(topicStreamsToResolve).ConfigureAwait(false);

        return messagesToRelay;
    }

    private void CheckForStreamIdsOutsideKeyHashRange((TMessage Data, Message<TMessage> OriginalMessage, string Topic)[] messagesToRelay)
    {
        if (!KeyHashRangeOutlierFound && _keyHashRanges != null)
        {
            if (messagesToRelay.Any(m => !_keyHashRanges.IsMatch(m.Data.StreamId)))
            {
                KeyHashRangeOutlierFound = true;
            }
        }
    }

    private async Task ResolveUpToDateTopicStreamsAsync((string Topic, string StreamId)[] topicStreams)
    {
        foreach (var topicStream in topicStreams)
        {
            await _streamFailureState.TopicStreamResolvedAsync(topicStream.Topic, topicStream.StreamId).ConfigureAwait(false);
        }
    }

    public async Task FinalizeBatchAsync(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks)
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
                await _streamFailureState.MessageFailedAsync(firstFailedMessage).ConfigureAwait(false);
            }
        }

        await AckToPulsarAsync().ConfigureAwait(false);
    }

    private async Task AckToPulsarAsync()
    {
        var consumer = await GetConsumerAsync().ConfigureAwait(false);
        foreach (var messageId in _messageIds)
            await consumer.AcknowledgeAsync(messageId).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if(_consumer != null) await _consumer.DisposeAsync().ConfigureAwait(false);
        _consumer = null;
    }

    private async Task<IConsumer<TMessage>> GetConsumerAsync()
    {
        if (_consumer != null)
            return _consumer;

        // Ensure Topic Exists
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var topic in _config.Topics)
            await _admin.RequireTopicAsync(topic, cts.Token).ConfigureAwait(false);

        var builder = _pulsarClient.NewConsumer(Schema.JSON<TMessage>())
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

        var consumer = await builder.SubscribeAsync().ConfigureAwait(false);

        if (_config.StartDateTime != null)
            await consumer.SeekAsync(_config.StartDateTime.Value.Ticks).ConfigureAwait(false);

        // Return
        return _consumer = consumer;
    }
}
