using System;
using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Publishers;

public class PublisherBuilder<TMessage> where TMessage : class, ITopicMessage, new()
{
    private readonly IStreamFactory _factory;
    private readonly ILoggerFactory _loggerFactory;
    private string _topic;
    private readonly Dictionary<string, Type> _typeDict = new();
    private TimeSpan _sendTimeout = TimeSpan.FromMinutes(2);
    private bool _isGuaranteed;
    private int _batchSize = 100;
    private bool _isOrderGuaranteed = true;

    public PublisherBuilder(IStreamFactory factory, ILoggerFactory loggerFactory)
    {
        _factory = factory;
        _loggerFactory = loggerFactory;
    }

    internal PublisherBuilder<TMessage> AddTopic(string topicName = null)
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<TMessage>>();
        _topic = topicName;
        return this;
    }

    public PublisherBuilder<TMessage> AddStateStream<TState>(string senderId = null) where TState : IState
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<TMessage>>();

        // Add Types
        var stateType = typeof(TState);
        var stateDetails = ReflectionFactory.GetStateDetail(stateType);
        foreach (var type in stateDetails.ActionDict)
            _typeDict[type.Key] = type.Value;

        // Add Topics
        _topic = _factory.CreateAdmin<TMessage>().GetTopic(stateType, senderId);

        return this;
    }

    public PublisherBuilder<TMessage> AddStream<TAction>(string senderId = null) where TAction : IAction
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<TMessage>>();

        var actionType = typeof(TAction);

        // Add Types
        var types = ReflectionFactory.GetTypeDetail(actionType).GetTypes<TAction>();
        foreach (var type in types)
            _typeDict[type.Name] = type;

        // Add Topics
        _topic = _factory.CreateAdmin<TMessage>().GetTopic(actionType, senderId);

        return this;
    }

    public PublisherBuilder<TMessage> IsGuaranteed(bool isGuaranteed)
    {
        _isGuaranteed = isGuaranteed;
        return this;
    }

    public PublisherBuilder<TMessage> IsOrderGuaranteed(bool isOrderGuaranteed)
    {
        _isOrderGuaranteed = isOrderGuaranteed;
        return this;
    }

    public PublisherBuilder<TMessage> SendTimeout(TimeSpan sendTimeout)
    {
        _sendTimeout = sendTimeout;
        return this;
    }

    public PublisherBuilder<TMessage> BatchSize(int batchSize)
    {
        _batchSize = batchSize;
        return this;
    }

    public Publisher<TMessage> Build()
    {
        var config = new PublisherConfig
        {
            Topic = _topic,
            TypeDict = _typeDict,
            IsGuaranteed = _isGuaranteed,
            IsOrderGuaranteed = _isOrderGuaranteed,
            SendTimeout = _sendTimeout,
            BatchSize = _batchSize
        };
        var logger = _loggerFactory.CreateLogger<Publisher<TMessage>>();

        // Create
        return new Publisher<TMessage>(_factory, config, logger);
    }
}
