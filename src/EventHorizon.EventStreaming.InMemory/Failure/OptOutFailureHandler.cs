using System;
using System.Collections.Generic;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.InMemory.Failure;

public class OptOutFailureHandler<T>: IFailureHandler<T> where T : class, ITopicMessage, new()
{
    public bool InNormalMode(string topic, string streamId) => true;

    public MessageContext<T>[] GetMessagesForRetry(int capacity) => Array.Empty<MessageContext<T>>();

    public void FinalizeBatch(MessageContext<T>[] acks, MessageContext<T>[] nacks,
        Dictionary<string, long> maxIndexByTopic)
    {
    }
}
