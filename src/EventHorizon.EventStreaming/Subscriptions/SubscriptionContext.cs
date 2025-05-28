using System.Collections.Generic;
using System.Threading;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionContext<T>
    where T : ITopicMessage
{
    internal readonly List<MessageContext<T>> AckList = new();
    internal readonly List<MessageContext<T>> NackList = new();
    public MessageContext<T>[] Messages { get; set; }
    public CancellationToken CancellationToken { get; set; }

    public void Ack(params MessageContext<T>[] messages)
    {
        AckList.AddRange(messages);
    }

    public void Nack(params MessageContext<T>[] messages)
    {
        NackList.AddRange(messages);
    }
}
