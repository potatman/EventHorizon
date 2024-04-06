using System;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Serialization;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;

namespace Insperex.EventHorizon.Abstractions.Models.TopicMessages;

public class Request : ITopicMessage
{
    public string Id { get; set; }
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
    public Compression? Compression { get; set; }
    public byte[] Data { get; set; }
    public string ResponseTopic { get; set; }

    public Request()
    {

    }

    public Request(string streamId, object payload)
    {
        Id = Guid.NewGuid().ToString();
        StreamId = streamId;
        Payload = JsonSerializer.Serialize(payload);
        Type = payload.GetType().Name;
    }
}
