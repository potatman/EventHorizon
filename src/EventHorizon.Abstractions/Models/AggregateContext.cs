using System.Collections.Generic;
using System.Linq;
using EventHorizon.Abstractions.Interfaces.Actions;

namespace EventHorizon.Abstractions.Models
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
            if(@event != null)
                Events.Add(@event);
        }

        public void AddEvents(IEvent[] events)
        {
            if (events?.Length > 0)
                Events.AddRange(events);
        }
    }
}
