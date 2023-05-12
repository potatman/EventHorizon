using System;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// An event message for the <see cref="FailureStateTopic{T}"/>, representing a change in the
/// failure status for a stream.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class StreamFailureStateEvent<T> where T: ITopicMessage, new()
{
    /// <summary>
    /// Which stream the event pertains to.
    /// </summary>
    public string StreamId { get; set; }

    /// <summary>
    /// The type of event represented by this message.
    /// </summary>
    public StreamFailureStateEventType EventType { get; set; }

    /// <summary>
    /// The payload from the message that failed to process.
    /// </summary>
    /// <remarks>
    /// Applies only when <see cref="EventType"/> is MessageProcessingFailed.
    /// </remarks>
    public T FailedMessagePayload { get; set; }

    /// <summary>
    /// Original primary topic that message came from.
    /// </summary>
    /// <remarks>
    /// Populated only when <see cref="FailedMessagePayload"/> is populated.
    /// </remarks>
    public string FailedMessageTopic { get; set; }

    /// <summary>
    /// Original publish time of message that failed to process.
    /// </summary>
    /// <remarks>
    /// Populated only when <see cref="FailedMessagePayload"/> is populated.
    /// </remarks>
    public DateTime? FailedMessagePublishTime { get; set; }

    /// <summary>
    /// Amount of times this failed message has been retried.
    /// </summary>
    /// <remarks>
    /// Applies only when <see cref="EventType"/> is MessageProcessingFailed.
    /// </remarks>
    public int? TimesRetried { get; set; }

    /// <summary>
    /// Calculated time for the next retry attempt.
    /// </summary>
    /// <remarks>
    /// Applies only when <see cref="EventType"/> is MessageProcessingFailed.
    /// </remarks>
    public DateTime? NextRetry { get; set; }

    /// <summary>
    /// The timestamp of the latest recovery message processed.
    /// </summary>
    /// <remarks>
    /// Applies only when <see cref="EventType"/> is RecoveryMessagesProcessed.
    /// </remarks>
    public DateTime? LastRecoveryMessagePublishTime { get; set; }
}
