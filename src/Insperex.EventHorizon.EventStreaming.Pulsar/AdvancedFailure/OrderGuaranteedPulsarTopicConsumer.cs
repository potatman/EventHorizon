using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Interfaces;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Util;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Pulsar topic consumer that uses advanced failure handling method to guarantee message order even
/// if some messages are nacked. Pulsar has no such order guarantees on nack, so the library must
/// enforce order outside of the Pulsar brokers.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class OrderGuaranteedPulsarTopicConsumer<T> : ITopicConsumer<T> where T : class, ITopicMessage, new()
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

    private readonly SubscriptionConfig<T> _config;
    private readonly ILogger<OrderGuaranteedPulsarTopicConsumer<T>> _logger;
    private readonly IPulsarKeyHashRangeProvider _keyHashRangeProvider;
    private readonly StreamFailureState<T> _streamFailureState;
    private readonly FailedMessageRetryHandler<T> _failedMessageRetryHandler;
    private readonly PrimaryTopicConsumer<T> _primaryTopicConsumer;
    private readonly Dictionary<BatchPhase, ITopicConsumer<T>> _phaseHandlers;
    private readonly OnCheckTimer _statsQueryTimer = new(TimeSpan.FromMinutes(1));

    private readonly string _consumerName = NameUtil.AssemblyNameWithGuid;
    private PulsarKeyHashRanges _keyHashRanges;

    private BatchPhase _phase = BatchPhase.Normal;

    public OrderGuaranteedPulsarTopicConsumer(
        PulsarClientResolver clientResolver,
        SubscriptionConfig<T> config,
        IStreamFactory streamFactory,
        ILoggerFactory loggerFactory,
        IPulsarKeyHashRangeProvider keyHashRangeProvider)
    {
        _logger = loggerFactory.CreateLogger<OrderGuaranteedPulsarTopicConsumer<T>>();

        var admin = (PulsarTopicAdmin<T>)streamFactory.CreateAdmin<T>();
        _config = config;
        _keyHashRangeProvider = keyHashRangeProvider;

        FailureStateTopic<T> failureStateTopic = new(_config, clientResolver, admin,
            loggerFactory.CreateLogger<FailureStateTopic<T>>());
        _streamFailureState = new(_config, loggerFactory.CreateLogger<StreamFailureState<T>>(),
            failureStateTopic);
        _primaryTopicConsumer = new(_streamFailureState, clientResolver,
            loggerFactory.CreateLogger<PrimaryTopicConsumer<T>>(),
            _config, admin, _consumerName);
        _failedMessageRetryHandler = new(_config, _streamFailureState,
            clientResolver, loggerFactory);

        _phaseHandlers = new()
        {
            [BatchPhase.Normal] = _primaryTopicConsumer,
            [BatchPhase.FailureRetry] = _failedMessageRetryHandler,
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

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        _phase = _phase switch
        {
            BatchPhase.FailureRetry => BatchPhase.Normal,
            BatchPhase.Normal => BatchPhase.FailureRetry,
            _ => BatchPhase.Normal,
        };

        if (_keyHashRanges == null)
        {
            // Pause a moment to ensure Pulsar can deliver key hash ranges when we query stats.
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
        }

        if (_keyHashRanges == null || ShouldQuerySubscriptionStats())
        {
            _logger.LogInformation(
                $"Reloading key hash ranges for subscription {_config.SubscriptionName}, consumer {_consumerName}");

            _keyHashRanges = await GetSubscriptionHashRanges(ct);
            _failedMessageRetryHandler.KeyHashRanges = _keyHashRanges;
        }

        if (_phase == BatchPhase.FailureRetry)
        {
            try
            {
                var failureRetryMessages = await _failedMessageRetryHandler.NextBatchAsync(ct);
                if (failureRetryMessages.Any())
                {
                    _logger.LogInformation($"Failure retry processing: got {failureRetryMessages.Length} events in batch");
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
        return messages;
    }

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
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
        return await _keyHashRangeProvider.GetSubscriptionHashRanges(_config.Topics.First(),
            _config.SubscriptionName, _consumerName, ct);
    }
}
