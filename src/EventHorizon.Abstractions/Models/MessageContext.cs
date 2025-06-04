using EventHorizon.Abstractions.Interfaces.Internal;

namespace EventHorizon.Abstractions.Models;

public class MessageContext<T> where T : ITopicMessage
{
    public T Data { get; set; }
    public TopicData TopicData { get; set; }
}
