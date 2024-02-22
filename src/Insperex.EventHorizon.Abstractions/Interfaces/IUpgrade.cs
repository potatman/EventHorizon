using Insperex.EventHorizon.Abstractions.Interfaces.Actions;

namespace Insperex.EventHorizon.Abstractions.Interfaces;

public interface IUpgradeTo<out T>
    where T : IAction
{
    public T Upgrade();
}
