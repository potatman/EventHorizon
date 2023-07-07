using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.Abstractions.Models.TopicMessages;

public class Response : ITopicMessage
{
    public string Id { get; set; }
    public string SenderId { get; set; }
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
    public int StatusCode { get; set; }
    public string Error { get; set; }

    public Response()
    {
    }

    public Response(string id, string senderId, string streamId, object payload, string error, int statusCode)
    {
        Id = id;
        StreamId = streamId;
        SenderId = senderId;
        Payload = JsonSerializer.Serialize(payload);
        Type = payload?.GetType().Name;
        Error = error;
        StatusCode = statusCode;
    }
}
