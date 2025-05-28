namespace EventHorizon.Abstractions.Interfaces.Actions;

public interface IEvent : IAction
{
}

public interface IEvent<T> : IEvent
    where T : IState
{
}
