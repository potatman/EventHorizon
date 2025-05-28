using System;
using System.Linq;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Models;

namespace EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicResolver : ITopicResolver
{
    private readonly AttributeUtil _attributeUtil;

    public InMemoryTopicResolver(AttributeUtil attributeUtil)
    {
        _attributeUtil = attributeUtil;
    }

    public string[] GetTopics<TM>(Type type, string topicName = null) where TM : ITopicMessage
    {
        var attributes = _attributeUtil.GetAll<StreamAttribute>(type);
        var topics = attributes
            .Select(x =>
            {
                var topic = topicName == null ? x.Topic : $"{x.Topic}-{topicName}";
                return $"in-memory://{typeof(TM).Name}/{topic}";
            })
            .ToArray();

        return topics;
    }
}
