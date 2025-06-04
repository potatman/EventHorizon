using System;
using System.Linq;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Models;
using EventHorizon.EventStreaming.Pulsar.Attributes;
using EventHorizon.EventStreaming.Pulsar.Models;

namespace EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicResolver : ITopicResolver
{
    private readonly AttributeUtil _attributeUtil;

    public PulsarTopicResolver(AttributeUtil attributeUtil)
    {
        _attributeUtil = attributeUtil;
    }

    public string[] GetTopics<TM>(Type type, string topicName = null) where TM : ITopicMessage
    {
        var persistent = EventStreamingConstants.Persistent;
        var pulsarAttr = _attributeUtil.GetOne<PulsarNamespaceAttribute>(type);
        var attributes = _attributeUtil.GetAll<StreamAttribute>(type);
        var topics = attributes
            .Select(x =>
            {
                var tenant = pulsarAttr?.Tenant ?? PulsarTopicConstants.DefaultTenant;
                var @namespace = !PulsarTopicConstants.MessageTypes.Contains(typeof(TM))
                    ? pulsarAttr?.Namespace ?? PulsarTopicConstants.DefaultNamespace
                    : PulsarTopicConstants.MessageNamespace;
                var topic = topicName == null ? x.Topic : $"{x.Topic}-{topicName}";
                return $"{persistent}://{tenant}/{@namespace}/{topic}".Replace(PulsarTopicConstants.TypeKey, typeof(TM).Name);
            })
            .ToArray();

        return topics;
    }
}
