using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.Abstractions.Interfaces.Handlers
{
    public interface IHandleEvent<in TEvent>
        where TEvent : IEvent
    {
        public void Handle(TEvent @event, AggregateContext context);
    }
}
