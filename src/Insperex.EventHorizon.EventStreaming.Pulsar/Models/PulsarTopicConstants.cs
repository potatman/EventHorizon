using System;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Models
{
    public class PulsarTopicConstants
    {
        public const string DefaultTenant = "public";
        public const string DefaultNamespace = "default";
        public const string MessageNamespace = "message";
        public const string TypeKey = "$type";

        public const int HashKey = 65536;
        public static readonly Type[] MessageTypes = new[]
        {
            typeof(Request), typeof(Response), typeof(Command)
        };
    }
}
