using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Interfaces.State;

public interface IApplyEvent<in T>
    where T : IEvent
{
    public void Apply(T payload);
}