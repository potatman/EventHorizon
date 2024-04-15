using System;
using System.Text.Json;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Serialization.Compression;

namespace EventHorizon.Abstractions.Models.TopicMessages;

public class Command : ITopicMessage
{
    public string Id { get; set; }
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
    public Compression? Compression { get; set; }
    public byte[] Data { get; set; }

    public Command()
    {
        Id = Guid.NewGuid().ToString();
    }

    public Command(string streamId, object payload)
    {
        Id = Guid.NewGuid().ToString();
        StreamId = streamId;
        Payload = JsonSerializer.Serialize(payload);
        Type = payload.GetType().Name;
    }
}
