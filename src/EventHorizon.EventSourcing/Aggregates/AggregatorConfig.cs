using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Serialization.Compression;

namespace EventHorizon.EventSourcing.Aggregates;

public class AggregatorConfig<T> where T : IState
{
    public bool IsValidationEnabled { get; set; }
    public Compression? StateCompression { get; set; }
    public Compression? EventCompression { get; set; }
}
