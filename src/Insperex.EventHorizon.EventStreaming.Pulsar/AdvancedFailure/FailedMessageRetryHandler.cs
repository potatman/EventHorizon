using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Handles the "failure retry" phase of the main cycle in <see cref="OrderGuaranteedPulsarTopicConsumer{T}"/>,
/// in which we retry any failed messages in an attempt to get the message's stream out of failure state.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class FailedMessageRetryHandler<T>: ITopicConsumer<T> where T : ITopicMessage, new()
{
    private readonly SubscriptionConfig<T> _config;
    private readonly StreamFailureState<T> _streamFailureState;

    public FailedMessageRetryHandler(SubscriptionConfig<T> config, StreamFailureState<T> streamFailureState)
    {
        _config = config;
        _streamFailureState = streamFailureState;
    }

    public Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct) =>
        Task.FromResult(_streamFailureState.FailedStreamsEligibleForRetry()
            .Take(_config.BatchSize ?? 1000)
            .Select((streamInfo, i) =>
                new MessageContext<T>
                {
                    Data = streamInfo.Data,
                    TopicData = new TopicData(
                        i.ToString(CultureInfo.InvariantCulture),
                        streamInfo.Topic,
                        streamInfo.Published
                        )
                })
            .ToArray());

    public async Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        if (acks?.Length > 0)
        {
            await _streamFailureState.FailedMessageRetrySucceeded(acks);
        }
        if (nacks?.Length > 0)
        {
            await _streamFailureState.MessageProcessingFailed(nacks);
        }
    }

    public void Dispose()
    {
    }
}
