using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.Abstractions.Formatters
{
    public class DefaultDatabaseFormatter : IDatabaseFormatter
    {
        public string GetFormat() => EventHorizonConstants.DefaultDbFormat;
    }
}
