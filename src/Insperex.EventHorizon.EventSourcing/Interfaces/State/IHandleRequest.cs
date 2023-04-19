using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Interfaces.State;

public interface IHandleRequest<in TReq, out TRes, in TState>
    where TReq : IRequest<TState, TRes>
    where TRes : IResponse<TState>
    where TState : class, IState
{
    public TRes Handle(TReq request, TState state, List<IEvent> events);
}