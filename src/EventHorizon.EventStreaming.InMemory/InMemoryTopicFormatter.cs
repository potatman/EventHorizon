using EventHorizon.Abstractions.Formatters;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventStreaming.InMemory
{
    public class InMemoryTopicFormatter : ITopicFormatter
    {
        public string GetFormat() => $"in-memory://{EventHorizonConstants.AssemblyKey}/{EventHorizonConstants.TypeKey}/{EventHorizonConstants.MessageKey}";
    }
}
