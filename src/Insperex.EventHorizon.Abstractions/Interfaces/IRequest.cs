namespace Insperex.EventHorizon.Abstractions.Interfaces;

public interface IRequest : IAction
{
}

public interface IRequest<T, TR> : IRequest 
    where T : IState
    where TR : IResponse<T>
{
}