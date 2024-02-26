using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;

namespace Insperex.EventHorizon.EventStreaming.InMemory;

public class InMemoryTopicResolver : ITopicResolver
{
    private readonly AttributeUtil _attributeUtil;

    public InMemoryTopicResolver(AttributeUtil attributeUtil)
    {
        _attributeUtil = attributeUtil;
    }

    public string GetTopic<TM>(Type type, string senderId = null) where TM : ITopicMessage
    {
        var attribute = _attributeUtil.GetAll<StreamAttribute>(type).FirstOrDefault(x => x.SubType == null);
        if (attribute == null) return null;

        var topic = senderId == null ? attribute.Topic : $"{attribute.Topic}-{senderId}";
        return $"in-memory://{typeof(TM).Name}/{topic}".Replace("$type", typeof(TM).Name);
    }
}
