namespace EventHorizon.Abstractions.Interfaces.Actions;

public interface IRequest : IAction
{
}

public interface IRequest<T, TR> : IRequest
    where T : IState
    where TR : IResponse<T>
{
}
