using System.Collections.Generic;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.Abstractions.Interfaces.Handlers;

public interface IHandleRequest<in TReq, out TRes>
    where TReq : IRequest
    where TRes : IResponse
{
    public TRes Handle(TReq request, AggregateContext context);
}
