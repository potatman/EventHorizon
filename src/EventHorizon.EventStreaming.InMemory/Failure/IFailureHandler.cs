using System.Collections.Generic;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.InMemory.Failure;

public interface IFailureHandler<TMessage>
    where TMessage : ITopicMessage
{
    bool InNormalMode(string topic, string streamId);

    MessageContext<TMessage>[] GetMessagesForRetry(int capacity);
    void FinalizeBatch(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks, Dictionary<string, long> maxIndexByTopic);
}
