using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.InMemory.Failure;

public interface IFailureHandler<TMessage>
    where TMessage : ITopicMessage
{
    bool InNormalMode(string topic, string streamId);

    MessageContext<TMessage>[] GetMessagesForRetry(int capacity);
    void FinalizeBatch(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks, Dictionary<string, long> maxIndexByTopic);
}
