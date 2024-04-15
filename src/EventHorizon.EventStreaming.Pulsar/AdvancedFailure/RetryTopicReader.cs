using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.Abstractions.Reflection;
using EventHorizon.EventStreaming.Pulsar.Utils;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;
using Pulsar.Client.Common;

namespace EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Reads from the subscription topics (all of them) and merges
/// messages from all topics into one reader stream for the output.
/// </summary>
/// <typeparam name="TMessage">Topic message type.</typeparam>
public class RetryTopicReader<TMessage>: IAsyncDisposable
    where TMessage : ITopicMessage
{
    private const int BufferSize = 1000;

    private sealed class TopicReaderContext
    {
        public string Topic { get; set; }
        public IReader<TMessage> Reader { get; set; }
        public DateTime ReaderStartTime { get; set; }
        public Dictionary<string, TopicStreamState> Streams;
        public Queue<Message<TMessage>> MessageQueue { get; set; }
        public bool ContinueReading { get; set; }
    }

    private readonly Dictionary<string, IReader<TMessage>> _readers = new();
    private readonly PulsarClient _pulsarClient;
    private readonly SubscriptionConfig<TMessage> _config;
    private readonly ILogger<RetryTopicReader<TMessage>> _logger;

    public RetryTopicReader(PulsarClient pulsarClient, SubscriptionConfig<TMessage> config, ILogger<RetryTopicReader<TMessage>> logger)
    {
        _pulsarClient = pulsarClient;
        _config = config;
        _logger = logger;
    }

    public async Task<MessageContext<TMessage>[]> GetNextAsync(TopicStreamState[] topicStreams, int batchSize, CancellationToken ct)
    {
        var contexts = await InitReaderContextsAsync(topicStreams).ConfigureAwait(false);
        var asOf = DateTime.UtcNow;
        var messages = new List<MessageContext<TMessage>>();
        bool anyMoreMessages;

        do
        {
            var (topic, message) = await GetNextMessage(contexts, ct).ConfigureAwait(false);
            anyMoreMessages = message != null;

            if (anyMoreMessages)
            {
                var data = message.GetValue();
                var isMessageEligible = IsMessageEligible(message, data.StreamId, contexts[topic], asOf);

                if (isMessageEligible)
                {
                    //_logger.LogInformation("Reader: got msg: {message}", MsgToStr(message, topic));
                    var sequenceId = message.SequenceId.ToString(CultureInfo.InvariantCulture);

                    var topicData = PulsarMessageMapper.MapTopicData(sequenceId, message, topic);
                    messages.Add(new MessageContext<TMessage>(data, topicData, _config.TypeDict));
                }
            }
        } while (messages.Count < batchSize && anyMoreMessages);

        return messages.ToArray();
    }

    private async Task<Dictionary<string, TopicReaderContext>> InitReaderContextsAsync(TopicStreamState[] topicStreams)
    {
        var topicStreamsByTopic = topicStreams.ToLookup(ts => ts.Topic);

        var contexts = topicStreamsByTopic
            .Select(async tsl => new TopicReaderContext
            {
                Topic = tsl.Key,
                Streams = tsl.ToDictionary(ts => ts.StreamId),
                MessageQueue = new(),
                Reader = await GetReader(tsl.Key).ConfigureAwait(false),
                ReaderStartTime = tsl.Min(s => s.LastMessagePublishTime),
                ContinueReading = true,
            })
            .Select(t => t.Result)
            .ToDictionary(trc => trc.Topic);

        foreach (var context in contexts.Values)
        {
            var seekTimestamp = PulsarMessageMapper.PublishTimestampFromDate(context.ReaderStartTime);
            await context.Reader.SeekAsync(seekTimestamp).ConfigureAwait(false);
        }

        return contexts;
    }

    private static async Task<(string Topic, Message<TMessage> Message)> GetNextMessage(
        Dictionary<string, TopicReaderContext> contexts,
        CancellationToken ct)
    {
        await PrimeTopicQueuesAsync(contexts, ct).ConfigureAwait(false);

        var contextsWithMessages = contexts.Values
            .Where(c => c.MessageQueue.Any())
            .ToArray();

        if (!contextsWithMessages.Any()) return (null, null);

        var selectedContext = contextsWithMessages
            .MinBy(q => q.MessageQueue.Peek().PublishTime);

        return (selectedContext.Topic, selectedContext.MessageQueue.Dequeue());
    }

    private static async Task PrimeTopicQueuesAsync(Dictionary<string, TopicReaderContext> contexts, CancellationToken ct)
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
            bool moreMessages = await context.Reader.HasMessageAvailableAsync().ConfigureAwait(false);

            while (context.MessageQueue.Count < BufferSize && moreMessages)
            {
                var message = await context.Reader.ReadNextAsync(ct).ConfigureAwait(false);

                context.MessageQueue.Enqueue(message);
                moreMessages = await context.Reader.HasMessageAvailableAsync().ConfigureAwait(false);
            }

            if (!moreMessages) context.ContinueReading = false;
        }
    }

    private static bool IsMessageEligible(Message<TMessage> message, string streamId, TopicReaderContext context,
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

    private async Task<IReader<TMessage>> GetReader(string topic)
    {
        if (_readers.TryGetValue(topic, out var reader)) return reader;

        var newReader = await _pulsarClient
            .NewReader(Schema.JSON<TMessage>())
            .Topic(topic)
            .ReaderName($"{AssemblyUtil.AssemblyNameWithGuid}_retry_{topic}")
            .ReceiverQueueSize(BufferSize)
            .StartMessageId(MessageId.Earliest)
            .CreateAsync()
            .ConfigureAwait(false);

        _readers.Add(topic, newReader);
        return newReader;
    }

    public async ValueTask DisposeAsync()
    {
        if (_readers != null)
        {
            foreach (var reader in _readers.Values)
            {
                await reader.DisposeAsync().ConfigureAwait(false);
            }

            _readers.Clear();
        }
    }

    private static string MsgToStr(Message<TMessage> message, string topic)
    {
        var data = message.GetValue();
        return ($"{topic}=>{data.StreamId}=>{message.SequenceId}");
    }
}
