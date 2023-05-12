using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Util;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Pulsar topic consumer that uses advanced failure handling method to guarantee message order even
/// if some messages are nacked. Pulsar has no such order guarantees on nack, so the library must
/// enforce order outside of the Pulsar brokers.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class OrderGuaranteedPulsarTopicConsumer<T> : ITopicConsumer<T> where T : ITopicMessage, new()
{
    /// <summary>
    /// The main algorithm handled by this consumer cycles continuously through
    /// the following phases.
    /// </summary>
    private enum BatchPhase
    {
        /// <summary>
        /// Retry the last failed message from any streams in Failed mode (that are due for a retry.)
        /// </summary>
        FailureRetry,

        /// <summary>
        /// For streams that have recovered from a failed message, process any subsequent queued-up messages.
        /// </summary>
        Recovery,

        /// <summary>
        /// Process new messages from the main topic.
        /// </summary>
        Normal
    }

    private readonly SubscriptionConfig<T> _config;
    private readonly PulsarTopicAdmin<T> _admin;
    private readonly StreamFailureState<T> _streamFailureState;
    private readonly FailedMessageRetryHandler<T> _failedMessageRetryHandler;
    private readonly MessageRecoveryHandler<T> _messageRecoveryHandler;
    private readonly PrimaryTopicConsumer<T> _primaryTopicConsumer;
    private readonly Dictionary<BatchPhase, ITopicConsumer<T>> _phaseHandlers;
    private readonly OnCheckTimer _statsQueryTimer = new(TimeSpan.FromMinutes(1));

    private readonly string _consumerName = NameUtil.AssemblyNameWithGuid;
    private PulsarKeyHashRanges _keyHashRanges;

    private BatchPhase _phase = BatchPhase.FailureRetry;

    public OrderGuaranteedPulsarTopicConsumer(
        PulsarClientResolver clientResolver,
        SubscriptionConfig<T> config,
        PulsarTopicAdmin<T> admin)
    {
        _config = config;
        _admin = admin;

        FailureStateTopic<T> failureStateTopic = new(_config, clientResolver, admin);
        MessageRecoveryTopic<T> messageRecoveryTopic = new(config, clientResolver, admin);
        _streamFailureState = new(_config, failureStateTopic);
        _primaryTopicConsumer = new(_streamFailureState, messageRecoveryTopic, clientResolver,
            _config, _admin, _consumerName);
        _failedMessageRetryHandler = new(_config, _streamFailureState);
        _messageRecoveryHandler = new(_streamFailureState, messageRecoveryTopic, config);

        _phaseHandlers = new()
        {
            [BatchPhase.Normal] = _primaryTopicConsumer,
            [BatchPhase.FailureRetry] = _failedMessageRetryHandler,
            [BatchPhase.Recovery] = _messageRecoveryHandler,
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _primaryTopicConsumer.DisposeAsync();
    }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        await _primaryTopicConsumer.InitializeAsync(ct);

        if (_keyHashRanges == null)
        {
            // Pause a moment to ensure Pulsar can deliver key hash ranges when we query stats.
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        if (_keyHashRanges == null || ShouldQuerySubscriptionStats())
        {
            _keyHashRanges = await GetSubscriptionHashRanges(ct);
            await _streamFailureState.InitializeAsync(_keyHashRanges, ct);
        }

        if (_phase == BatchPhase.FailureRetry)
        {
            var messages = await _failedMessageRetryHandler.NextBatchAsync(ct);
            if (messages.Any()) return messages;
            _phase = BatchPhase.Recovery;
        }

        if (_phase == BatchPhase.Recovery)
        {
            var messages = await _messageRecoveryHandler.NextBatchAsync(ct);
            if (messages.Any()) return messages;
            _phase = BatchPhase.Normal;
        }

        // Normal phase.
        return await _primaryTopicConsumer.NextBatchAsync(ct);
    }

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        await _phaseHandlers[_phase].FinalizeBatchAsync(acks, nacks);

        _phase = _phase switch
        {
            BatchPhase.FailureRetry => BatchPhase.Recovery,
            BatchPhase.Recovery => BatchPhase.Normal,
            BatchPhase.Normal => BatchPhase.FailureRetry,
            _ => BatchPhase.Normal,
        };
    }

    /// <summary>
    /// Checks whether it's an appropriate time to query stats for the current subscription.
    /// </summary>
    private bool ShouldQuerySubscriptionStats() => _statsQueryTimer.Check();

    /// <summary>
    /// Query the Pulsar admin API for allotted key hash ranges for this consumer.
    /// </summary>
    private async Task<PulsarKeyHashRanges> GetSubscriptionHashRanges(CancellationToken ct)
    {
        var ranges = await _admin.GetTopicConsumerKeyHashRanges(_config.Topics.First(),
            _config.SubscriptionName, _consumerName, ct);
        return PulsarKeyHashRanges.Create(ranges);
    }
}
