using Insperex.EventHorizon.Abstractions.Formatters;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Models
{
    public class PulsarTopicFormatter : ITopicFormatter
    {
        public string GetFormat() => PulsarTopicConstants.DefaultTopicFormat;
    }
}
