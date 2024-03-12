using Insperex.EventHorizon.Abstractions.Formatters;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;

namespace Insperex.EventHorizon.EventStreaming.Pulsar
{
    public class PulsarTopicFormatter : ITopicFormatter
    {
        public string GetFormat() => PulsarTopicConstants.DefaultTopicFormat;
    }
}
