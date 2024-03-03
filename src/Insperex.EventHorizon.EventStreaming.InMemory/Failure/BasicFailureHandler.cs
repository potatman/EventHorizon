using System;
using System.Collections.Generic;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.InMemory.Failure;

public class BasicFailureHandler<TMessage>: IFailureHandler<TMessage>
    where TMessage : ITopicMessage
{
    private readonly SubscriptionConfig<TMessage> _config;
    private readonly Dictionary<string, Queue<MessageContext<TMessage>>> _backlogs = new();

    public BasicFailureHandler(SubscriptionConfig<TMessage> config)
    {
        _config = config;

        foreach (var topic in _config.Topics)
        {
            _backlogs[topic] = new Queue<MessageContext<TMessage>>();
        }
    }

    public bool InNormalMode(string topic, string streamId) => true;

    public MessageContext<TMessage>[] GetMessagesForRetry(int capacity)
    {
        var backlogList = BuildBacklogBuffer(capacity);

        foreach (var topic in _config.Topics)
        {
            var remainingCapacity = capacity - backlogList.Count;

            for (var i = 0; i < remainingCapacity; i++)
                if (_backlogs[topic].TryDequeue(out var message))
                    backlogList.Add(message);

            if (backlogList.Count >= capacity)
                break;
        }

        return backlogList.ToArray();
    }

    private List<MessageContext<TMessage>> BuildBacklogBuffer(int capacity)
    {
        var totalBackloggedItemCount = _backlogs.Values.Sum(bl => bl.Count);
        var backlogList = new List<MessageContext<TMessage>>(Math.Min(capacity, totalBackloggedItemCount));
        return backlogList;
    }

    public void FinalizeBatch(MessageContext<TMessage>[] acks, MessageContext<TMessage>[] nacks,
        Dictionary<string, long> maxIndexByTopic)
    {
        foreach (var message in nacks)
            _backlogs[message.TopicData.Topic].Enqueue(message);
    }
}
