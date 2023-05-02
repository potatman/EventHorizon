using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Models;

namespace Insperex.EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicResolver : ITopicResolver
{
    private readonly AttributeUtil _attributeUtil;

    public InMemoryTopicResolver(AttributeUtil attributeUtil)
    {
        _attributeUtil = attributeUtil;
    }

    public string[] GetTopics<TM>(Type type, string topicName = null) where TM : ITopicMessage
    {
        var attributes = _attributeUtil.GetAll<StreamAttribute<TM>>(type);
        var topics = attributes
            .Select(x =>
            {
                var tenant = x.Tenant ?? "public";
                var @namespace = x.Namespace ?? "default";
                var topic = topicName == null ? x.Topic : $"{x.Topic}-{topicName}";
                return $"in-memory://{tenant}/{@namespace}/{topic}";
            })
            .ToArray();

        if (!topics.Any())
            throw new Exception($"{type.Name} is Missing [StreamAttribute<{typeof(TM).Name}>]");

        return topics;
    }
}
