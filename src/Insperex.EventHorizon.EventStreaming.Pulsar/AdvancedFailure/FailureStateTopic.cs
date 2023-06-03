using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Tracing;
using Insperex.EventHorizon.EventStreaming.Util;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<FailureStateTopic<T>> _logger;
    private readonly PulsarTopic _topic;
    private IProducer<StreamState> _producer;
    private readonly string _publisherName;
    private ITableView<StreamState> _tableView;
    private readonly OTelProducerInterceptor.OTelProducerInterceptor<StreamState> _intercept;

    public FailureStateTopic(SubscriptionConfig<T> subscriptionConfig, PulsarClientResolver clientResolver,
        PulsarTopicAdmin<T> admin, ILogger<FailureStateTopic<T>> logger)
    {
        _clientResolver = clientResolver;
        _admin = admin;
        _logger = logger;
        _topic = Topic(subscriptionConfig.Topics.First(), subscriptionConfig.SubscriptionName);
        _publisherName = NameUtil.AssemblyNameWithGuid;
        _intercept = new OTelProducerInterceptor.OTelProducerInterceptor<StreamState>(
            TraceConstants.ActivitySourceName, PulsarClient.Logger);
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        if (_tableView == null)
        {
            try
            {
                await _admin.RequireTopicAsync(_topic.ToString(), ct);
                _tableView = await GetTableViewAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Something went wrong when initializing failure state topic.");
                throw;
            }
        }
    }

    public async Task Publish(params StreamState[] streamUpdates)
    {
        var producer = await GetProducerAsync();
        foreach (var streamUpdate in streamUpdates)
        {
            var message = producer.NewMessage(streamUpdate, streamUpdate.StreamId);
            await producer.SendAndForgetAsync(message);
        }
    }

    private async Task<IProducer<StreamState>> GetProducerAsync()
    {
        if (_producer != null) return _producer;

        var client = await _clientResolver.GetPulsarClientAsync();
        var builder = client.NewProducer(Schema.JSON<StreamState>())
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

    public IEnumerable<StreamState> GetStreams()
    {
        return _tableView.Values
            .Where(s => s.Topics?.Keys.Any() == true);
    }

    public IEnumerable<StreamState> FindStreams(string[] streamIds)
    {
        foreach (var streamId in streamIds)
        {
            var streamState = _tableView.GetValueOrDefault(streamId);
            if (streamState?.Topics?.Keys.Any() == true)
                yield return streamState;
        }
    }

    public StreamState FindStream(string streamId)
    {
        var streamState = _tableView.GetValueOrDefault(streamId);
        return streamState?.Topics?.Keys.Any() == true ? streamState : null;
    }

    private async Task<ITableView<StreamState>> GetTableViewAsync()
    {
        var client = await _clientResolver.GetPulsarClientAsync();
        return await client.NewTableViewBuilder(Schema.JSON<StreamState>())
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
