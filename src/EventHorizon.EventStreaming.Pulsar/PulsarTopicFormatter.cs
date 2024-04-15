using EventHorizon.Abstractions.Formatters;
using EventHorizon.EventStreaming.Pulsar.Models;

namespace EventHorizon.EventStreaming.Pulsar
{
    public class PulsarTopicFormatter : ITopicFormatter
    {
        public string GetFormat() => PulsarTopicConstants.DefaultTopicFormat;
    }
}
