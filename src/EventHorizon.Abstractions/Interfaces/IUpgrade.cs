using EventHorizon.Abstractions.Interfaces.Actions;

namespace EventHorizon.Abstractions.Interfaces;

public interface IUpgradeTo<out T>
    where T : IAction
{
    public T Upgrade();
}
