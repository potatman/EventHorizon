using System;
using EventHorizon.Abstractions.Models;
using EventHorizon.Abstractions.Models.TopicMessages;

namespace EventHorizon.EventStreaming.Pulsar.Models
{
    public class PulsarTopicConstants
    {
        public const string DefaultTenant = "public";
        public const string DefaultNamespace = "default";
        public const string MessageNamespace = "message";
        public const string DefaultTopicFormat = $"persistent://{EventHorizonConstants.AssemblyKey}/{EventHorizonConstants.TypeKey}-{EventHorizonConstants.MessageKey}/{EventHorizonConstants.TypeKey}-{EventHorizonConstants.MessageKey}";

        public const string Persistent = "persistent";
        public const string NonPersistent = "non-persistent";

        public const int HashKey = 65536;
        public static readonly Type[] MessageTypes = new[]
        {
            typeof(Request), typeof(Response), typeof(Command)
        };
    }
}
