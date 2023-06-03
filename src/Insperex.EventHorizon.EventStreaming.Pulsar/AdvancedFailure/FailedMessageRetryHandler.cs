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
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Handles the "failure retry" phase of the main cycle in <see cref="OrderGuaranteedPulsarTopicConsumer{T}"/>,
/// in which we retry any failed messages in an attempt to get the message's stream out of failure state.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class FailedMessageRetryHandler<T>: ITopicConsumer<T> where T : class, ITopicMessage, new()
{
    private readonly StreamFailureState<T> _streamFailureState;
    private readonly IStreamFactory _streamFactory;
    private readonly PulsarClientResolver _clientResolver;
    private readonly ILogger<FailedMessageRetryHandler<T>> _logger;
    private readonly int _batchSize;

    public FailedMessageRetryHandler(SubscriptionConfig<T> config, StreamFailureState<T> streamFailureState,
        IStreamFactory streamFactory, PulsarClientResolver clientResolver,
        ILogger<FailedMessageRetryHandler<T>> logger)
    {
        _batchSize = config.BatchSize ?? 1000;
        _streamFailureState = streamFailureState;
        _streamFactory = streamFactory;
        _clientResolver = clientResolver;
        _logger = logger;
    }

    public PulsarKeyHashRanges KeyHashRanges { get; set; }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        var streamsForRetry = _streamFailureState.StreamsForRetry()
            .Where(s => KeyHashRanges.IsMatch(s.StreamId))
            .Take(MaxStreams)
            .ToArray();

        if (streamsForRetry.Length == 0) return Array.Empty<MessageContext<T>>();

        var reader = new FailedMessageRetryReader<T>(streamsForRetry, _clientResolver, _streamFactory.CreateAdmin<T>(),
            _logger);

        var messages = await reader.GetNextAsync(_batchSize, ct);

        if (messages.Length < _batchSize)
        {
            // If we didn't get a full batch, that may mean some streams have been fully resolved.
            // Check if any streams didn't get any messages (and aren't still expecting a retry at some stage).

            await MarkResolvedTopicStreams(messages, streamsForRetry);
        }

        return messages;
    }

    private async Task MarkResolvedTopicStreams(MessageContext<T>[] messages, StreamState[] streamsForRetry)
    {
        var streamsWithMessages = messages
            .Select(m => (m.Data.StreamId, m.TopicData.Topic))
            .ToHashSet();

        var resolvedTopicStreams = streamsForRetry
            .SelectMany(s => s.Topics)
            .Where(st => !st.Value.NextRetry.HasValue)
            .Where(st => !streamsWithMessages.Contains((st.Key, st.Value.TopicName)))
            .ToArray();

        foreach (var resolvedTopicStream in resolvedTopicStreams)
        {
            await _streamFailureState.StreamResolved(resolvedTopicStream.Key, resolvedTopicStream.Value.TopicName);
        }
    }

    private int MaxStreams => _batchSize / 3;

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        var results = acks.Select(a => (IsSuccess: true, Message: a))
            .Concat(nacks.Select(n => (IsSuccess: false, Message: n)));
        var resultsByStream = results
            .ToLookup(r => r.Message.Data.StreamId);

        foreach (var streamResults in resultsByStream)
        {
            var streamId = streamResults.Key;
            var firstFailedMessage = streamResults
                .Where(r => !r.IsSuccess)
                .Select(r => r.Message)
                .FirstOrDefault();

            if (firstFailedMessage != null)
            {
                await _streamFailureState.MessageFailed(firstFailedMessage);
            }
            else
            {
                var lastMessage = streamResults.Last().Message;
                await _streamFailureState.MessageSucceeded(lastMessage);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
