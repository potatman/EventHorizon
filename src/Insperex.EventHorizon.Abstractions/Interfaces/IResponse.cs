namespace Insperex.EventHorizon.Abstractions.Interfaces;

public interface IResponse : IAction
{
}

public interface IResponse<T> : IResponse
    where T : IState
{
}