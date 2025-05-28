using System.Collections.Generic;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.InMemory.Failure;

public interface IFailureHandler<T> where T: class, ITopicMessage, new()
{
    bool InNormalMode(string topic, string streamId);

    MessageContext<T>[] GetMessagesForRetry(int capacity);
    void FinalizeBatch(MessageContext<T>[] acks, MessageContext<T>[] nacks, Dictionary<string, long> maxIndexByTopic);
}
