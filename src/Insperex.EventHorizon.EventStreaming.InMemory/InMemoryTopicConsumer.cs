using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.InMemory.Databases;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Util;

namespace Insperex.EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicConsumer<T> : ITopicConsumer<T> where T : class, ITopicMessage
{
    private readonly Dictionary<string, Queue<MessageContext<T>>> _backlogs = new();
    private readonly SubscriptionConfig<T> _config;
    private readonly IndexDatabase _indexDatabase;
    private readonly MessageDatabase _messageDatabase;
    private readonly Dictionary<string, int> _consumers = new();
    private readonly ConsumerDatabase _consumerDatabase;

    public InMemoryTopicConsumer(
        SubscriptionConfig<T> config,
        MessageDatabase messageDatabase,
        IndexDatabase indexDatabase,
        ConsumerDatabase consumerDatabase)
    {
        _config = config;
        _messageDatabase = messageDatabase;
        _indexDatabase = indexDatabase;
        _consumerDatabase = consumerDatabase;

        foreach (var topic in _config.Topics)
        {
            _backlogs[topic] = new Queue<MessageContext<T>>();
            _consumers[topic] = consumerDatabase.Register(topic, _config.SubscriptionName, NameUtil.AssemblyNameWithGuid);
            _indexDatabase.Setup(topic, _config.SubscriptionName, _consumers[topic], _config.IsBeginning != false);
        }

        // Wait for other consumers to register
        Thread.Sleep(500);
    }

    public Task InitAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<MessageContext<T>[]> NextBatchAsync(CancellationToken ct)
    {
        var list = new List<MessageContext<T>>();
        var batchSize = _config.BatchSize ?? 1000;

        // Ensure Registration is done
        await Task.Delay(1000, ct);

        // Pull from Main
        foreach (var topic in _config.Topics)
        {
            var index = (int)_indexDatabase.GetCurrentSequence(topic, _config.SubscriptionName, _consumers[topic]);
            var count = batchSize - list.Count;
            var consumerCount = _consumerDatabase.Count(topic, _config.SubscriptionName);
            var messages = _messageDatabase.GetMessages<T>(topic, _consumers[topic], consumerCount, index, count);

            list.AddRange(messages);

            // If Size Hit Return List
            if (list.Count >= batchSize)
                return list.ToArray();
        }

        // Pull from backlog
        foreach (var topic in _config.Topics)
        {
            var size = batchSize - list.Count;
            for (var i = 0; i < size; i++)
                if (_backlogs[topic].TryDequeue(out var message))
                    list.Add(message);

            // If Size Hit Return List
            if (list.Count >= batchSize)
                return list.ToArray();
        }

        // Delay if no messages
        return !list.Any() ? null : list.ToArray();
    }

    public Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        var topics = acks.Concat(nacks)
            .GroupBy(x => x.TopicData.Topic).ToArray();
        foreach (var topic in topics)
            _indexDatabase.Increment(topic.Key, _config.SubscriptionName, _consumers[topic.Key], topic.Count());

        foreach (var message in nacks)
            _backlogs[message.TopicData.Topic].Enqueue(message);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _backlogs.Clear();
        _consumers.Clear();
    }

    public ValueTask DisposeAsync()
    {
        _backlogs.Clear();
        _consumers.Clear();
        return ValueTask.CompletedTask;
    }
}
