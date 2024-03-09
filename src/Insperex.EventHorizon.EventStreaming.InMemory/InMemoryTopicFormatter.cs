using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventStreaming.InMemory
{
    public class InMemoryTopicFormatter : ITopicFormatter
    {
        public string GetFormat() => $"in-memory://{EventHorizonConstants.AssemblyKey}/{EventHorizonConstants.TypeKey}/{EventHorizonConstants.MessageKey}";
    }
}
