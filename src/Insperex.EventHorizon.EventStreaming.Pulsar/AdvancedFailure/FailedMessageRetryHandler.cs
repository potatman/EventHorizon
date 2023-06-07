using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Handles the "failure retry" phase of the main cycle in <see cref="OrderGuaranteedPulsarTopicConsumer{T}"/>,
/// in which we retry any failed messages in an attempt to get the message's stream out of failure state.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class FailedMessageRetryHandler<T>: ITopicConsumer<T> where T : class, ITopicMessage, new()
{
    private readonly StreamFailureState<T> _streamFailureState;
    private readonly PulsarClientResolver _clientResolver;
    private readonly ILogger<FailedMessageRetryHandler<T>> _logger;
    private readonly int _batchSize;

    public FailedMessageRetryHandler(SubscriptionConfig<T> config, StreamFailureState<T> streamFailureState,
        PulsarClientResolver clientResolver, ILogger<FailedMessageRetryHandler<T>> logger)
    {
        _batchSize = config.BatchSize ?? 1000;
        _streamFailureState = streamFailureState;
        _clientResolver = clientResolver;
        _logger = logger;
    }

    public PulsarKeyHashRanges KeyHashRanges { get; set; }

    /// <summary>
    /// This is originally tracked in the <see cref="PrimaryTopicConsumer{T}"/> and passed in here./>
    /// </summary>
    public IReadOnlyDictionary<string, DateTime?> TopicLastMessageTime { get; set; }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        _logger.LogInformation("Next batch start");

        var streamsForRetry = _streamFailureState.StreamsForRetry()
            .Where(s => KeyHashRanges.IsMatch(s.StreamId))
            .Take(MaxStreams)
            .ToArray();

        _logger.LogInformation($"Streams for retry: {streamsForRetry.Length}");
        if (streamsForRetry.Length == 0) return Array.Empty<MessageContext<T>>();

        var reader = new FailedMessageRetryReader<T>(streamsForRetry, _batchSize, TopicLastMessageTime,
            _clientResolver, _logger);

        try
        {
            var messages = await reader.GetNextAsync(_batchSize, ct);

            if (messages.Length < _batchSize)
            {
                // If we didn't get a full batch, that may mean some streams have been fully resolved.
                // Check if any streams didn't get any messages (and aren't still expecting a retry at some stage).

                await MarkTopicStreamsUpToDate(messages, streamsForRetry);
            }

            _logger.LogInformation("Next batch end");

            return messages;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in NextBatchAsync");
            throw;
        }
    }

    private async Task MarkTopicStreamsUpToDate(MessageContext<T>[] messages, StreamState[] streamsForRetry)
    {
        var messageStreamTopics = messages
            .Select(m => (m.Data.StreamId, m.TopicData.Topic))
            .ToHashSet();

        var resolvedStreamTopics = streamsForRetry
            .SelectMany(s => s.Topics.Select(t => new
            {
                TopicName = t.Key, s.StreamId, Topic = t.Value
            }))
            .Where(st => !st.Topic.NextRetry.HasValue)
            .Where(st => !messageStreamTopics.Contains((st.StreamId, st.TopicName)))
            .GroupBy(st => st.StreamId)
            .Select(st => new
            {
                StreamId = st.Key, Topics = st.Select(t => t.TopicName).ToArray()
            })
            .ToArray();

        foreach (var resolvedTopicStream in resolvedStreamTopics)
        {
            await _streamFailureState.StreamTopicsUpToDate(resolvedTopicStream.StreamId, resolvedTopicStream.Topics);
        }
    }

    private int MaxStreams => _batchSize / 3;

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        _logger.LogInformation("Finalize batch start");

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
            else
            {
                var lastMessage = topicStreamResults.Last().Message;
                await _streamFailureState.MessageSucceeded(lastMessage);
            }
        }

        _logger.LogInformation("Next batch end");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
