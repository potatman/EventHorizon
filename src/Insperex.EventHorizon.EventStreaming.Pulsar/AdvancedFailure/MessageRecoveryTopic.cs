using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Insperex.EventHorizon.EventStreaming.Util;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;
using Range = Pulsar.Client.Api.Range;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Interface for communication with the topic that stores messages received from the primary
/// topic subsequent to a message that had failed, for a given stream. As long as the stream
/// has not been returned to "normal" mode, this message recovery topic will receive all its
/// messages.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class MessageRecoveryTopic<T> where T : ITopicMessage, new()
{
    private readonly PulsarClientResolver _clientResolver;
    private readonly PulsarTopicAdmin<T> _admin;
    private readonly PulsarTopic _topic;
    private IProducer<RecoveryMessage<T>> _producer;
    private readonly string _publisherName;
    private readonly OTelProducerInterceptor.OTelProducerInterceptor<RecoveryMessage<T>> _intercept;

    public MessageRecoveryTopic(SubscriptionConfig<T> subscriptionConfig, PulsarClientResolver clientResolver,
        PulsarTopicAdmin<T> admin)
    {
        _clientResolver = clientResolver;
        _admin = admin;
        _topic = Topic(subscriptionConfig.Topics.First(), subscriptionConfig.SubscriptionName);
        _publisherName = NameUtil.AssemblyNameWithGuid;
        _intercept = new OTelProducerInterceptor.OTelProducerInterceptor<RecoveryMessage<T>>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _admin.RequireTopicAsync(_topic.ToString(), ct);
    }

    private async Task<IProducer<RecoveryMessage<T>>> GetProducerAsync()
    {
        if (_producer != null) return _producer;

        var client = await _clientResolver.GetPulsarClientAsync();
        var builder = client.NewProducer(Schema.JSON<RecoveryMessage<T>>())
            .ProducerName(_publisherName)
            .BlockIfQueueFull(true)
            .BatchBuilder(BatchBuilder.KeyBased)
            .CompressionType(CompressionType.LZ4)
            .MaxPendingMessages(10000)
            .MaxPendingMessagesAcrossPartitions(50000)
            .Intercept(_intercept)
            .Topic(_topic.ToString());

        _producer = await builder.CreateAsync();

        return _producer;
    }

    public async IAsyncEnumerable<RecoveryMessage<T>> ReadMessages(string[] streamIds, DateTime startTime,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var streamLookup = streamIds.ToHashSet();

        await using var reader = await GetReader(streamIds, startTime);

        while (!reader.HasReachedEndOfTopic && await reader.HasMessageAvailableAsync())
        {
            var message = await reader.ReadNextAsync(ct);
            // Due to imperfect hash algorithm here, we might get stream IDs back that were not
            // in the requested set. Be sure to filter those out.
            if (streamLookup.Contains(message.GetValue().StreamId))
            {
                yield return message.GetValue();
            }
        }
    }

    private async Task<IReader<RecoveryMessage<T>>> GetReader(string[] streamIds, DateTime startTime)
    {
        var keyHashRanges = streamIds
            .Select(x => MurmurHash3.Hash(x) % 65536)
            .Select(x => new Range(x, x))
            .ToArray();

        var client = await _clientResolver.GetPulsarClientAsync();
        var reader = await client.NewReader(Schema.JSON<RecoveryMessage<T>>())
            .Topic(_topic.ToString())
            .ReaderName(NameUtil.AssemblyNameWithGuid)
            .ReceiverQueueSize(1000)
            .StartMessageId(MessageId.Earliest)
            .KeyHashRange(keyHashRanges)
            .CreateAsync();

        await reader.SeekAsync(startTime.Ticks);

        return reader;
    }

    public async Task PublishMessages(params RecoveryMessage<T>[] events)
    {
        var producer = await GetProducerAsync();
        foreach (var evt in events)
        {
            var msg = producer.NewMessage(evt, evt.StreamId);
            await producer.SendAndForgetAsync(msg);
        }
    }

    /// <summary>
    /// Based on primary topic and the subscription to it, provides message recovery
    /// topic info.
    /// </summary>
    public static PulsarTopic Topic(string primaryTopicName, string subscriptionName)
    {
        var primaryTopic = PulsarTopicParser.Parse(primaryTopicName);

        return new PulsarTopic()
        {
            IsPersisted = true,
            Tenant = primaryTopic.Tenant,
            Namespace = primaryTopic.Namespace,
            Topic = $"subscription__{subscriptionName}__messageRecovery",
        };
    }
}
