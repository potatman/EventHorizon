using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.InMemory.Databases;

public class MessageDatabase
{
    private readonly ConcurrentDictionary<string, List<object>> _messages = new();

    public void AddMessages<T>(PublisherConfig config, params T[] messages) where T : class, ITopicMessage
    {
        if (!_messages.ContainsKey(config.Topic))
            _messages[config.Topic] = new List<object>();

        foreach (var message in messages)
        {
            var topicData = new TopicData(_messages[config.Topic].Count.ToString(CultureInfo.InvariantCulture), config.Topic, DateTime.UtcNow);
            var context = new MessageContext<T>(message, topicData, config.TypeDict);

            _messages[config.Topic].Add(context);
        }
    }

    public MessageContext<T>[] GetMessages<T>(string topic, string[] streamIds, int startIndex = 0) where T : class, ITopicMessage
    {
        if (!_messages.ContainsKey(topic))
            _messages[topic] = new List<object>();

        return _messages[topic]
            .Skip(startIndex)
            .ToArray()
            .Cast<MessageContext<T>>()
            .Where(x => streamIds == null || streamIds.Contains(x.Data.StreamId))
            .ToArray();
    }

    public MessageContext<T>[] GetMessages<T>(string topic, int shard, int numOfShards, int start, int size)
        where T : ITopicMessage
    {
        if (!_messages.ContainsKey(topic))
            _messages[topic] = new List<object>();

        var messages = _messages[topic]
            .Skip(start)
            .Cast<MessageContext<T>>()
            .Where(x => x.Data.StreamId.Sum(s => s) % numOfShards == shard)
            .Take(size)
            .ToArray();

        return messages;
    }

    public int GetCount(string topic)
    {
        if (!_messages.ContainsKey(topic))
            _messages[topic] = new List<object>();

        return _messages[topic].Count;
    }

    public void DeleteTopic(string topic)
    {
        if(_messages.ContainsKey(topic))
            _messages.Remove(topic, out var value);
    }
}
