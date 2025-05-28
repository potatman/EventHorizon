using EventHorizon.Abstractions.Interfaces;
using EventHorizon.EventSourcing.Interfaces;

namespace EventHorizon.EventSourcing.Aggregates;

public class AggregateConfig<T> where T : class, IState
{
    public bool IsValidationEnabled { get; set; }
    public bool IsRebuildEnabled { get; set; }
    public int? BatchSize { get; set; }
    public IAggregateMiddleware<T> Middleware { get; set; }
}
