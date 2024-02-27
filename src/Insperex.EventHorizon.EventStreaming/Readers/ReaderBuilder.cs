using System;
using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

namespace Insperex.EventHorizon.EventStreaming.Readers;

public class ReaderBuilder<TMessage> where TMessage : class, ITopicMessage, new()
{
    private readonly IStreamFactory _factory;
    private DateTime? _endDateTime;
    private bool _isBeginning = true;
    private DateTime? _startDateTime;
    private string[] _keys;
    private string _topic;
    private readonly Dictionary<string, Type> _typeDict = new();

    public ReaderBuilder(IStreamFactory factory)
    {
        _factory = factory;
    }

    public ReaderBuilder<TMessage> AddStateStream<TState>(string senderId = null) where TState : IState
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<ReaderBuilder<TMessage>>();

        // Add Types
        var stateType = typeof(TState);
        var stateDetails = ReflectionFactory.GetStateDetail(stateType);
        foreach (var type in stateDetails.ActionDict)
            _typeDict[type.Key] = type.Value;

        // Add Topics
        _topic = _factory.CreateAdmin<TMessage>().GetTopic(stateType, senderId);

        return this;
    }

    public ReaderBuilder<TMessage> AddStream<TAction>(string senderId = null) where TAction : IAction
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<ReaderBuilder<TMessage>>();

        var actionType = typeof(TAction);

        // Add Types
        var types = ReflectionFactory.GetTypeDetail(actionType).GetTypes<TAction>();
        foreach (var type in types)
            _typeDict[type.Name] = type;

        // Add Topics
        _topic = _factory.CreateAdmin<TMessage>().GetTopic(typeof(TAction), senderId);

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
