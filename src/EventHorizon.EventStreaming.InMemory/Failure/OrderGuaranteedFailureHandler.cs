using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.InMemory.Databases;
using EventHorizon.EventStreaming.Subscriptions;
using EventHorizon.EventStreaming.Subscriptions.Backoff;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming.InMemory.Failure;

public class OrderGuaranteedFailureHandler<TMessage>: IFailureHandler<TMessage>
    where TMessage : ITopicMessage
{
    private readonly SubscriptionConfig<TMessage> _config;
    private readonly MessageDatabase _messageDatabase;
    private readonly ILogger<OrderGuaranteedFailureHandler<TMessage>> _logger;
    private readonly IBackoffStrategy _backoffStrategy;

    private sealed class TopicStreamState
    {
        public long NextIndex { get; set; }
        public int TimesRetried { get; set; }
        public DateTime? NextRetry { get; set; }
    }

    /// <summary>
    /// Look up by (topicName, streamId).
    /// </summary>
    private readonly Dictionary<(string Topic, string StreamId), TopicStreamState> _topicStreams = new();

    private readonly Dictionary<string, long> _maxIndexByTopic;

    public OrderGuaranteedFailureHandler(SubscriptionConfig<TMessage> config, MessageDatabase messageDatabase,
        ILogger<OrderGuaranteedFailureHandler<TMessage>> logger)
    {
        _config = config;
        _messageDatabase = messageDatabase;
        _logger = logger;

        _backoffStrategy = _config.BackoffStrategy
            ?? new ExponentialBackoffStrategy() {BaseMs = 10, MaxMs = 10_000,};

        _maxIndexByTopic = _config.Topics.ToDictionary(t => t, _ => default(long));
    }

    public bool InNormalMode(string topic, string streamId) => !_topicStreams.ContainsKey((topic, streamId));

    public MessageContext<TMessage>[] GetMessagesForRetry(int capacity)
    {
        var asOf = DateTime.UtcNow;

        var eligibleTopicStreams = _topicStreams
            .Where(ts =>
                !ts.Value.NextRetry.HasValue || ts.Value.NextRetry.Value <= asOf)
            .ToArray();

        var messages = PullNextMessages(capacity, eligibleTopicStreams);

        // If we couldn't pull full capacity, we can safely bring any streams out of failure/recovery mode that
        // don't have any messages in the batch we could pull.
        if (messages.Count < capacity)
        {
            ClearTopicStreams(messages, eligibleTopicStreams);
        }

        return messages.ToArray();
    }

    public void FinalizeBatch(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks,
        Dictionary<string, long> maxIndexByTopic)
    {
        // Update local store of max index reached by topic. This info is used to prevent
        // pulling "future" messages during the recovery phase.
        foreach (var topicMaxIndex in maxIndexByTopic)
        {
            _maxIndexByTopic[topicMaxIndex.Key] = topicMaxIndex.Value;
        }

        var results = acks.Select(a => (IsSuccess: true, Message: a))
            .Concat(nacks.Select(n => (IsSuccess: false, Message: n)));
        var resultsByTopicStream = results
            .ToLookup(r => (r.Message.TopicData.Topic, r.Message.Data.StreamId));

        foreach (var topicStreamResult in resultsByTopicStream)
        {
            var firstFailedMessage = topicStreamResult
                .Where(r => !r.IsSuccess)
                .Select(r => r.Message)
                .FirstOrDefault();

            if (firstFailedMessage != null)
            {
                MessageFailed(firstFailedMessage);
            }
            else
            {
                // Mark as recovering.
                var lastMessage = topicStreamResult.Last().Message;
                MessageSucceeded(lastMessage);
            }
        }
    }

    private void MessageFailed(MessageContext<TMessage> message)
    {
        var topic = message.TopicData.Topic;
        var streamId = message.Data.StreamId;
        var state = _topicStreams.GetValueOrDefault((topic, streamId));

        if (state == null)
        {
            _topicStreams.Add((topic, streamId), new());
            state = _topicStreams[(topic, streamId)];
        }
        else
        {
            state.TimesRetried++;
        }

        // In failed mode - set up for next retry.
        state.NextIndex = long.Parse(message.TopicData.Id, CultureInfo.InvariantCulture);
        var nextRetryInterval = _backoffStrategy.NextInterval(state.TimesRetried);
        state.NextRetry = DateTime.UtcNow.Add(nextRetryInterval);
    }

    private void MessageSucceeded(MessageContext<TMessage> message)
    {
        var topic = message.TopicData.Topic;
        var streamId = message.Data.StreamId;
        var state = _topicStreams.GetValueOrDefault((topic, streamId));

        if (state != null)
        {
            state.NextRetry = null;
            state.NextIndex = long.Parse(message.TopicData.Id, CultureInfo.InvariantCulture) + 1;
            state.TimesRetried = 0;
        }
    }

    private List<MessageContext<TMessage>> PullNextMessages(int capacity, KeyValuePair<(string Topic, string StreamId), TopicStreamState>[] eligibleTopicStreams)
    {
        List<MessageContext<TMessage>> list = new();

        foreach (var topic in _config.Topics)
        {
            if (list.Count >= capacity) break;

            var streams = eligibleTopicStreams
                .Where(ts => ts.Key.Topic == topic)
                .Select(ts => new {ts.Key.StreamId, ts.Value.NextIndex})
                .ToArray();

            if (streams.Any())
            {
                var streamIds = streams.Select(s => s.StreamId).ToArray();
                var startIndex = streams.Min(s => s.NextIndex);

                var messages = _messageDatabase
                    .GetMessages<TMessage>(topic, streamIds, (int) startIndex)
                    .Select(m => new
                    {
                        Message = m,
                        Index = long.Parse(m.TopicData.Id, CultureInfo.InvariantCulture)
                    })
                    .Where(m =>
                        m.Index >= _topicStreams[(topic, m.Message.Data.StreamId)].NextIndex
                        && m.Index <= _maxIndexByTopic[topic])
                    .Take(capacity - list.Count)
                    .Select(m => m.Message)
                    .ToArray();

                list.AddRange(messages);
            }
        }

        return list;
    }

    /// <summary>
    /// Remove topic/stream pairs from internal tracking data structure which are deemed to no longer
    /// be in need of retry or recovery (i.e. back in normal mode.)
    /// </summary>
    /// <param name="extractedMessages">Messages we've extracted from database.</param>
    /// <param name="topicStreamsInScope">Topic/streams in scope for this extractions.</param>
    private void ClearTopicStreams(List<MessageContext<TMessage>> extractedMessages,
        KeyValuePair<(string Topic, string StreamId), TopicStreamState>[] topicStreamsInScope)
    {
        var messageTopicStreams = extractedMessages
            .Select(m => (m.TopicData.Topic, m.Data.StreamId))
            .ToHashSet();

        var topicStreamsToClear = topicStreamsInScope
            .Where(ts => !messageTopicStreams.Contains(ts.Key))
            .Select(ts => ts.Key)
            .ToArray();

        foreach (var topicStream in topicStreamsToClear)
        {
            _topicStreams.Remove(topicStream);
        }
    }
}
