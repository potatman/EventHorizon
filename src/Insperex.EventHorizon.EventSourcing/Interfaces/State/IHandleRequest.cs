using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Interfaces.State;

public interface IHandleRequest<in TReq, out TRes>
    where TReq : IRequest
    where TRes : IResponse
{
    public TRes Handle(TReq request, List<IEvent> events);
}