using System.Collections.Generic;

namespace Insperex.EventHorizon.Abstractions.Interfaces.Handlers;

public interface IHandleCommand<in TCommand>
    where TCommand : ICommand
{
    public void Handle(TCommand command, List<IEvent> events);
}
