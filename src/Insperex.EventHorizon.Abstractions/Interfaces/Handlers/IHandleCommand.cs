using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.Abstractions.Interfaces.Handlers;

public interface IHandleCommand<in TCommand>
    where TCommand : ICommand
{
    public void Handle(TCommand command, AggregateContext context);
}
