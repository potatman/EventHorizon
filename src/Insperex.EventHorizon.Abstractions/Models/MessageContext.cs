using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.Abstractions.Models;

public class MessageContext<T> where T : ITopicMessage
{
    public T Data { get; set; }
    public TopicData TopicData { get; set; }
}