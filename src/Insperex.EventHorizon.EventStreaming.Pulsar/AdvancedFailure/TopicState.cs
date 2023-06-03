using System;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

public sealed class TopicState
{
    public string TopicName { get; set; }
    public long LastSequenceId { get; set; }
    public DateTime LastMessagePublishTime { get; set; }
    public int TimesRetried { get; set; }
    /// <summary>
    /// If this field is null, then the last message has already succeeded and this stream/topic is in recovery.
    /// </summary>
    public DateTime? NextRetry { get; set; }

    public string ToString(bool includeName)
    {
        var nameStr = includeName ? $"name={TopicName}," : string.Empty;
        return $"[{nameStr}seq={LastSequenceId},pub={LastMessagePublishTime:s},retr#={TimesRetried},next={NextRetry:s}]";
    }
}
