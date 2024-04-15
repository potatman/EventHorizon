using System.Text.Json;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Serialization.Compression;

namespace EventHorizon.Abstractions.Models.TopicMessages;

public class Response : ITopicMessage
{
    public string Id { get; set; }
    public string Topic { get; set; }
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
    public Compression? Compression { get; set; }
    public byte[] Data { get; set; }
    public int StatusCode { get; set; }
    public string Error { get; set; }

    public Response()
    {
    }

    public Response(string id, string topic, string streamId, object payload, string error, int statusCode)
    {
        Id = id;
        StreamId = streamId;
        Topic = topic;
        Payload = JsonSerializer.Serialize(payload);
        Type = payload?.GetType().Name;
        Error = error;
        StatusCode = statusCode;
    }
}
