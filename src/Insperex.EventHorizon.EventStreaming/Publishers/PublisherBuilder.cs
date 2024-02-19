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

namespace Insperex.EventHorizon.EventStreaming.Publishers;

public class PublisherBuilder<T> where T : class, ITopicMessage, new()
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

    internal PublisherBuilder<T> AddTopic(string topicName = null)
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<T>>();
        _topic = topicName;
        return this;
    }

    public PublisherBuilder<T> AddStateStream<TState>(string senderId = null) where TState : IState
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<T>>();

        // Add Types
        var stateType = typeof(TState);
        var stateDetails = ReflectionFactory.GetStateDetail(stateType);
        foreach (var type in stateDetails.GetTypeDictWithGenericArg<T>())
            _typeDict[type.Key] = type.Value;

        // Add Topics
        _topic = _factory.GetTopicResolver().GetTopic<T>(stateType, senderId);

        return this;
    }

    public PublisherBuilder<T> AddStream<TAction>(string senderId = null) where TAction : IAction
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<T>>();

        // Add Types
        var types = AssemblyUtil.GetTypes<TAction>();
        foreach (var type in types)
            _typeDict[type.Name] = type;

        // Add Topics
        _topic = _factory.GetTopicResolver().GetTopic<T>(typeof(TAction), senderId);

        return this;
    }

    public PublisherBuilder<T> IsGuaranteed(bool isGuaranteed)
    {
        _isGuaranteed = isGuaranteed;
        return this;
    }

    public PublisherBuilder<T> IsOrderGuaranteed(bool isOrderGuaranteed)
    {
        _isOrderGuaranteed = isOrderGuaranteed;
        return this;
    }

    public PublisherBuilder<T> SendTimeout(TimeSpan sendTimeout)
    {
        _sendTimeout = sendTimeout;
        return this;
    }

    public PublisherBuilder<T> BatchSize(int batchSize)
    {
        _batchSize = batchSize;
        return this;
    }

    public Publisher<T> Build()
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
        var logger = _loggerFactory.CreateLogger<Publisher<T>>();

        // Create
        return new Publisher<T>(_factory, config, logger);
    }
}
