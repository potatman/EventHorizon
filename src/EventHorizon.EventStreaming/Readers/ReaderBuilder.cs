﻿using System;
using System.Collections.Generic;
using EventHorizon.Abstractions.Exceptions;
using EventHorizon.Abstractions.Extensions;
using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Reflection;
using EventHorizon.EventStreaming.Interfaces.Streaming;

namespace EventHorizon.EventStreaming.Readers;

public class ReaderBuilder<TMessage>
    where TMessage : ITopicMessage
{
    private readonly Formatter _resolver;
    private readonly IStreamFactory _factory;
    private DateTime? _endDateTime;
    private bool _isBeginning = true;
    private DateTime? _startDateTime;
    private string[] _keys;
    private string _topic;
    private readonly Dictionary<string, Type> _typeDict = new();
    private readonly Type _messageType;

    public ReaderBuilder(Formatter resolver, IStreamFactory factory)
    {
        _resolver = resolver;
        _factory = factory;
        _messageType = typeof(TMessage);
    }

    public ReaderBuilder<TMessage> AddStateStream<TState>(string senderId = null) where TState : IState
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<ReaderBuilder<TMessage>>();

        var stateType = typeof(TState);

        // Add Types and Topics
        _typeDict.AddRange(ReflectionFactory.GetStateDetail(stateType).MessageTypeDict[_messageType]);
        _topic = _resolver.GetTopic<TMessage>(stateType);

        return this;
    }

    public ReaderBuilder<TMessage> AddStream<TAction>(string senderId = null) where TAction : IAction
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<ReaderBuilder<TMessage>>();

        var actionType = typeof(TAction);

        // Add Types and Topics
        _typeDict.AddRange(ReflectionFactory.GetTypeDetail(actionType).GetTypes<TAction>());
        _topic = _resolver.GetTopic<TMessage>(actionType);

        return this;
    }

    public ReaderBuilder<TMessage> Keys(params string[] keys)
    {
        _keys = keys;
        return this;
    }

    public ReaderBuilder<TMessage> StartDateTime(DateTime? startDateTime)
    {
        _startDateTime = startDateTime;
        return this;
    }

    public ReaderBuilder<TMessage> EndDateTime(DateTime? endDateTime)
    {
        _endDateTime = endDateTime;
        return this;
    }

    public ReaderBuilder<TMessage> IsBeginning(bool isBeginning)
    {
        _isBeginning = isBeginning;
        return this;
    }

    public Reader<TMessage> Build()
    {
        var config = new ReaderConfig
        {
            Topic = _topic,
            TypeDict = _typeDict,
            Keys = _keys,
            StartDateTime = _startDateTime,
            EndDateTime = _endDateTime,
            IsBeginning = _isBeginning
        };
        var consumer = _factory.CreateReader<TMessage>(config);

        return new Reader<TMessage>(consumer);
    }
}