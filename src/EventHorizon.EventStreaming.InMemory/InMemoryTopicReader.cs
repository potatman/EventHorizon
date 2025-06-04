﻿using System;
using System.Linq;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models;
using EventHorizon.EventStreaming.InMemory.Databases;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Readers;

namespace EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicReader<T> : ITopicReader<T> where T : class, ITopicMessage
{
    private readonly ReaderConfig _config;
    private readonly MessageDatabase _messageDatabase;
    private int _index;

    public InMemoryTopicReader(ReaderConfig config, MessageDatabase messageDatabase)
    {
        _config = config;
        _messageDatabase = messageDatabase;
        _index = 0;
    }

    public Task<MessageContext<T>[]> GetNextAsync(int batchSize, TimeSpan timeout)
    {
        var messages = _messageDatabase.GetMessages<T>(_config.Topic, _config.Keys);
        var results = messages.SkipLast(_index).Take(batchSize).ToArray();
        _index += batchSize;
        return Task.FromResult(results);
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
