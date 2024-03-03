using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.Abstractions.Models;

public class MessageContext<TMessage>
    where TMessage : ITopicMessage
{
    public TMessage Data { get; set; }
    public TopicData TopicData { get; set; }
    public Dictionary<string, Type> TypeDict { get; set; }

    public MessageContext(TMessage data, TopicData topicData, Dictionary<string, Type> typeDict)
    {
        Data = data;
        TopicData = topicData;
        TypeDict = typeDict;
    }

    public object GetPayload() => JsonSerializer.Deserialize(Data.Payload, TypeDict[Data.Type]);

    public TMessage Upgrade()
    {
        var payload = GetPayload();
        var upgrade = TypeDict[Data.Type]
            .GetInterfaces()
            .FirstOrDefault(x => x.Name == typeof(IUpgradeTo<>).Name)?.GetMethod("Upgrade");

        // If no upgrade return original message
        if (upgrade == null) return Data;

        upgrade?.Invoke(payload, null);
        return (TMessage)Activator.CreateInstance(typeof(TMessage), Data.StreamId, payload);

    }
}
