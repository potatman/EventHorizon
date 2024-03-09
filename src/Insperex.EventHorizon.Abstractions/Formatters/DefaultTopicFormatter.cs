using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.Abstractions.Formatters
{
    public class DefaultTopicFormatter : ITopicFormatter
    {
        public string GetFormat() => EventHorizonConstants.DefaultTopicFormat;
    }
}
