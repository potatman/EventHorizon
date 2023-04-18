namespace Insperex.EventHorizon.Abstractions.Interfaces;

public interface ICommand : IAction
{
}

public interface ICommand<T> : ICommand
    where T : IState
{
}