using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Insperex.EventHorizon.EventStreaming.Util;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Range = Pulsar.Client.Api.Range;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Reads from the subscription topics (all of them) and merges
/// messages from all topics into one reader stream for the output.
/// </summary>
/// <typeparam name="T">Topic message type.</typeparam>
public class FailedMessageRetryReader<T> where T : class, ITopicMessage, new()
{
    private static readonly TimeSpan MessageCatchupMargin = TimeSpan.FromMilliseconds(1);

    // Dependencies.
    private readonly PulsarClientResolver _clientResolver;
    private readonly ILogger _logger;

    // Inputs
    private readonly StreamState[] _streamsForRetry;
    private readonly int _bufferSize;
    /// <summary>
    /// For each topic, last message time reached by primary consumer.
    /// </summary>
    private readonly IReadOnlyDictionary<string, DateTime?> _topicLastMessageTime;

    /// <summary>
    /// Two-tier lookup, first by topic, then by stream.
    /// </summary>
    private Dictionary<string, Dictionary<string, TopicState>> _topicStreamDict;

    private Dictionary<string, Queue<Message<T>>> _topicQueues;
    private Dictionary<string, bool> _topicContinueReading;
    /// <summary>
    /// Last read message (per topic) in the current reader session.
    /// </summary>
    private Dictionary<string, MessageId> _topicLastReadMessage;

    public FailedMessageRetryReader(
        StreamState[] streamsForRetry, int bufferSize,
        IReadOnlyDictionary<string, DateTime?> topicLastMessageTime,
        PulsarClientResolver clientResolver,
        ILogger logger)
    {
        _streamsForRetry = streamsForRetry;
        _bufferSize = bufferSize;
        _topicLastMessageTime = topicLastMessageTime;
        _clientResolver = clientResolver;
        _logger = logger;

        InitializeDataStructures();
    }

    public async Task<MessageContext<T>[]> GetNextAsync(int batchSize, CancellationToken ct)
    {
        var asOf = DateTime.UtcNow;
        var messages = new List<MessageContext<T>>();
        bool anyMoreMessages;

        do
        {
            var (topic, message) = await GetNextMessage(ct);
            anyMoreMessages = message != null;

            if (anyMoreMessages)
            {
                var data = message.GetValue();
                var isMessageEligible = IsMessageEligible(message, data.StreamId, topic, asOf);

                if (isMessageEligible)
                {
                    _logger.LogInformation($"Reader: got msg: {MsgToStr(message, topic)}");
                    var sequenceId = message.SequenceId.ToString(CultureInfo.InvariantCulture);

                    messages.Add(new MessageContext<T>
                    {
                        Data = data,
                        TopicData = PulsarMessageMapper.MapTopicData(sequenceId, message, topic)
                    });
                }
            }
        } while (messages.Count < batchSize && anyMoreMessages);

        return messages.ToArray();
    }

    private bool IsMessageEligible(Message<T> message, string streamId, string topic, DateTime asOf)
    {
        var topicData = _topicStreamDict[topic].GetValueOrDefault(streamId);

        if (topicData != null)
        {
            return topicData.NextRetry.HasValue
                    // Failure retry mode - would need to reprocess last message since it had failed.
                    ? asOf >= topicData.NextRetry.Value && message.SequenceId >= topicData.LastSequenceId
                    // Recovery mode - last message will have already been successfully processed.
                    : message.SequenceId > topicData.LastSequenceId;
        }

        return false;
    }

    private async Task<(string Topic, Message<T> Message)> GetNextMessage(CancellationToken ct)
    {
        await PrimeTopicQueues(ct);

        var topicQueuesWithMessages =
            _topicQueues.Where(q => q.Value.Any()).ToArray();

        if (!topicQueuesWithMessages.Any()) return (null, null);

        var topic = topicQueuesWithMessages
            .MinBy(q => q.Value.Peek().PublishTime)
            .Key;

        return (topic, _topicQueues[topic].Dequeue());
    }

    private async Task PrimeTopicQueues(CancellationToken ct)
    {
        var capacity = _bufferSize / _topicQueues.Count;

        foreach (var topic in _topicQueues.Keys)
        {
            var lastMessageTime = _topicLastMessageTime.GetValueOrDefault(topic);
            await PrimeTopicQueue(topic, capacity, lastMessageTime, ct);
        }
    }

    private async Task PrimeTopicQueue(string topic, int capacity, DateTime? lastMessageTime, CancellationToken ct)
    {
        if (_topicContinueReading[topic] && !_topicQueues[topic].Any())
        {
            await using var reader = await GetTopicReader(topic);
            bool moreMessages = await reader.HasMessageAvailableAsync();

            while (_topicQueues[topic].Count < capacity && moreMessages)
            {
                var message = await reader.ReadNextAsync(ct);
                _topicLastReadMessage[topic] = message.MessageId;
                var messagePublishTime = PulsarMessageMapper.PublishDateFromTimestamp(message.PublishTime);
                var timeFromLastMessage = messagePublishTime - (lastMessageTime ?? DateTime.UtcNow);

                if (timeFromLastMessage <= MessageCatchupMargin)
                {
                    _topicQueues[topic].Enqueue(message);
                    moreMessages = await reader.HasMessageAvailableAsync();
                }
                else
                {
                    // Reader has passed where main consumer left off. Stop reading.
                    moreMessages = false;
                }
            }

            if (!moreMessages) _topicContinueReading[topic] = false;
        }
    }

    private async Task<IReader<T>> GetTopicReader(string topic)
    {
        var topicStreams = _topicStreamDict[topic];
        var client = await _clientResolver.GetPulsarClientAsync();
        var lastMessageId = _topicLastReadMessage.GetValueOrDefault(topic);

        var reader = await client
            .NewReader(Schema.JSON<T>())
            .Topic(topic)
            .ReaderName($"{NameUtil.AssemblyNameWithGuid}_{topic}")
            .ReceiverQueueSize(1000)
            .StartMessageId(lastMessageId ?? MessageId.Earliest)
            .KeyHashRange(
                topicStreams
                    .Select(s => MurmurHash3.Hash(s.Key) % 65536)
                    .Select(x => new Range(x, x))
                    .ToArray())
            .CreateAsync();

        if (lastMessageId == null)
        {
            // Seek to start timestamp.
            var seekTime = topicStreams
                .Min(s => s.Value.LastMessagePublishTime);
            var seekTimestamp = PulsarMessageMapper.PublishTimestampFromDate(seekTime);
            await reader.SeekAsync(seekTimestamp);
        }

        return reader;
    }

    private void InitializeDataStructures()
    {
        var topicStreamLookup = _streamsForRetry
            .SelectMany(s => s.Topics.Select(t => new { s.StreamId, Topic = t.Value }))
            .ToLookup(st => st.Topic.TopicName);

        _topicQueues = topicStreamLookup
            .ToDictionary(ts => ts.Key, _ => new Queue<Message<T>>());

        _topicStreamDict = topicStreamLookup
            .ToDictionary(
                group => group.Key, // Topic
                group => group.ToDictionary(
                    item => item.StreamId,
                    item => item.Topic));

        _topicContinueReading = topicStreamLookup
            .ToDictionary(ts => ts.Key, _ => true);

        _topicLastReadMessage = new();
    }

    private static string MsgToStr(Message<T> message, string topic)
    {
        var data = message.GetValue();
        return ($"{topic}=>{data.StreamId}=>{message.SequenceId}");
    }
}
