using Insperex.EventHorizon.Abstractions.Interfaces;

namespace Insperex.EventHorizon.EventStreaming.Interfaces;

public interface IUpgradeTo<out T>
    where T : IAction
{
    public T Upgrade();
}
