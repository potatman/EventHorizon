using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;

namespace Insperex.EventHorizon.Abstractions.Models.TopicMessages;

public class Response : ITopicMessage
{
    public string Id { get; set; }
    public string RequestId { get; set; }
    public string SenderId { get; set; }
    public string StreamId { get; set; }
    public string Type { get; set; }
    public string Payload { get; set; }
    public AggregateStatus Status { get; set; }
    public string Error { get; set; }
    
    public Response()
    {
    }

    public Response(string streamId, string requestId, string senderId, object payload)
    {
        StreamId = streamId;
        RequestId = requestId;
        SenderId = senderId;
        Payload = JsonSerializer.Serialize(payload);
        Type = payload?.GetType().Name;
    }
}