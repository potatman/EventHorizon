using System;
using System.Linq;
using System.Text.Json;
using EventHorizon.Abstractions.Interfaces.Internal;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Interfaces;

namespace EventHorizon.EventStreaming.Extensions;

public static class TopicMessageExtensions
{
    public static object GetPayload<T>(this T message)
        where T : class, ITopicMessage =>
        JsonSerializer.Deserialize(message.Payload, AssemblyUtil.ActionDict[message.Type]);

    public static T Upgrade<T>(this T message)
        where T : class, ITopicMessage
    {
        var payload = message.GetPayload();
        var upgrade = AssemblyUtil.ActionDict[message.Type]
            .GetInterfaces()
            .FirstOrDefault(x => x.Name == typeof(IUpgradeTo<>).Name)?.GetMethod("Upgrade");

        // If no upgrade return original message
        if (upgrade == null) return message;

        upgrade?.Invoke(payload, null);
        return Activator.CreateInstance(typeof(T), message.StreamId, payload) as T;

    }
}
