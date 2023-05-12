namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Used in messages for the <see cref="FailureStateTopic{T}"/> to indicate
/// the basic status change taking place in each message.
/// </summary>
public enum StreamFailureStateEventType
{
    /// <summary>
    /// A message from this stream has failed to process, either for the first time or on retry
    /// </summary>
    /// <remarks>
    /// This will cause the stream to enter (or remain in) Failed state.
    /// </remarks>
    MessageProcessingFailed,

    /// <summary>
    /// A previously-failed message has now succeeded.
    /// </summary>
    /// <remarks>
    /// This will cause the stream to enter recovery state.
    /// </remarks>
    MessageProcessingSucceeded,

    /// <summary>
    /// One or more messages from the <see cref="MessageRecoveryTopic{T}"/> have been processed
    /// for this stream.
    /// </summary>
    RecoveryMessagesProcessed,

    /// <summary>
    /// All of the stream's recovery messages have been processed.
    /// </summary>
    /// <remarks>
    /// This will cause the stream to enter normal state.
    /// </remarks>
    RecoveryComplete,
}
