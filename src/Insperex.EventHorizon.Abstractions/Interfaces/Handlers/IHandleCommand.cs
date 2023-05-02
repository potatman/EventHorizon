using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;

namespace Insperex.EventHorizon.Abstractions.Interfaces.Handlers;

public interface IHandleCommand<in TCommand>
    where TCommand : ICommand
{
    public void Handle(TCommand command, List<IEvent> events);
}
