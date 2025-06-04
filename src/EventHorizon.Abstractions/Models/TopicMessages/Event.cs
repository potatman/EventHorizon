using System.Text.Json;
using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.Abstractions.Models.TopicMessages;

public class Event : ITopicMessage
{
    public long SequenceId { get; set; }
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }

    public Event()
    {
    }

    public Event(string streamId, object payload)
    {
        StreamId = streamId;
        Payload = JsonSerializer.Serialize(payload);
        Type = payload.GetType().Name;
    }

    public Event(string streamId, long sequenceId, object payload) : this(streamId, payload)
    {
        SequenceId = sequenceId;
    }
}
