using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Interfaces.State;

public interface IHandleCommand<in TCommand>
    where TCommand : ICommand
{
    public void Handle(TCommand command, List<IEvent> events);
}