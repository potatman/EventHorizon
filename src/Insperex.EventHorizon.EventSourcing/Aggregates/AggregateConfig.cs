using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregateConfig<T> where T : IState
{
    public bool IsValidationEnabled { get; set; }
}
