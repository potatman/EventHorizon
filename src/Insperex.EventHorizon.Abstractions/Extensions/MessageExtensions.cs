using System;
using System.Collections.Generic;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.Abstractions.Extensions;

public static class MessageExtensions
{
    public static object GetPayload<TMessage>(this TMessage message, Dictionary<string, Type> types)
        where TMessage : ITopicMessage
    {
        var type = types.GetValueOrDefault(message.Type);
        return type == null? null : JsonSerializer.Deserialize(message.Payload, type);
    }
}
