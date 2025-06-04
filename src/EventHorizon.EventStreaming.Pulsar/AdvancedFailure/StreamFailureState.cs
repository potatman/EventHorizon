using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.Pulsar.Models;
using EventHorizon.EventStreaming.Subscriptions;
using EventHorizon.EventStreaming.Subscriptions.Backoff;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

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
    private PulsarKeyHashRanges _keyHashRanges;

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

    #region State

    private bool _detectedZeroTrackedTopicStreams;
    private int _trackedTopicStreamsGuess;

    /// <summary>
    /// We may not need to query the failure state topic at times. #optimization
    /// </summary>
    private bool ShortCircuitFailureStateTopic => _trackedTopicStreamsGuess == 0 && _detectedZeroTrackedTopicStreams;

    public PulsarKeyHashRanges KeyHashRanges
    {
        get => _keyHashRanges;
        set
        {
            _keyHashRanges = value;
            // Reset all volatile state tracking flags.
            _detectedZeroTrackedTopicStreams = false;
            _trackedTopicStreamsGuess = 0;
        }
}

    #endregion State

    #region Queries

    public TopicStreamState[] TopicStreamsForRetry(DateTime asOf, int limit)
    {
        if (ShortCircuitFailureStateTopic) return Array.Empty<TopicStreamState>();

        var (topicStreams, totalTrackedTopicStreams) =
            _failureStateTopic.GetTopicStreams(
                KeyHashRanges,
                ts =>
                    !ts.IsUpToDate
                    && (!ts.NextRetry.HasValue || asOf >= ts.NextRetry.Value),
                limit);

        _detectedZeroTrackedTopicStreams = totalTrackedTopicStreams == 0;
        _trackedTopicStreamsGuess = Math.Max(_trackedTopicStreamsGuess, totalTrackedTopicStreams);

        return topicStreams;
    }

    /// <summary>
    /// Searches for given topic/streams and returns their status.
    /// </summary>
    public TopicStreamState[] FindTopicStreams((string Topic, string StreamId)[] topicStreams)
    {
        return ShortCircuitFailureStateTopic
            ? Array.Empty<TopicStreamState>()
            : _failureStateTopic.FindTopicStreams(topicStreams);
    }

    #endregion Queries

    #region Commands

    public async Task TopicStreamUpToDate(string topic, string streamId)
    {
        //_logger.LogInformation("Up to date: {topic} => {streamId}", topic, streamId);
        _detectedZeroTrackedTopicStreams = false;
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
        _trackedTopicStreamsGuess = Math.Max(0, _trackedTopicStreamsGuess - 1);
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

        _trackedTopicStreamsGuess++;
        _detectedZeroTrackedTopicStreams = false;
        var state = EnsureTopicForStream(message);

        if (state.NextRetry.HasValue) state.TimesRetried++;

        var nextRetryInterval = _backoffStrategy.NextInterval(state.TimesRetried);
        state.NextRetry = DateTime.UtcNow.Add(nextRetryInterval);

        await _failureStateTopic.Publish(state);
    }

    public async Task MessageSucceeded(MessageContext<T> message)
    {
        //_logger.LogInformation("Msg SUCCEED: {topic} => {streamId} => {id}", message.TopicData.Topic, message.Data.StreamId, message.TopicData.Id);

        _detectedZeroTrackedTopicStreams = false;
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
