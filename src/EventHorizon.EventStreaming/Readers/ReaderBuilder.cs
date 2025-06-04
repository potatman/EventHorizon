﻿using System;
using System.Linq;
using System.Threading;
using EventHorizon.Abstractions.Exceptions;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming.Readers;

public class ReaderBuilder<T> where T : class, ITopicMessage, new()
{
    private readonly IStreamFactory _factory;
    private readonly ILoggerFactory _loggerFactory;
    private DateTime? _endDateTime;
    private bool _isBeginning = true;
    private DateTime? _startDateTime;
    private string[] _keys;
    private string _topic;

    public ReaderBuilder(IStreamFactory factory, ILoggerFactory loggerFactory)
    {
        _factory = factory;
        _loggerFactory = loggerFactory;
    }

    public ReaderBuilder<T> AddStream<TS>(string topicName = null)
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<ReaderBuilder<T>>();
        _topic = _factory.GetTopicResolver().GetTopics<T>(typeof(TS), topicName).FirstOrDefault();
        return this;
    }

    public ReaderBuilder<T> Keys(params string[] keys)
    {
        _keys = keys;
        return this;
    }

    public ReaderBuilder<T> StartDateTime(DateTime? startDateTime)
    {
        _startDateTime = startDateTime;
        return this;
    }

    public ReaderBuilder<T> EndDateTime(DateTime? endDateTime)
    {
        _endDateTime = endDateTime;
        return this;
    }

    public ReaderBuilder<T> IsBeginning(bool isBeginning)
    {
        _isBeginning = isBeginning;
        return this;
    }

    public Reader<T> Build()
    {
        var config = new ReaderConfig
        {
            Topic = _topic,
            Keys = _keys,
            StartDateTime = _startDateTime,
            EndDateTime = _endDateTime,
            IsBeginning = _isBeginning
        };
        var consumer = _factory.CreateReader<T>(config);

        return new Reader<T>(consumer);
    }
}
