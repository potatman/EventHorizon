using EventHorizon.Abstractions.Interfaces.Actions;

namespace EventHorizon.Abstractions.Interfaces.Handlers;

public interface IApplyEvent<in T>
    where T : IEvent
{
    public void Apply(T @event);
}
