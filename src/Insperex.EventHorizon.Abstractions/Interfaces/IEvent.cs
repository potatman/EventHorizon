namespace Insperex.EventHorizon.Abstractions.Interfaces;

public interface IEvent : IAction
{
}

public interface IEvent<T> : IEvent
    where T : IState
{
}