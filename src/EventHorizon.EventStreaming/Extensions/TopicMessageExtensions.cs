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
        var upgraded = false;

        // Walk the whole chain (V1 -> V2 -> V3): handlers are only required for the
        // latest version (ValidationUtil skips IUpgradeTo types)
        while (true)
        {
            var upgrade = payload.GetType()
                .GetInterfaces()
                .FirstOrDefault(x => x.Name == typeof(IUpgradeTo<>).Name)?.GetMethod("Upgrade");

            var newPayload = upgrade?.Invoke(payload, null);
            if (newPayload == null || newPayload.GetType() == payload.GetType())
                break;

            payload = newPayload;
            upgraded = true;
        }

        // If no upgrade return original message
        if (!upgraded) return message;

        // Update in place to preserve envelope fields (e.g. Event.SequenceId)
        message.Type = payload.GetType().Name;
        message.Payload = JsonSerializer.Serialize(payload);
        return message;
    }
}
