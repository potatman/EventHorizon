using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.InMemory.Databases;
using EventHorizon.EventStreaming.InMemory.Failure;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Subscriptions;
using EventHorizon.EventStreaming.Util;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicConsumer<T> : ITopicConsumer<T> where T : class, ITopicMessage, new()
{
    private readonly Dictionary<string, Queue<MessageContext<T>>> _backlogs = new();
    private readonly SubscriptionConfig<T> _config;
    private readonly IndexDatabase _indexDatabase;
    private readonly MessageDatabase _messageDatabase;
    private readonly Dictionary<string, int> _consumers = new();
    private readonly ConsumerDatabase _consumerDatabase;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IFailureHandler<T> _failureHandler;
    private readonly object _batchInProgressLock = new object();
    private bool _batchInProgress;

    // Volatile state.
    private Dictionary<string, long> _maxIndexByTopic = new();

    public InMemoryTopicConsumer(
        SubscriptionConfig<T> config,
        MessageDatabase messageDatabase,
        IndexDatabase indexDatabase,
        ConsumerDatabase consumerDatabase,
        FailureHandlerFactory failureHandlerFactory,
        ILoggerFactory loggerFactory)
    {
        _config = config;
        _messageDatabase = messageDatabase;
        _indexDatabase = indexDatabase;
        _consumerDatabase = consumerDatabase;
        _loggerFactory = loggerFactory;
        _failureHandler = failureHandlerFactory.Create(_config);

        foreach (var topic in _config.Topics)
        {
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
        lock (_batchInProgressLock)
        {
            if (_batchInProgress)
                return Array.Empty<MessageContext<T>>();
            _batchInProgress = true;
        }

        var list = new List<MessageContext<T>>();
        _maxIndexByTopic.Clear();
        var batchSize = _config.BatchSize ?? 1000;

        // Ensure Registration is done
        await Task.Delay(1000, ct);

        // Pull from backlog
        var backlogItems = _failureHandler.GetMessagesForRetry(batchSize);

        // Pull from Main
        var remainingBatchCapacity = batchSize - backlogItems.Length;
        foreach (var topic in _config.Topics)
        {
            if (list.Count >= remainingBatchCapacity) break;

            var index = (int)_indexDatabase.GetCurrentSequence(topic, _config.SubscriptionName, _consumers[topic]);
            var count = remainingBatchCapacity - list.Count;
            var consumerCount = _consumerDatabase.Count(topic, _config.SubscriptionName);
            var messages = _messageDatabase
                .GetMessages<T>(topic, _consumers[topic], consumerCount, index, count)
                .Where(m => _failureHandler.InNormalMode(topic, m.Data.StreamId));

            list.AddRange(messages);
        }

        // Record how far we got in each topic.
        var topics = list.GroupBy(x => x.TopicData.Topic).ToArray();
        foreach (var topic in topics)
        {
            var maxIndex = topic
                .Select(m => long.Parse(m.TopicData.Id, CultureInfo.InvariantCulture))
                .Max();
            _maxIndexByTopic[topic.Key] = maxIndex;
        }

        if (backlogItems.Any())
            list.AddRange(backlogItems);

            // Delay if no messages
        if (!list.Any())
        {
            await Task.Delay(_config.NoBatchDelay, ct);
            return null;
        }

        return list.ToArray();
    }

    public Task FinalizeBatchAsync(MessageContext<T>[] acks, MessageContext<T>[] nacks)
    {
        _failureHandler.FinalizeBatch(acks, nacks, _maxIndexByTopic);

        foreach (var topicName in _maxIndexByTopic.Keys)
        {
            _indexDatabase.SetCurrentSequence(topicName, _config.SubscriptionName, _consumers[topicName],
                _maxIndexByTopic[topicName] + 1);
        }

        lock (_batchInProgressLock)
        {
            _batchInProgress = false;
        }

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
