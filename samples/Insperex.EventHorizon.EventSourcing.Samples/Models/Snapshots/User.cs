using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Interfaces.State;
using Insperex.EventHorizon.EventStreaming.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;

[SnapshotStore("test_snapshot_bank_user", nameof(User))]
[EventStream("test_event_bank_user", nameof(User))]
public class User : IState, 
    IHandleCommand<CreateUser, User>,
    IApplyEvent<UserCreatedV2>
{
    public string Id { get; set; }
    public string Name { get; set; }
    
    public User() { }
    
    public void Handle(CreateUser command, User state, List<IEvent> events)
    {
        if(Name == default)
            events.Add(new UserCreatedV2(command.Name));
    }

    public void Apply(UserCreatedV2 payload)
    {
        Name = payload.Name;
    }
}

// Commands
public record CreateUser(string Name) : ICommand<User>;

// Events
public record UserCreatedV2(string Name) : IEvent<User>;

// Legacy Events
public record UserCreated(string Name) : IEvent<User>, IUpgradeTo<UserCreatedV2>
{
    public UserCreatedV2 Upgrade()
    {
        return new UserCreatedV2(Name);
    }
}