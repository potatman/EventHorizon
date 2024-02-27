using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;
using Pulsar.Client.Api;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Pulsar topic consumer that uses advanced failure handling method to guarantee message order even
/// if some messages are nacked. Pulsar has no such order guarantees on nack, so the library must
/// enforce order outside of the Pulsar brokers.
/// </summary>
/// <typeparam name="TMessage">Type of message from the primary topic.</typeparam>
public class OrderGuaranteedPulsarTopicConsumer<TMessage> : ITopicConsumer<TMessage>
    where TMessage : ITopicMessage
{
    /// <summary>
    /// The main algorithm handled by this consumer cycles continuously through
    /// the following phases.
    /// </summary>
    private enum BatchPhase
    {
        /// <summary>
        /// Process failed and subsequent messages from streams that are in or are recovering from a failed state.
        /// </summary>
        FailureRetry,

        /// <summary>
        /// Process new messages from the main topic.
        /// </summary>
        Normal
    }

    private readonly SubscriptionConfig<TMessage> _config;
    private readonly ILogger<OrderGuaranteedPulsarTopicConsumer<TMessage>> _logger;
    private readonly PulsarTopicAdmin<TMessage> _topicAdmin;
    private readonly StreamFailureState<TMessage> _streamFailureState;
    private readonly FailedMessageRetryConsumer<TMessage> _failedMessageRetryConsumer;
    private readonly PrimaryTopicConsumer<TMessage> _primaryTopicConsumer;
    private readonly Dictionary<BatchPhase, ITopicConsumer<TMessage>> _phaseHandlers;
    private readonly OnCheckTimer _statsQueryTimer = new(TimeSpan.FromMinutes(30));
    private readonly object _batchInProgressLock = new object();
    private bool _batchInProgress;

    private readonly string _consumerName = AssemblyUtil.AssemblyNameWithGuid;
    private PulsarKeyHashRanges _keyHashRanges;

    private BatchPhase _phase = BatchPhase.Normal;

    public OrderGuaranteedPulsarTopicConsumer(
        PulsarClient pulsarClient,
        SubscriptionConfig<TMessage> config,
        IStreamFactory streamFactory,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<OrderGuaranteedPulsarTopicConsumer<TMessage>>();

        _topicAdmin = (PulsarTopicAdmin<TMessage>)streamFactory.CreateAdmin<TMessage>();
        _config = config;

        FailureStateTopic<TMessage> failureStateTopic = new(_config, pulsarClient, _topicAdmin,
            loggerFactory.CreateLogger<FailureStateTopic<TMessage>>());
        _streamFailureState = new(_config, loggerFactory.CreateLogger<StreamFailureState<TMessage>>(),
            failureStateTopic);
        _primaryTopicConsumer = new(_streamFailureState, pulsarClient,
            loggerFactory.CreateLogger<PrimaryTopicConsumer<TMessage>>(),
            _config, _topicAdmin, _consumerName);
        _failedMessageRetryConsumer = new(_config, _streamFailureState,
            pulsarClient, loggerFactory);

        _phaseHandlers = new()
        {
            [BatchPhase.Normal] = _primaryTopicConsumer,
            [BatchPhase.FailureRetry] = _failedMessageRetryConsumer,
        };
    }

    public async ValueTask DisposeAsync()
    {
        await _primaryTopicConsumer.DisposeAsync();
    }

    public async Task InitAsync()
    {
        await _primaryTopicConsumer.InitAsync();
        await _streamFailureState.InitializeAsync(CancellationToken.None);
    }

    public async Task<MessageContext<TMessage>[]> NextBatchAsync(CancellationToken ct)
    {
        lock (_batchInProgressLock)
        {
            if (_batchInProgress)
                return Array.Empty<MessageContext<TMessage>>();
            _batchInProgress = true;
        }

        _phase = _phase switch
        {
            BatchPhase.FailureRetry => BatchPhase.Normal,
            BatchPhase.Normal => BatchPhase.FailureRetry,
            _ => BatchPhase.Normal,
        };

        if (_keyHashRanges == null || ShouldQuerySubscriptionStats())
        {
            _logger.LogInformation(
                "Reloading key hash ranges for subscription {subscriptionName}, consumer {consumerName}",
                _config.SubscriptionName, _consumerName);

            _keyHashRanges = await GetSubscriptionHashRanges(ct);
            _streamFailureState.KeyHashRanges = _keyHashRanges;
            _primaryTopicConsumer.KeyHashRanges = _keyHashRanges;
        }

        if (_phase == BatchPhase.FailureRetry)
        {
            try
            {
                var failureRetryMessages = await _failedMessageRetryConsumer.NextBatchAsync(ct);
                if (failureRetryMessages.Any())
                {
                    _logger.LogInformation(
                        "Failure retry processing: got {eventCount} events in batch",
                        failureRetryMessages.Length);
                    return failureRetryMessages;
                }
                _phase = BatchPhase.Normal;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error on failure phase batch retrieval");
                throw;
            }
        }

        // Normal phase.
        var messages = await _primaryTopicConsumer.NextBatchAsync(ct);
        if (messages.Length == 0)
        {
            lock (_batchInProgressLock)
            {
                _batchInProgress = false;
            }
        }
        return messages;
    }

    public async Task FinalizeBatchAsync(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks)
    {
        try
        {
            await _phaseHandlers[_phase].FinalizeBatchAsync(acks, nacks);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Error in FinalizeBatchAsync");
            throw;
        }
        finally
        {
            lock (_batchInProgressLock)
            {
                _batchInProgress = false;
            }
        }
    }

    /// <summary>
    /// Checks whether it's an appropriate time to query stats for the current subscription.
    /// </summary>
    private bool ShouldQuerySubscriptionStats() =>
        _primaryTopicConsumer.KeyHashRangeOutlierFound
        || _statsQueryTimer.Check();

    /// <summary>
    /// Query the Pulsar admin API for allotted key hash ranges for this consumer.
    /// </summary>
    private async Task<PulsarKeyHashRanges> GetSubscriptionHashRanges(CancellationToken ct)
    {
        return await _topicAdmin.GetTopicConsumerKeyHashRanges(_config.Topics.First(),
            _config.SubscriptionName, _consumerName, ct);
    }
}
