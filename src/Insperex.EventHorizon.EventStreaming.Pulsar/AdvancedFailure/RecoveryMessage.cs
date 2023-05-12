using System;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// A message for the <see cref="MessageRecoveryTopic{T}"/>, representing a bookmarked message for a failed
/// stream, that must be retried once the stream is in Recovery state.
/// </summary>
/// <typeparam name="T">Type of message from the primary topic.</typeparam>
public class RecoveryMessage<T> where T: ITopicMessage, new()
{
    /// <summary>
    /// Which stream the message pertains to.
    /// </summary>
    public string StreamId { get; set; }

    /// <summary>
    /// The payload from the message.
    /// </summary>
    public T Payload { get; set; }

    /// <summary>
    /// Original primary topic that message came from.
    /// </summary>
    public string Topic { get; set; }

    /// <summary>
    /// Original publish time of the message.
    /// </summary>
    public DateTime? PublishTime { get; set; }
}
