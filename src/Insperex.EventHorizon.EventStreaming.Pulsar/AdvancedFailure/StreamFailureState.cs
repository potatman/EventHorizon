using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
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
    private readonly RetrySchedule _retrySchedule;

    public StreamFailureState(SubscriptionConfig<T> config, ILogger<StreamFailureState<T>> logger,
        FailureStateTopic<T> failureStateTopic)
    {
        _retrySchedule = config.RetryBackoffPolicy == null
            ? new RetrySchedule()
            : new RetrySchedule(config.RetryBackoffPolicy);
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

    public IEnumerable<TopicStreamState> TopicStreamsForRetry()
    {
        return _failureStateTopic.GetTopicStreams()
            .Where(ts =>
                !ts.IsUpToDate
                && (!ts.NextRetry.HasValue || DateTime.UtcNow >= ts.NextRetry.Value));
    }

    /// <summary>
    /// Searches for given topic/streams and returns their status.
    /// </summary>
    public TopicStreamState[] FindTopicStreams((string Topic, string StreamId)[] topicStreams)
    {
        return _failureStateTopic.FindTopicStreams(topicStreams).ToArray();
    }

    #endregion Queries

    #region Commands

    public async Task TopicStreamUpToDate(string topic, string streamId)
    {
        var state = _failureStateTopic.FindTopicStream((topic, streamId));
        if (state != null)
        {
            state.IsUpToDate = true;
            await _failureStateTopic.Publish(state);
        }
    }

    public async Task TopicStreamResolved(string topic, string streamId)
    {
        var state = _failureStateTopic.FindTopicStream((topic, streamId));
        if (state != null)
        {
            state.IsResolved = true;
            await _failureStateTopic.Publish(state);
        }
    }

    public async Task MessageFailed(MessageContext<T> message)
    {
        _logger.LogInformation($"Msg FAIL: {message.TopicData.Topic} => {message.Data.StreamId} => {message.TopicData.Id}");

        var state = EnsureTopicForStream(message);

        if (state.NextRetry.HasValue) state.TimesRetried++;

        var nextRetryInterval = _retrySchedule.NextInterval(state.TimesRetried);
        state.NextRetry = message.TopicData.CreatedDate.Add(nextRetryInterval);

        await _failureStateTopic.Publish(state);
    }

    public async Task MessageSucceeded(MessageContext<T> message)
    {
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
