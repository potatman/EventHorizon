using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avro;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Handles the "recovery" phase of the main cycle in <see cref="OrderGuaranteedPulsarTopicConsumer{T}"/>,
/// in which we read the stored messages that have queued up for any stream exiting failed state.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class MessageRecoveryHandler<T>: ITopicConsumer<T> where T : ITopicMessage, new()
{
    private readonly StreamFailureState<T> _streamFailureState;
    private readonly MessageRecoveryTopic<T> _messageRecoveryTopic;
    private readonly int _batchSize;

    public MessageRecoveryHandler(StreamFailureState<T> streamFailureState,
        MessageRecoveryTopic<T> messageRecoveryTopic, SubscriptionConfig<T> config)
    {
        _streamFailureState = streamFailureState;
        _messageRecoveryTopic = messageRecoveryTopic;
        _batchSize = config.BatchSize.GetValueOrDefault(1000);
    }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        var streamLastRecovery = _streamFailureState.StreamsInRecovery()
            .ToDictionary(s => s.StreamId, s => s.LastRecovered);
        var startDateTime = streamLastRecovery.Values.Min();
        var streamIds = streamLastRecovery.Keys.ToArray();
        var messages = new List<MessageContext<T>>();
        var count = 0;

        await foreach (var message in _messageRecoveryTopic.ReadMessages(streamIds, startDateTime, ct))
        {
            var createdDate = message.PublishTime.GetValueOrDefault(DateTime.UtcNow);
            if (createdDate > streamLastRecovery[message.StreamId])
            {
                messages.Add(
                    new MessageContext<T>
                    {
                        Data = message.Payload,
                        TopicData = new TopicData(
                            count.ToString(CultureInfo.InvariantCulture),
                            message.Topic,
                            createdDate),
                    });
                ++count;
                if (count >= _batchSize) break;
            }
        }

        if (messages.Count == 0)
        {
            // Reached the end of the recovery backlog. Mark all these streams as recovered.
            await _streamFailureState.StreamRecoveryCompleted(streamIds);
        }

        return messages.ToArray();
    }

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
            var anyFailed = firstFailedMessage != null;
            var successThresholdDate = anyFailed ? firstFailedMessage.TopicData.CreatedDate : DateTime.MaxValue;
            var succeededMessages = streamResults
                .Where(r => r.IsSuccess && r.Message.TopicData.CreatedDate < successThresholdDate)
                .ToArray();
            var anySucceeded = succeededMessages.Any();

            if (anySucceeded)
            {
                await _streamFailureState.RecoveryMessagesProcessed(streamId,
                    succeededMessages.Max(m => m.Message.TopicData.CreatedDate));
            }

            if (anyFailed)
            {
                await _streamFailureState.MessageProcessingFailed(firstFailedMessage);
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
