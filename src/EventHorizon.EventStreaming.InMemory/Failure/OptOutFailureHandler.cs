using System;
using System.Collections.Generic;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.InMemory.Failure;

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
