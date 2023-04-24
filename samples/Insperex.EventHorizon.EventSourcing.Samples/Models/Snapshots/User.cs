using System;
using System.Collections.Generic;
using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.EventSourcing.Interfaces.State;
using Insperex.EventHorizon.EventStreaming.Interfaces;

namespace Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;

[SnapshotStore("test_snapshot_bank_user", nameof(User))]
[EventStream("test_event_bank_user", nameof(User))]
public class User : IState,
    IHandleCommand<ChangeUserName>,
    IApplyEvent<UserNameChangedV2>
{
    public string Id { get; set; }
    public string Name { get; set; }

    public User() { }

    public void Handle(ChangeUserName command, List<IEvent> events)
    {
        if(Name != command.Name)
            events.Add(new UserNameChangedV2(command.Name));
    }

    public void Apply(UserNameChangedV2 payload)
    {
        Name = payload.Name;
    }
}

// Commands
public record ChangeUserName(string Name) : ICommand<User>;

// Events
public record UserNameChangedV2(string Name) : IEvent<User>;

// Legacy Events
[Obsolete]
public record UserNameChanged(string Name) : IEvent<User>, IUpgradeTo<UserNameChangedV2>
{
    public UserNameChangedV2 Upgrade()
    {
        return new UserNameChangedV2(Name);
    }
}
