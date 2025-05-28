using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;

namespace EventHorizon.EventStreaming.Interfaces;

public interface IUpgradeTo<out T>
    where T : IAction
{
    public T Upgrade();
}
