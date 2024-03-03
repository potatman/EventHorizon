using System;
using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.InMemory.Failure;

public class OptOutFailureHandler<TMessage>: IFailureHandler<TMessage>
    where TMessage : ITopicMessage
{
    public bool InNormalMode(string topic, string streamId) => true;

    public MessageContext<TMessage>[] GetMessagesForRetry(int capacity) => Array.Empty<MessageContext<TMessage>>();

    public void FinalizeBatch(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks,
        Dictionary<string, long> maxIndexByTopic)
    {
    }
}
