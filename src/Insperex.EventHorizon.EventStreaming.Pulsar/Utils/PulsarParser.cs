using Insperex.EventHorizon.EventStreaming.Pulsar.Models;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Utils;

public static class PulsarTopicParser
{
    private const string ForwardSlash = "/";
    private const string Break = "://";
    private const string Public = "public";
    private const string Default = "default";
    private const string Persistent = "persistent";

    public static PulsarTopic Parse(string topic)
    {
        if (!topic.Contains(Break))
            return new PulsarTopic
            {
                IsPersisted = true,
                Tenant = Public,
                Namespace = Default,
                Topic = topic
            };

        var parts1 = topic.Split(Break);
        var parts2 = parts1[1].Split(ForwardSlash);
        return new PulsarTopic
        {
            IsPersisted = parts1[0] == Persistent,
            Tenant = parts2[0],
            Namespace = parts2[1],
            Topic = parts2[2]
        };
    }
}