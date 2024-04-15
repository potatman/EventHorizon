using EventHorizon.Abstractions.Models;

namespace EventHorizon.Abstractions.Formatters
{
    public class DefaultDatabaseFormatter : IDatabaseFormatter
    {
        public string GetFormat() => EventHorizonConstants.DefaultDbFormat;
    }
}
