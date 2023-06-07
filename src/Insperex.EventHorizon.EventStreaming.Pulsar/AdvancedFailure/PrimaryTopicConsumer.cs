using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
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
    private Dictionary<string, DateTime?> _topicLastMessageTime;

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
        _topicLastMessageTime = _config.Topics.ToDictionary(t => t, _ => (DateTime?)null);
    }

    public async Task InitializeAsync()
    {
        await GetConsumerAsync();
    }

    /// <summary>
    /// Represents the latest publish time of any message read from this topic by this consumer.
    /// </summary>
    public IReadOnlyDictionary<string, DateTime?> TopicLastMessageTime => _topicLastMessageTime;

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

        UpdateTopicLastMessageTimes(messageDetails);

        var topicStreamsFromMessages = messageDetails
            .Select(c => (c.Topic, c.Data.StreamId))
            .ToHashSet();

        var topicStreamState = _streamFailureState
            .FindTopicStreams(topicStreamsFromMessages);

        await ResolveUpToDateStreamTopics(topicStreamState);

        var topicStreamsInNonNormalState = topicStreamState
            .Where(ts => !ts.Topic.IsUpToDate)
            .Select(ts => (ts.Topic.TopicName, ts.StreamId))
            .ToHashSet();

        var messagesToRelay = messageDetails
            .Where(m => !topicStreamsInNonNormalState.Contains((m.Topic, m.Data.StreamId)))
            .ToArray();

        return messagesToRelay;
    }

    private async Task ResolveUpToDateStreamTopics((string StreamId, TopicState Topic)[] topicStreamState)
    {
        var streamsWithUpToDateTopics = topicStreamState
            .Where(ts => ts.Topic.IsUpToDate)
            .GroupBy(ts => ts.StreamId)
            .Select(s => new
            {
                StreamId = s.Key,
                Topics = s.Select(st => st.Topic.TopicName).ToArray()
            })
            .ToArray();

        foreach (var stream in streamsWithUpToDateTopics)
        {
            await _streamFailureState.StreamTopicsResolved(stream.StreamId, stream.Topics);
        }
    }

    private void UpdateTopicLastMessageTimes((T Data, Message<T> OriginalMessage, string Topic)[] messageDetails)
    {
        if (messageDetails.Any())
        {
            var topicLatestMessages = messageDetails
                .GroupBy(m => m.Topic)
                .Select(t => new
                {
                    Topic = t.Key,
                    MaxPublishDate = PulsarMessageMapper.PublishDateFromTimestamp(
                        t.Max(m => m.OriginalMessage.PublishTime))
                })
                .ToArray();

            foreach (var topicLatestMessage in topicLatestMessages)
            {
                if (_topicLastMessageTime.ContainsKey(topicLatestMessage.Topic))
                {
                    _topicLastMessageTime[topicLatestMessage.Topic] = topicLatestMessage.MaxPublishDate;
                }
            }
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
