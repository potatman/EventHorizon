using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.Abstractions.Interfaces.Handlers;

public interface IHandleCommand<in TCommand>
    where TCommand : ICommand
{
    public void Handle(TCommand command, AggregateContext context);
}
