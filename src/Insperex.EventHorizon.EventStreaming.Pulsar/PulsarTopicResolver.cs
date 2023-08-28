using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Attributes;
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
