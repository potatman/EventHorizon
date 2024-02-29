using Insperex.EventHorizon.Abstractions.Interfaces.Actions;

namespace Insperex.EventHorizon.Abstractions.Interfaces;

public interface IState : IPayload
{
    public string Id { get; set; }
}
