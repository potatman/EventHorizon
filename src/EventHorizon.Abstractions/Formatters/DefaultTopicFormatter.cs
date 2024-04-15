using EventHorizon.Abstractions.Models;

namespace EventHorizon.Abstractions.Formatters
{
    public class DefaultTopicFormatter : ITopicFormatter
    {
        public string GetFormat() => EventHorizonConstants.DefaultTopicFormat;
    }
}
