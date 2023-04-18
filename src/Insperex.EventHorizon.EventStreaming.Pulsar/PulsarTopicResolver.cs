using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicResolver : ITopicResolver
{
    private readonly AttributeUtil _attributeUtil;

    public PulsarTopicResolver(AttributeUtil attributeUtil)
    {
        _attributeUtil = attributeUtil;
    }

    public string[] GetTopics<TM>(Type type, string topicName = null) where TM : ITopicMessage
    {
        var messageType = typeof(TM);
        var persistent = messageType.Name == PulsarConstants.Event
            ? PulsarConstants.Persistent
            : PulsarConstants.NonPersistent;

        var attributes = _attributeUtil.GetAll<EventStreamAttribute>(type);
        var topics = attributes
            .Select(x => $"{persistent}://{x.BucketId}/{messageType.Name}s/{topicName ?? x.Type}")
            .ToArray();

        return topics;
    }
}