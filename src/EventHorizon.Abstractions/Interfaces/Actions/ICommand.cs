namespace EventHorizon.Abstractions.Interfaces.Actions;

public interface ICommand : IAction
{
}

public interface ICommand<T> : ICommand
    where T : IState
{
}
