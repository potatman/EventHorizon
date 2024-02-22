using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
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


    public string GetTopic<TM>(Type type, string senderId = null) where TM : ITopicMessage
    {
        var persistent = EventStreamingConstants.Persistent;
        var pulsarAttr = _attributeUtil.GetOne<PulsarNamespaceAttribute>(type);
        var attribute = _attributeUtil.GetAll<StreamAttribute>(type).First(x => x.SubType == null);

        var tenant = pulsarAttr?.Tenant ?? PulsarTopicConstants.DefaultTenant;
        var @namespace = !PulsarTopicConstants.MessageTypes.Contains(typeof(TM))
            ? pulsarAttr?.Namespace ?? PulsarTopicConstants.DefaultNamespace
            : PulsarTopicConstants.MessageNamespace;

        var topic = senderId == null ? attribute.Topic : $"{attribute.Topic}-{senderId}";
        return $"{persistent}://{tenant}/{@namespace}/{topic}".Replace(PulsarTopicConstants.TypeKey, typeof(TM).Name);
    }
}
