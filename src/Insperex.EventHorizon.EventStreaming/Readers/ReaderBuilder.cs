using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Readers;

public class ReaderBuilder<T> where T : class, ITopicMessage, new()
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

    public ReaderBuilder<T> AddStateStream<TState>(string senderId = null) where TState : IState
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<ReaderBuilder<T>>();

        // Add Types
        var stateType = typeof(TState);
        var stateDetails = ReflectionFactory.GetStateDetail(stateType);
        foreach (var type in stateDetails.GetTypeDictWithGenericArg<T>())
            _typeDict[type.Key] = type.Value;

        // Add Topics
        _topic = _factory.GetTopicResolver().GetTopic<T>(stateType, senderId);

        return this;
    }

    public ReaderBuilder<T> AddStream<TAction>(string senderId = null) where TAction : IAction
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<ReaderBuilder<T>>();

        // Add Types
        var types = AssemblyUtil.GetTypes<TAction>();
        foreach (var type in types)
            _typeDict[type.Name] = type;

        // Add Topics
        _topic = _factory.GetTopicResolver().GetTopic<T>(typeof(TAction), senderId);

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
            TypeDict = _typeDict,
            Keys = _keys,
            StartDateTime = _startDateTime,
            EndDateTime = _endDateTime,
            IsBeginning = _isBeginning
        };
        var consumer = _factory.CreateReader<T>(config);

        return new Reader<T>(consumer);
    }
}
