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

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Interface for communication with the topic that acts as an event history for individual
/// streams entering or exiting failure state for a subscription.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public sealed class FailureStateTopic<T> where T : ITopicMessage, new()
{
    private readonly PulsarClientResolver _clientResolver;
    private readonly PulsarTopicAdmin<T> _admin;
    private readonly PulsarTopic _topic;
    private IProducer<StreamFailureStateEvent<T>> _producer;
    private readonly string _publisherName;
    private readonly OTelProducerInterceptor.OTelProducerInterceptor<StreamFailureStateEvent<T>> _intercept;

    public FailureStateTopic(SubscriptionConfig<T> subscriptionConfig, PulsarClientResolver clientResolver,
        PulsarTopicAdmin<T> admin)
    {
        _clientResolver = clientResolver;
        _admin = admin;
        _topic = Topic(subscriptionConfig.Topics.First(), subscriptionConfig.SubscriptionName);
        _publisherName = NameUtil.AssemblyNameWithGuid;
        _intercept = new OTelProducerInterceptor.OTelProducerInterceptor<StreamFailureStateEvent<T>>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        await _admin.RequireTopicAsync(_topic.ToString(), ct);
    }

    private async Task<IProducer<StreamFailureStateEvent<T>>> GetProducerAsync()
    {
        if (_producer != null) return _producer;

        var client = await _clientResolver.GetPulsarClientAsync();
        var builder = client.NewProducer(Schema.JSON<StreamFailureStateEvent<T>>())
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

    public async IAsyncEnumerable<StreamFailureStateEvent<T>> ReadEvents(PulsarKeyHashRanges keyHashRanges,
        [EnumeratorCancellation] CancellationToken ct)
    {
        await using var reader = await GetReader(keyHashRanges);

        while (!reader.HasReachedEndOfTopic && await reader.HasMessageAvailableAsync())
        {
            var message = await reader.ReadNextAsync(ct);
            yield return message.GetValue();
        }
    }

    private async Task<IReader<StreamFailureStateEvent<T>>> GetReader(PulsarKeyHashRanges keyHashRanges )
    {
        var client = await _clientResolver.GetPulsarClientAsync();
        return await client.NewReader(Schema.JSON<StreamFailureStateEvent<T>>())
            .Topic(_topic.ToString())
            .ReaderName(NameUtil.AssemblyNameWithGuid)
            .ReceiverQueueSize(1000)
            .StartMessageId(MessageId.Earliest)
            .KeyHashRange(keyHashRanges.ToRangeArray())
            .CreateAsync();
    }

    public async Task PublishEvents(params StreamFailureStateEvent<T>[] events)
    {
        var producer = await GetProducerAsync();
        foreach (var evt in events)
        {
            var msg = producer.NewMessage(evt, evt.StreamId);
            await producer.SendAndForgetAsync(msg);
        }
    }

    /// <summary>
    /// Based on primary topic and the subscription to it, provides failure state
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
            Topic = $"subscription__{subscriptionName}__streamFailureState",
        };
    }
}
