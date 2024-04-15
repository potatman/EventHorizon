using System.Linq;

namespace EventHorizon.Abstractions.Models
{
    public static class EventHorizonConstants
    {
        public const string TypeKey = "$payload";
        public const string MessageKey = "$message";
        public const string AssemblyKey = "$assembly";
        public static readonly string DefaultDbFormat = $"{AssemblyKey.Split(".").First()}_{MessageKey.ToLowerInvariant()}_{TypeKey.ToLowerInvariant()}";
        public static readonly string DefaultTopicFormat = $"{AssemblyKey.Split(".").First()}-{MessageKey.ToLowerInvariant()}-{TypeKey.ToLowerInvariant()}";
    }
}
