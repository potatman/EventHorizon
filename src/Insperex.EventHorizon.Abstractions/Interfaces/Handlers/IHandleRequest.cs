using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.Abstractions.Interfaces.Handlers;

public interface IHandleRequest<in TReq, out TRes>
    where TReq : IRequest
    where TRes : IResponse
{
    public TRes Handle(TReq request, AggregateContext context);
}
