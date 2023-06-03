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
public class FailedMessageRetryReader<T>: IAsyncDisposable where T : class, ITopicMessage, new()
{
    private const int ReaderBatchSize = 5;

    // Dependencies.
    private readonly PulsarClientResolver _clientResolver;
    private readonly ITopicAdmin<T> _admin;
    private readonly ILogger _logger;

    // Inputs
    private readonly StreamState[] _streamsForRetry;

    // Readers and read state.
    private IReader<T>[] _readers;
    private bool[] _isReaderDone;
    private Queue<Message<T>>[] _readerQueues;
    private string[] _topicNames;
    /// <summary>
    /// Two-tier lookup, first by topic, then by stream.
    /// </summary>
    private Dictionary<string, Dictionary<string, TopicState>> _topicStreamDict;

    public FailedMessageRetryReader(
        StreamState[] streamsForRetry,
        PulsarClientResolver clientResolver,
        ITopicAdmin<T> admin, ILogger logger)
    {
        _streamsForRetry = streamsForRetry;
        _clientResolver = clientResolver;
        _admin = admin;
        _logger = logger;
    }

    public async Task<MessageContext<T>[]> GetNextAsync(int batchSize, CancellationToken ct)
    {
        await EnsureTopicReaders(ct);

        if (_readers.Length == 0) return Array.Empty<MessageContext<T>>();

        var asOf = DateTime.UtcNow;
        var messages = new List<MessageContext<T>>();
        bool anyMoreMessages = false;

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
        await PrimeReaderQueues(ct);

        var index = _readerQueues
            .Select((q, i) => new { Index = i, Queue = q, })
            .Where(q => q.Queue.Any())
            .MinBy(q => q.Queue.Peek().PublishTime)
            ?.Index;

        if (!index.HasValue) return (null, null);

        return (_topicNames[index.Value], _readerQueues[index.Value].Dequeue());
    }

    private async Task PrimeReaderQueues(CancellationToken ct)
    {
        await Task.WhenAll(_readers.Select((r, i) => PrimeReaderQueue(i, ct)));
    }

    private async Task PrimeReaderQueue(int i, CancellationToken ct)
    {
        if (!_readerQueues[i].Any() && !_isReaderDone[i])
        {
            for (var m = 0; m < ReaderBatchSize; m++)
            {
                var available = await _readers[i].HasMessageAvailableAsync();
                if (!available) break;

                var message = await _readers[i].ReadNextAsync(ct);

                _readerQueues[i].Enqueue(message);
            }

            if (!_readerQueues[i].Any()) _isReaderDone[i] = true;
        }
    }

    private async Task EnsureTopicReaders(CancellationToken ct)
    {
        if (_readers != null) return;

        var topicStreamLookup = _streamsForRetry
            .SelectMany(s => s.Topics.Select(t => new { s.StreamId, Topic = t.Value }))
            .ToLookup(st => st.Topic.TopicName);

        _isReaderDone = topicStreamLookup.Select(_ => false).ToArray();
        _readerQueues = topicStreamLookup.Select(_ => new Queue<Message<T>>()).ToArray();
        _topicNames = topicStreamLookup.Select(st => st.Key).ToArray();

        _topicStreamDict = topicStreamLookup
            .ToDictionary(
                group => group.Key, // Topic
                group => group.ToDictionary(
                    item => item.StreamId,
                    item => item.Topic));

        foreach (var topic in topicStreamLookup.Select(st => st.Key))
        {
            await _admin.RequireTopicAsync(topic, ct);
        }

        var client = await _clientResolver.GetPulsarClientAsync();

        _readers = await Task.WhenAll(
            topicStreamLookup
                .Select(streamsPerTopic => client
                    .NewReader(Schema.JSON<T>())
                    .Topic(streamsPerTopic.Key)
                    .ReaderName($"{NameUtil.AssemblyNameWithGuid}_{streamsPerTopic.Key}")
                    .ReceiverQueueSize(1000)
                    .StartMessageId(MessageId.Earliest)
                    .KeyHashRange(
                        streamsPerTopic
                            .Select(s => MurmurHash3.Hash(s.StreamId) % 65536)
                            .Select(x => new Range(x, x))
                            .ToArray())
                    .CreateAsync())
                .ToArray());

        // Seek to start timestamp.
        for (var i = 0; i < _readers.Length; i++)
        {
            var seekTime = _topicStreamDict[_topicNames[i]]
                .Min(s => s.Value.LastMessagePublishTime);
            var seekTimestamp = PulsarMessageMapper.PublishTimestampFromDate(seekTime);
            await _readers[i].SeekAsync(seekTimestamp);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_readers != null)
        {
            foreach (var reader in _readers)
            {
                if (reader != null) await reader.DisposeAsync();
            }

            _readers = null;
        }

        _readerQueues = null;
        _isReaderDone = null;
        _topicNames = null;
        _topicStreamDict = null;
    }
}
