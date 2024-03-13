using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.Abstractions.Interfaces.Handlers
{
    public interface IHandleEvent<in TEvent>
        where TEvent : IEvent
    {
        public void Handle(TEvent @event, AggregateContext context);
    }
}
