using System.Collections.Generic;
using System.Threading;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.Subscriptions;

public class SubscriptionContext<TMessage>
    where TMessage : ITopicMessage
{
    internal readonly List<MessageContext<TMessage>> AckList = new();
    internal readonly List<MessageContext<TMessage>> NackList = new();
    public MessageContext<TMessage>[] Messages { get; set; }
    public CancellationToken CancellationToken { get; set; }

    public void Ack(params MessageContext<TMessage>[] messages)
    {
        AckList.AddRange(messages);
    }

    public void Nack(params MessageContext<TMessage>[] messages)
    {
        NackList.AddRange(messages);
    }
}
