using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces;

namespace Insperex.EventHorizon.EventStreaming.Extensions;

public static class TopicMessageExtensions
{
    public static object GetPayload<T>(this T message, Dictionary<string, Type> types)
        where T : class, ITopicMessage =>
        JsonSerializer.Deserialize(message.Payload, types[message.Type]);
}
