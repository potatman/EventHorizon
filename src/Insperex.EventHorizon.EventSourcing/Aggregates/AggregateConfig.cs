using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregateConfig<T> where T : class, IState
{
    public bool IsValidationEnabled { get; set; }
    public bool IsRebuildEnabled { get; set; }
    public int RetryLimit { get; set; }
    public int BatchSize { get; set; }
    public IAggregateMiddleware<T> Middleware { get; set; }
}
