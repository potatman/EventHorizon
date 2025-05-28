using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace EventHorizon.EventStreaming.InMemory.Databases;

public class ConsumerDatabase
{
    private readonly List<string> _consumers = new();
    private readonly ConcurrentDictionary<string, int> _sharedKeys = new();

    public int Register(string topic, string subscription, string consumer)
    {
        var key = $"{topic}-{subscription}-{consumer}";
        if (_consumers.Contains(key)) return _consumers.IndexOf(key);

        _consumers.Add(key);

        // Increment
        var key2 = $"{topic}-{subscription}";
        if (!_sharedKeys.ContainsKey(key2))
            _sharedKeys[key2] = 0;
        _sharedKeys[key2]++;

        return _sharedKeys[key2] - 1;
    }

    public int Count(string topic, string subscription)
    {
        return _sharedKeys[$"{topic}-{subscription}"];
    }

    public void DeleteTopic(string str)
    {
        var consumers = _consumers.Where(x => x.StartsWith(str, StringComparison.InvariantCulture)).ToArray();
        foreach (var consumer in consumers)
            _consumers.Remove(consumer);

        var keys = _sharedKeys.Keys.Where(x => x.StartsWith(str, StringComparison.InvariantCulture)).ToArray();
        foreach (var key in keys)
            _sharedKeys.Remove(key, out var value);
    }
}
