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

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Reads from the subscription topics (all of them) and merges
/// messages from all topics into one reader stream for the output.
/// </summary>
/// <typeparam name="T">Topic message type.</typeparam>
public class RetryTopicReader<T>: IAsyncDisposable where T : class, ITopicMessage, new()
{
    private const int BufferSize = 1000;

    private sealed class TopicReaderContext
    {
        public string Topic { get; set; }
        public IReader<T> Reader { get; set; }
        public DateTime ReaderStartTime { get; set; }
        public Dictionary<string, TopicStreamState> Streams;
        public Queue<Message<T>> MessageQueue { get; set; }
        public bool ContinueReading { get; set; }
    }

    private readonly Dictionary<string, IReader<T>> _readers = new();
    private readonly PulsarClientResolver _clientResolver;
    private readonly ILogger<RetryTopicReader<T>> _logger;

    public RetryTopicReader(PulsarClientResolver clientResolver, ILogger<RetryTopicReader<T>> logger)
    {
        _clientResolver = clientResolver;
        _logger = logger;
    }

    public async Task<MessageContext<T>[]> GetNextAsync(TopicStreamState[] topicStreams, int batchSize,
        CancellationToken ct)
    {
        var contexts = await InitReaderContexts(topicStreams);
        var asOf = DateTime.UtcNow;
        var messages = new List<MessageContext<T>>();
        bool anyMoreMessages;

        do
        {
            var (topic, message) = await GetNextMessage(contexts, ct);
            anyMoreMessages = message != null;

            if (anyMoreMessages)
            {
                var data = message.GetValue();
                var isMessageEligible = IsMessageEligible(message, data.StreamId, contexts[topic], asOf);

                if (isMessageEligible)
                {
                    //_logger.LogInformation($"Reader: got msg: {MsgToStr(message, topic)}");
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

    private async Task<Dictionary<string, TopicReaderContext>> InitReaderContexts(TopicStreamState[] topicStreams)
    {
        var topicStreamsByTopic = topicStreams.ToLookup(ts => ts.Topic);

        var contexts = topicStreamsByTopic
            .Select(async tsl => new TopicReaderContext
            {
                Topic = tsl.Key,
                Streams = tsl.ToDictionary(ts => ts.StreamId),
                MessageQueue = new(),
                Reader = await GetReader(tsl.Key),
                ReaderStartTime = tsl.Min(s => s.LastMessagePublishTime),
                ContinueReading = true,
            })
            .Select(t => t.Result)
            .ToDictionary(trc => trc.Topic);

        foreach (var context in contexts.Values)
        {
            var seekTimestamp = PulsarMessageMapper.PublishTimestampFromDate(context.ReaderStartTime);
            await context.Reader.SeekAsync(seekTimestamp);
        }

        return contexts;
    }

    private static async Task<(string Topic, Message<T> Message)> GetNextMessage(
        Dictionary<string, TopicReaderContext> contexts,
        CancellationToken ct)
    {
        await PrimeTopicQueues(contexts, ct);

        var contextsWithMessages = contexts.Values
            .Where(c => c.MessageQueue.Any())
            .ToArray();

        if (!contextsWithMessages.Any()) return (null, null);

        var selectedContext = contextsWithMessages
            .MinBy(q => q.MessageQueue.Peek().PublishTime);

        return (selectedContext.Topic, selectedContext.MessageQueue.Dequeue());
    }

    private static async Task PrimeTopicQueues(Dictionary<string, TopicReaderContext> contexts, CancellationToken ct)
    {
        foreach (var context in contexts.Values)
        {
            await PrimeTopicQueue(context, ct);
        }
    }

    private static async Task PrimeTopicQueue(TopicReaderContext context, CancellationToken ct)
    {
        if (context.ContinueReading && !context.MessageQueue.Any())
        {
            bool moreMessages = await context.Reader.HasMessageAvailableAsync();

            while (context.MessageQueue.Count < BufferSize && moreMessages)
            {
                var message = await context.Reader.ReadNextAsync(ct);

                context.MessageQueue.Enqueue(message);
                moreMessages = await context.Reader.HasMessageAvailableAsync();
            }

            if (!moreMessages) context.ContinueReading = false;
        }
    }

    private static bool IsMessageEligible(Message<T> message, string streamId, TopicReaderContext context,
        DateTime asOf)
    {
        var topicStream = context.Streams.GetValueOrDefault(streamId);

        if (topicStream != null)
        {
            return topicStream.NextRetry.HasValue
                // Failure retry mode - would need to reprocess last message since it had failed.
                ? asOf >= topicStream.NextRetry.Value && message.SequenceId >= topicStream.LastSequenceId
                // Recovery mode - last message will have already been successfully processed.
                : message.SequenceId > topicStream.LastSequenceId;
        }

        return false;
    }

    private async Task<IReader<T>> GetReader(string topic)
    {
        if (_readers.TryGetValue(topic, out var reader)) return reader;

        var client = await _clientResolver.GetPulsarClientAsync();

        var newReader = await client
            .NewReader(Schema.JSON<T>())
            .Topic(topic)
            .ReaderName($"{NameUtil.AssemblyNameWithGuid}_retry_{topic}")
            .ReceiverQueueSize(BufferSize)
            .StartMessageId(MessageId.Earliest)
            .CreateAsync();

        _readers.Add(topic, newReader);
        return newReader;
    }

    public async ValueTask DisposeAsync()
    {
        if (_readers != null)
        {
            foreach (var reader in _readers.Values)
            {
                await reader.DisposeAsync();
            }

            _readers.Clear();
        }
    }

    private static string MsgToStr(Message<T> message, string topic)
    {
        var data = message.GetValue();
        return ($"{topic}=>{data.StreamId}=>{message.SequenceId}");
    }
}
