using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Serialization.Compression;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregatorConfig<T> where T : IState
{
    public bool IsValidationEnabled { get; set; }
    public Compression? StateCompression { get; set; }
    public Compression? EventCompression { get; set; }
}
