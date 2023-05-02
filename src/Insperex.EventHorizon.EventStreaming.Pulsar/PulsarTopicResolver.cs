using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Models;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;

namespace Insperex.EventHorizon.EventStreaming.Pulsar;

public class PulsarTopicResolver : ITopicResolver
{
    private readonly AttributeUtil _attributeUtil;
    private const string DefaultTenant = "public";
    private const string DefaultNamespace = "default";

    public PulsarTopicResolver(AttributeUtil attributeUtil)
    {
        _attributeUtil = attributeUtil;
    }

    public string[] GetTopics<TM>(Type type, string topicName = null) where TM : ITopicMessage
    {
        var persistent = typeof(TM).Name == EventStreamingConstants.Event
            ? EventStreamingConstants.Persistent
            : EventStreamingConstants.NonPersistent;

        var attributes = _attributeUtil.GetAll<StreamAttribute<TM>>(type);
        var topics = attributes
            .Select(x =>
            {
                var tenant = x.Tenant ?? DefaultTenant;
                var @namespace = x.Namespace ?? DefaultNamespace;
                var topic = topicName == null ? x.Topic : $"{x.Topic}-{topicName}";
                return $"{persistent}://{tenant}/{@namespace}/{topic}";
            })
            .ToArray();

        if (!topics.Any())
            throw new Exception($"{type.Name} is Missing [StreamAttribute<{typeof(TM).Name}>]");

        return topics;
    }
}
