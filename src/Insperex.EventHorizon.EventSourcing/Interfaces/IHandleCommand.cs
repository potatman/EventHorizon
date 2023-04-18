using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Interfaces;

public interface IHandleCommand<in TCommand, in TState>
    where TCommand : ICommand<TState>
    where TState : class, IState
{
    public void Handle(TCommand command, TState state, List<IEvent> events);
}