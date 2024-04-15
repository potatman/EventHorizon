namespace EventHorizon.Abstractions.Interfaces;

public interface IState : IPayload
{
    public string Id { get; set; }
}
