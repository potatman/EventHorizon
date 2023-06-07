using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
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

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        var topicStreamsForRetry = _streamFailureState.TopicStreamsForRetry()
            .Where(ts => KeyHashRanges.IsMatch(ts.StreamId))
            .Take(MaxStreams)
            .ToArray();

        _logger.LogInformation($"Topic/streams for retry: {topicStreamsForRetry.Length}");
        if (topicStreamsForRetry.Length == 0) return Array.Empty<MessageContext<T>>();

        var reader = new FailedMessageRetryReader<T>(topicStreamsForRetry, _batchSize, _clientResolver, _logger);

        try
        {
            var messages = await reader.GetNextAsync(_batchSize, ct);

            if (messages.Length < _batchSize)
            {
                // If we didn't get a full batch, that may mean some streams have been fully resolved.
                // Check if any streams didn't get any messages (and aren't still expecting a retry at some stage).

                _logger.LogInformation("Some topic/streams might be up to date");
                await MarkTopicStreamsUpToDate(messages, topicStreamsForRetry);
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

    private async Task MarkTopicStreamsUpToDate(MessageContext<T>[] messages, TopicStreamState[] topicStreamsForRetry)
    {
        var messageTopicStreams = messages
            .Select(m => (m.TopicData.Topic, m.Data.StreamId))
            .ToHashSet();

        var upToDateTopicStreams = topicStreamsForRetry
            .Where(ts =>
                !ts.NextRetry.HasValue
                && !messageTopicStreams.Contains((ts.Topic, ts.StreamId)))
            .ToArray();

        foreach (var topicStream in upToDateTopicStreams)
        {
            await _streamFailureState.TopicStreamUpToDate(topicStream.Topic, topicStream.StreamId);
        }
    }

    private int MaxStreams => _batchSize / 3;

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        _logger.LogInformation("Finalize retry/recovery batch start");

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

        _logger.LogInformation("Finalize retry/recovery batch end");
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
