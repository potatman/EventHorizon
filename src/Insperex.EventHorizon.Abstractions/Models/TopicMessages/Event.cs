using System;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;

namespace Insperex.EventHorizon.Abstractions.Models.TopicMessages;

public class Event : ITopicMessage
{
    public long SequenceId { get; set; }
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
    public Compression? Compression { get; set; }
    public byte[] Data { get; set; }

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
