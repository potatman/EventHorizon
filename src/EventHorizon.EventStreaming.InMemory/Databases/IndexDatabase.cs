using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace EventHorizon.EventStreaming.InMemory.Databases;

public class IndexDatabase
{
    private readonly ConcurrentDictionary<string, long> _indexes = new();
    private readonly MessageDatabase _messageDatabase;

    public IndexDatabase(MessageDatabase messageDatabase)
    {
        _messageDatabase = messageDatabase;
    }

    public void Setup(string topic, string subscription, int consumer, bool isBeginning)
    {
        var key = $"{topic}-{subscription}-{consumer}";
        if (_indexes.ContainsKey(key)) return;

        _indexes[key] = isBeginning == false ? _messageDatabase.GetCount(topic) : 0;
    }

    public long GetCurrentSequence(string topic, string subscription, int consumer)
    {
        return _indexes.GetValueOrDefault($"{topic}-{subscription}-{consumer}");
    }

    public void SetCurrentSequence(string topic, string subscription, int consumer, long sequence)
    {
        _indexes[$"{topic}-{subscription}-{consumer}"] = sequence;
    }

    public void DeleteTopic(string str)
    {
        var keys = _indexes.Keys.Where(x => x.StartsWith(str, StringComparison.InvariantCulture)).ToArray();
        foreach (var key in keys)
            _indexes.Remove(key, out var value);
    }
}
