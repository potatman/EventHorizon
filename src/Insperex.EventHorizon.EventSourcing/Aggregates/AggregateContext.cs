using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;

namespace Insperex.EventHorizon.EventSourcing.Aggregates;

public class AggregateContext
{
    internal List<IEvent> Events { get; set; }
    public void Apply(IEvent @event) => Events.Add(@event);
}