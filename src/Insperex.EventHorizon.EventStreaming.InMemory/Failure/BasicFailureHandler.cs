using System.Collections.Generic;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;

namespace Insperex.EventHorizon.EventStreaming.InMemory.Failure;

public class BasicFailureHandler<T>: IFailureHandler<T> where T : class, ITopicMessage, new()
{
    private readonly SubscriptionConfig<T> _config;
    private readonly Dictionary<string, Queue<MessageContext<T>>> _backlogs = new();

    public BasicFailureHandler(SubscriptionConfig<T> config)
    {
        _config = config;

        foreach (var topic in _config.Topics)
        {
            _backlogs[topic] = new Queue<MessageContext<T>>();
        }
    }

    public bool InNormalMode(string topic, string streamId) => true;

    public MessageContext<T>[] GetMessagesForRetry(int capacity)
    {
        var backlogList = new List<MessageContext<T>>();

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

    public void FinalizeBatch(MessageContext<T>[] acks, MessageContext<T>[] nacks,
        Dictionary<string, long> maxIndexByTopic)
    {
        foreach (var message in nacks)
            _backlogs[message.TopicData.Topic].Enqueue(message);
    }
}
