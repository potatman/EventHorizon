using System;
using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregateConfig<T> where T : class, IState
{
    public bool IsRebuildEnabled { get; set; }
    public int RetryLimit { get; set; }
    public Action<Aggregate<T>[]> BeforeSave { get; set; }
}