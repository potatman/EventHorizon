using System;
using System.Collections.Generic;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;

namespace Insperex.EventHorizon.EventStreaming.Extensions;

public static class TopicMessageExtensions
{
    public static object GetPayload<T>(this T message, Dictionary<string, Type> types)
        where T : class, ITopicMessage =>
        JsonSerializer.Deserialize(message.Payload, types[message.Type]);
}
