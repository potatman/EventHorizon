using System;
using System.Text;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.AdvancedFailure;

/// <summary>
/// Record of the state of one stream in a topic (in or out of failure mode.)
/// </summary>
public class TopicStreamState
{
    public string Topic { get; set; }
    public string StreamId { get; set; }
    public long LastSequenceId { get; set; }
    public DateTime LastMessagePublishTime { get; set; }
    public int TimesRetried { get; set; }
    /// <summary>
    /// If this field is null, then the last message has already succeeded and this stream/topic is in recovery.
    /// </summary>
    public DateTime? NextRetry { get; set; }
    /// <summary>
    /// If this is true, then the stream/topic is considered to be past the recovery stage and the "normal" phase
    /// consumer can pick up messages for it.
    /// </summary>
    public bool IsUpToDate { get; set; }
    /// <summary>
    /// If this is true, then this record is no longer active.
    /// </summary>
    public bool IsResolved { get; set; }

    public override string ToString() =>
        $"[{Topic}=>{StreamId}seq={LastSequenceId},pub={LastMessagePublishTime:s},retr#={TimesRetried},next={NextRetry:s},uptodt={IsUpToDate}]";
}
