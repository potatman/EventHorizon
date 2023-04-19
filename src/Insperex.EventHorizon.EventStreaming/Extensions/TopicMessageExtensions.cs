using System;
using System.Linq;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.Abstractions.Util;
using Insperex.EventHorizon.EventStreaming.Interfaces;

namespace Insperex.EventHorizon.EventStreaming.Extensions;

public static class TopicMessageExtensions
{
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