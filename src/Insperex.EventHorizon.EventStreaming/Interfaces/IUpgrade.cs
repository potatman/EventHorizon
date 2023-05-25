using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;

namespace Insperex.EventHorizon.EventStreaming.Interfaces;

public interface IUpgradeTo<out T>
    where T : IAction
{
    public T Upgrade();
}
