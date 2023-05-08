using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;

namespace Insperex.EventHorizon.Abstractions.Models
{
    public class AggregateContext
    {
        internal readonly List<IEvent> Events = new();

        public bool Exists { get; set; }

        public AggregateContext(bool exists)
        {
            Exists = exists;
        }

        public void AddEvent(IEvent @event)
        {
            Events.Add(@event);
        }
    }
}
