namespace EventHorizon.Abstractions.Interfaces.Actions;

public interface IResponse : IAction
{
}

public interface IResponse<T> : IResponse
    where T : IState
{
}
