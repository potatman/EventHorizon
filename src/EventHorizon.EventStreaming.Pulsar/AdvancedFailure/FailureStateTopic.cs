using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Reflection;
using EventHorizon.EventStreaming.Pulsar.Models;
using EventHorizon.EventStreaming.Pulsar.Utils;
using EventHorizon.EventStreaming.Subscriptions;
using EventHorizon.EventStreaming.Tracing;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Pulsar.Client.Otel;

namespace EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Interface for communication with the topic that acts as an event history for individual
/// streams entering or exiting failure state for a subscription.
/// </summary>
/// <typeparam name="TMessage">Type of message from the primary topic.</typeparam>
public sealed class FailureStateTopic<TMessage>
    where TMessage : ITopicMessage
{
    private readonly PulsarClient _pulsarClient;
    private readonly PulsarTopicAdmin<TMessage> _admin;
    private readonly ILogger<FailureStateTopic<TMessage>> _logger;
    private readonly PulsarTopic _topic;
    private IProducer<TopicStreamState> _producer;
    private readonly string _publisherName;
    private ITableView<TopicStreamState> _tableView;
    private readonly OTelProducerInterceptor.OTelProducerInterceptor<TopicStreamState> _intercept;

    public FailureStateTopic(SubscriptionConfig<TMessage> subscriptionConfig, PulsarClient pulsarClient,
        PulsarTopicAdmin<TMessage> admin, ILogger<FailureStateTopic<TMessage>> logger)
    {
        _pulsarClient = pulsarClient;
        _admin = admin;
        _logger = logger;
        _topic = Topic(subscriptionConfig.Topics.First(), subscriptionConfig.SubscriptionName);
        _publisherName = AssemblyUtil.AssemblyNameWithGuid;
        _intercept = new OTelProducerInterceptor.OTelProducerInterceptor<TopicStreamState>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_tableView == null)
        {
            try
            {
                await _admin.RequireTopicAsync(_topic.ToString(), ct).ConfigureAwait(false);
                _tableView = await GetTableViewAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Something went wrong when initializing failure state topic.");
                throw;
            }
        }
    }

    public async Task PublishAsync(params TopicStreamState[] updates)
    {
        var producer = await GetProducerAsync().ConfigureAwait(false);
        foreach (var update in updates)
        {
            var message = producer.NewMessage(update, update.Key());
            await producer.SendAndForgetAsync(message).ConfigureAwait(false);
        }
    }

    private async Task<IProducer<TopicStreamState>> GetProducerAsync()
    {
        if (_producer != null) return _producer;

        var builder = _pulsarClient.NewProducer(Schema.JSON<TopicStreamState>())
            .ProducerName(_publisherName)
            .BlockIfQueueFull(true)
            .BatchBuilder(BatchBuilder.KeyBased)
            .CompressionType(CompressionType.LZ4)
            .MaxPendingMessages(10000)
            .MaxPendingMessagesAcrossPartitions(50000)
            .Intercept(_intercept)
            .Topic(_topic.ToString());

        _producer = await builder.CreateAsync().ConfigureAwait(false);

        return _producer;
    }

    public (TopicStreamState[] TopicStreams, int TotalTrackedTopicStreams) GetTopicStreams(
        PulsarKeyHashRanges keyHashRanges, Func<TopicStreamState, bool> predicate, int limit)
    {
        var totalTrackedTopicStreams = 0;

        var results = _tableView.Values
            .Where(ts => !ts.IsResolved && keyHashRanges.IsMatch(ts.StreamId))
            // Tally the total number of topic/streams in the given key hash ranges,
            // even if they are to be subsequently filtered by the given predicate.
            // It'll be useful to the caller to know what the total number of tracked topic/streams is.
            .Where(_ =>
            {
                totalTrackedTopicStreams++;
                return true;
            })
            .Where(predicate)
            .Take(limit)
            .ToArray();

        return (results, totalTrackedTopicStreams);
    }

    public TopicStreamState[] FindTopicStreams((string Topic, string StreamId)[] topicStreams)
    {
        return topicStreams
            .Select(ts => _tableView.GetValueOrDefault(ts.Key()))
            .Where(s => s is {IsResolved: false})
            .ToArray();
    }

    public TopicStreamState FindTopicStream((string Topic, string StreamId) topicStream)
    {
        var state = _tableView.GetValueOrDefault(topicStream.Key());
        return state == null || state.IsResolved ? null : state;
    }

    private Task<ITableView<TopicStreamState>> GetTableViewAsync()
    {
        return _pulsarClient.NewTableViewBuilder(Schema.JSON<TopicStreamState>())
            .Topic(_topic.ToString())
            .CreateAsync();
    }

    /// <summary>
    /// Based on subscription, provides failure state
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
