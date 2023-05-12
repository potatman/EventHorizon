using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

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
    private enum StreamStatus
    {
        Failed,
        Recovering,
        Normal
    }

    /// <summary>
    /// Record of one stream, so this structure can track its full state.
    /// </summary>
    private sealed class StreamState
    {
        public StreamStatus Status { get; set; }
        public T FailedMessagePayload { get; set; }
        public string FailedMessageTopic { get; set; }
        public DateTime? FailedMessagePublishTime { get; set; }
        public int TimesRetried { get; set; }
        public DateTime? NextRetry { get; set; }
        public DateTime? LastRecoveryMessagePublishTime { get; set; }
    }

    private static readonly StreamState DefaultStreamState =
        new StreamFailureState<T>.StreamState {Status = StreamStatus.Normal};

    private readonly FailureStateTopic<T> _failureStateTopic;
    private readonly RetrySchedule _retrySchedule;
    private readonly Dictionary<string, StreamState> _streams = new();
    private PulsarKeyHashRanges _keyHashRanges;

    public StreamFailureState(SubscriptionConfig<T> config, FailureStateTopic<T> failureStateTopic)
    {
        _retrySchedule = config.RetryBackoffPolicy == null
            ? new RetrySchedule()
            : new RetrySchedule(config.RetryBackoffPolicy);
        _failureStateTopic = failureStateTopic;
    }

    #region Lifecycle

    public async Task InitializeAsync(PulsarKeyHashRanges keyHashRanges, CancellationToken ct)
    {
        if (_keyHashRanges != keyHashRanges)
        {
            // Either first-time initialization scenario or key hash ranges have changed.
            // Either way, we need to reload everything.
            _keyHashRanges = keyHashRanges;
            await _failureStateTopic.InitializeAsync(ct);
            await ReacquireState(ct);
        }
    }

    private async Task ReacquireState(CancellationToken ct)
    {
        _streams.Clear();

        await foreach (var @event in _failureStateTopic.ReadEvents(_keyHashRanges, ct))
        {
            ApplyEventsToLocalStore(@event);
        }
    }

    #endregion Lifecycle

    #region Queries

    private StreamState this[string streamId] => _streams.GetValueOrDefault(streamId, DefaultStreamState);

    /// <summary>
    /// Streams that are in Failed state but eligible for a retry.
    /// </summary>
    /// <returns>Enumerable of relevant stream info.</returns>
    public IEnumerable<(T Data, string Topic, DateTime Published)> FailedStreamsEligibleForRetry()
    {
        return _streams
            .Where(kv => kv.Value.Status == StreamStatus.Failed
                         && kv.Value.NextRetry < DateTime.UtcNow)
            .Select(kv => (
                kv.Value.FailedMessagePayload, kv.Value.FailedMessageTopic,
                kv.Value.FailedMessagePublishTime.GetValueOrDefault(DateTime.UtcNow)));
    }

    /// <summary>
    /// Streams that are in Recovering state.
    /// </summary>
    /// <returns>Enumerable of relevant stream info.</returns>
    public IEnumerable<(string StreamId, DateTime LastRecovered)> StreamsInRecovery()
    {
        return _streams
            .Where(kv => kv.Value.Status == StreamStatus.Recovering)
            .Select(kv => (
                kv.Key,
                kv.Value.LastRecoveryMessagePublishTime.GetValueOrDefault(DateTime.UtcNow)));
    }

    /// <summary>
    /// Returns whether stream is marked to be in Normal state.
    /// </summary>
    public bool IsStreamInNormalState(string streamId)
    {
        if (_streams.TryGetValue(streamId, out var streamState))
        {
            return streamState.Status == StreamStatus.Normal;
        }

        return true;
    }

    #endregion Queries

    #region Commands

    public async Task MessageProcessingFailed(params MessageContext<T>[] messages)
    {
        var events = messages
            .Select(message =>
            {
                var streamState = this[message.Data.StreamId];
                var timesRetried = streamState.Status == StreamStatus.Failed ? streamState.TimesRetried + 1 : 0;
                var nextRetryInterval = _retrySchedule.NextInterval(timesRetried);

                return new StreamFailureStateEvent<T>
                {
                    StreamId = message.Data.StreamId,
                    EventType = StreamFailureStateEventType.MessageProcessingFailed,
                    FailedMessagePayload = message.Data,
                    FailedMessageTopic = message.TopicData.Topic,
                    FailedMessagePublishTime = message.TopicData.CreatedDate,
                    TimesRetried = timesRetried,
                    NextRetry = message.TopicData.CreatedDate.Add(nextRetryInterval),
                };
            })
            .ToArray();

        ApplyEventsToLocalStore(events);
        await _failureStateTopic.PublishEvents(events);
    }

    public async Task FailedMessageRetrySucceeded(params MessageContext<T>[] messages)
    {
        var events = messages
            .Select(message => new StreamFailureStateEvent<T>
            {
                StreamId = message.Data.StreamId,
                EventType = StreamFailureStateEventType.MessageProcessingSucceeded,
            })
            .ToArray();

        ApplyEventsToLocalStore(events);
        await _failureStateTopic.PublishEvents(events);
    }

    public async Task RecoveryMessagesProcessed(string streamId, DateTime recoveredUpThroughDate)
    {
        var evt = new StreamFailureStateEvent<T>
        {
            StreamId = streamId,
            EventType = StreamFailureStateEventType.RecoveryMessagesProcessed,
            LastRecoveryMessagePublishTime = recoveredUpThroughDate,
        };

        ApplyEventsToLocalStore(evt);
        await _failureStateTopic.PublishEvents(evt);
    }

    public async Task StreamRecoveryCompleted(params string[] streamIds)
    {
        var events = streamIds
            .Select(streamId => new StreamFailureStateEvent<T>
            {
                StreamId = streamId,
                EventType = StreamFailureStateEventType.RecoveryComplete,
            })
            .ToArray();

        ApplyEventsToLocalStore(events);
        await _failureStateTopic.PublishEvents(events);
    }

    #endregion Commands

    #region Applying events

    private void ApplyEventsToLocalStore(params StreamFailureStateEvent<T>[] events)
    {
        foreach (var evt in events)
        {
            var streamState = this[evt.StreamId];

            ApplyEvent(evt, streamState);

            if (streamState.Status == StreamStatus.Normal)
            {
                _streams.Remove(evt.StreamId);
            }
            else
            {
                _streams[evt.StreamId] = streamState;
            }
        }
    }

    private static void ApplyEvent(StreamFailureStateEvent<T> evt, StreamState stream)
    {
        switch (evt.EventType)
        {
            case StreamFailureStateEventType.MessageProcessingFailed:
                stream.Status = StreamStatus.Failed;
                stream.FailedMessagePayload = evt.FailedMessagePayload;
                stream.FailedMessageTopic = evt.FailedMessageTopic;
                stream.FailedMessagePublishTime = evt.FailedMessagePublishTime;
                stream.TimesRetried = evt.TimesRetried ?? stream.TimesRetried + 1;
                stream.NextRetry = evt.NextRetry ?? DateTime.UtcNow;
                break;
            case StreamFailureStateEventType.MessageProcessingSucceeded:
                stream.Status = StreamStatus.Recovering;
                stream.FailedMessagePayload = default(T);
                stream.FailedMessageTopic = null;
                stream.FailedMessagePublishTime = null;
                stream.TimesRetried = 0;
                stream.NextRetry = null;
                break;
            case StreamFailureStateEventType.RecoveryMessagesProcessed:
                stream.Status = StreamStatus.Recovering;
                stream.LastRecoveryMessagePublishTime = evt.LastRecoveryMessagePublishTime;
                break;
            case StreamFailureStateEventType.RecoveryComplete:
                stream.Status = StreamStatus.Normal;
                break;
        }
    }

    #endregion Applying events
}
