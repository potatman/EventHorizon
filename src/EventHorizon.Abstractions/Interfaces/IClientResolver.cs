namespace EventHorizon.Abstractions.Interfaces
{
    public interface IClientResolver<out T>
    {
        T GetClient();
    }
}