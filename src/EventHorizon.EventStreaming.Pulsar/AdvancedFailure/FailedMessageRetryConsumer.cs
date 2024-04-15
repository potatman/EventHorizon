using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;

namespace EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Handles the "failure retry" phase of the main cycle in <see cref="OrderGuaranteedPulsarTopicConsumer{T}"/>,
/// in which we retry any failed messages in an attempt to get the message's stream out of failure state.
/// </summary>
/// <typeparam name="TMessage">Type of message from the primary topic.</typeparam>
public class FailedMessageRetryConsumer<TMessage>: ITopicConsumer<TMessage>
    where TMessage : ITopicMessage
{
    private const int MaxStreams = 300;

    private readonly StreamFailureState<TMessage> _streamFailureState;
    private readonly RetryTopicReader<TMessage> _reader;
    private readonly ILogger<FailedMessageRetryConsumer<TMessage>> _logger;
    private readonly int _batchSize;

    public FailedMessageRetryConsumer(SubscriptionConfig<TMessage> config, StreamFailureState<TMessage> streamFailureState,
        PulsarClient pulsarClient, ILoggerFactory loggerFactory)
    {
        _batchSize = config.BatchSize ?? 1000;
        _streamFailureState = streamFailureState;
        _reader = new RetryTopicReader<TMessage>(pulsarClient, config, loggerFactory.CreateLogger<RetryTopicReader<TMessage>>());
        _logger = loggerFactory.CreateLogger<FailedMessageRetryConsumer<TMessage>>();
    }

    public Task InitAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<MessageContext<TMessage>[]> NextBatchAsync(CancellationToken ct)
    {
        var topicStreamsForRetry =
            _streamFailureState.TopicStreamsForRetry(DateTime.UtcNow, MaxStreams);

        //_logger.LogInformation($"Topic/streams for retry: {topicStreamsForRetry.Length}");
        if (topicStreamsForRetry.Length == 0) return Array.Empty<MessageContext<TMessage>>();

        try
        {
            var messages = await _reader.GetNextAsync(topicStreamsForRetry, _batchSize, ct).ConfigureAwait(false);

            if (messages.Length < _batchSize)
            {
                // If we didn't get a full batch, that may mean some streams have been fully resolved.
                // Check if any streams didn't get any messages (and aren't still expecting a retry at some stage).

                //_logger.LogInformation("Some topic/streams might be up to date");
                await MarkQuietTopicStreamsUpToDateAsync(messages, topicStreamsForRetry).ConfigureAwait(false);
            }

            return messages;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in NextBatchAsync");
            throw;
        }
    }

    private async Task MarkQuietTopicStreamsUpToDateAsync(MessageContext<TMessage>[] messages, TopicStreamState[] topicStreamsForRetry)
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
            await _streamFailureState.TopicStreamUpToDateAsync(topicStream.Topic, topicStream.StreamId).ConfigureAwait(false);
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
            else
            {
                var lastMessage = topicStreamResults.Last().Message;
                await _streamFailureState.MessageSucceededAsync(lastMessage).ConfigureAwait(false);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
