using EventHorizon.Abstractions.Interfaces;

namespace EventHorizon.EventSourcing.Samples.Models.Snapshots;

public class BankAccount : IState
{
    public string Id { get; set; }
    public User User { get; set; }
    public Account Account { get; set; }
}
