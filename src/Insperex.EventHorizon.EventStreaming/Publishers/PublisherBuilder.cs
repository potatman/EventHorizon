using System;
using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Exceptions;
using Insperex.EventHorizon.Abstractions.Extensions;
using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Reflection;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Publishers;

public class PublisherBuilder<TMessage>
    where TMessage : class, ITopicMessage
{
    private readonly Formatter _formatter;
    private readonly IStreamFactory _factory;
    private readonly ILoggerFactory _loggerFactory;
    private string _topic;
    private readonly Dictionary<string, Type> _typeDict = new();
    private TimeSpan _sendTimeout = TimeSpan.FromMinutes(2);
    private bool _isGuaranteed;
    private int _batchSize = 100;
    private bool _isOrderGuaranteed = true;
    private readonly Type _messageType;
    private Compression? _compressionType;

    public PublisherBuilder(Formatter formatter, IStreamFactory factory, ILoggerFactory loggerFactory)
    {
        _formatter = formatter;
        _factory = factory;
        _loggerFactory = loggerFactory;
        _messageType = typeof(TMessage);
    }

    public PublisherBuilder<TMessage> AddTopic(string topicName = null)
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<TMessage>>();
        _topic = topicName;
        return this;
    }

    public PublisherBuilder<TMessage> AddStateStream<TState>() where TState : IState
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<TMessage>>();

        var stateType = typeof(TState);

        // Add Types and Topics
        _typeDict.AddRange(ReflectionFactory.GetStateDetail(stateType).MessageTypeDict[_messageType]);
        _topic = _formatter.GetTopic<TMessage>(stateType);

        return this;
    }

    public PublisherBuilder<TMessage> AddStream<TAction>() where TAction : IAction
    {
        if (_topic != null) throw new MultiTopicNotSupportedException<PublisherBuilder<TMessage>>();

        var actionType = typeof(TAction);

        // Add Types and Topics
        _typeDict.AddRange(ReflectionFactory.GetTypeDetail(actionType).GetTypes<TAction>());
        _topic = _formatter.GetTopic<TMessage>(actionType);

        return this;
    }

    public PublisherBuilder<TMessage> AddCompression(Compression? compressionType)
    {
        _compressionType = compressionType;
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
            BatchSize = _batchSize,
            CompressionType = _compressionType,
        };
        var logger = _loggerFactory.CreateLogger<Publisher<TMessage>>();

        // Create
        return new Publisher<TMessage>(_factory, config, logger);
    }
}
