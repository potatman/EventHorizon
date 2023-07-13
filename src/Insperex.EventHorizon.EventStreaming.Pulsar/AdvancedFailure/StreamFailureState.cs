using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Data structure to keep track of which streams have entered a non-normal (failure/recovery) state.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
/// <remarks>
/// The data structure acts as a write-behind cache as well, persisting any state change events in the
/// failure state topic.
///
/// It is not strictly read-behind, though, since it requires explicit call to update itself from the
/// failure state topic.
/// </remarks>
public class StreamFailureState<T> where T : ITopicMessage, new()
{
    private readonly ILogger<StreamFailureState<T>> _logger;
    private readonly FailureStateTopic<T> _failureStateTopic;
    private readonly IBackoffStrategy _backoffStrategy;

    public StreamFailureState(SubscriptionConfig<T> config, ILogger<StreamFailureState<T>> logger,
        FailureStateTopic<T> failureStateTopic)
    {
        _backoffStrategy = config.BackoffStrategy
                           ?? new ExponentialBackoffStrategy() {BaseMs = 10, MaxMs = 10_000,};
        _logger = logger;
        _failureStateTopic = failureStateTopic;
    }

    #region Lifecycle

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _failureStateTopic.InitializeAsync(ct);
    }

    #endregion Lifecycle

    #region Queries

    public TopicStreamState[] TopicStreamsForRetry(DateTime asOf, PulsarKeyHashRanges keyHashRanges, int limit)
    {
        return _failureStateTopic.GetTopicStreams(
            ts =>
                !ts.IsUpToDate
                && (!ts.NextRetry.HasValue || asOf >= ts.NextRetry.Value)
                && keyHashRanges.IsMatch(ts.StreamId),
            limit);
    }

    /// <summary>
    /// Searches for given topic/streams and returns their status.
    /// </summary>
    public TopicStreamState[] FindTopicStreams((string Topic, string StreamId)[] topicStreams)
    {
        return _failureStateTopic.FindTopicStreams(topicStreams);
    }

    #endregion Queries

    #region Commands

    public async Task TopicStreamUpToDate(string topic, string streamId)
    {
        //_logger.LogInformation("Up to date: {topic} => {streamId}", topic, streamId);
        var state = _failureStateTopic.FindTopicStream((topic, streamId));
        if (state != null)
        {
            state.IsUpToDate = true;
            await _failureStateTopic.Publish(state);
        }
    }

    public async Task TopicStreamResolved(string topic, string streamId)
    {
        //_logger.LogInformation("Resolved: {topic} => {streamId}", topic, streamId);
        var state = _failureStateTopic.FindTopicStream((topic, streamId));
        if (state != null)
        {
            state.IsResolved = true;
            await _failureStateTopic.Publish(state);
        }
    }

    public async Task MessageFailed(MessageContext<T> message)
    {
        //_logger.LogInformation("Msg FAIL: {topic} => {streamId} => {id}", message.TopicData.Topic, message.Data.StreamId, message.TopicData.Id);

        var state = EnsureTopicForStream(message);

        if (state.NextRetry.HasValue) state.TimesRetried++;

        var nextRetryInterval = _backoffStrategy.NextInterval(state.TimesRetried);
        state.NextRetry = DateTime.UtcNow.Add(nextRetryInterval);

        await _failureStateTopic.Publish(state);
    }

    public async Task MessageSucceeded(MessageContext<T> message)
    {
        //_logger.LogInformation("Msg SUCCEED: {topic} => {streamId} => {id}", message.TopicData.Topic, message.Data.StreamId, message.TopicData.Id);

        var state = EnsureTopicForStream(message);

        state.TimesRetried = 0;
        state.NextRetry = null;

        await _failureStateTopic.Publish(state);
    }

    private TopicStreamState EnsureTopicForStream(MessageContext<T> message)
    {
        var streamId = message.Data.StreamId;
        var topic = message.TopicData.Topic;
        long.TryParse(message.TopicData.Id, CultureInfo.InvariantCulture, out var sequenceId);
        var messagePublishTime = message.TopicData.CreatedDate;

        var state = _failureStateTopic.FindTopicStream((topic, streamId))
                    ?? new TopicStreamState { Topic = topic, StreamId = streamId, TimesRetried = 0, NextRetry = null };

        state.LastSequenceId = sequenceId;
        state.LastMessagePublishTime = messagePublishTime;

        return state;
    }

    #endregion Commands
}
