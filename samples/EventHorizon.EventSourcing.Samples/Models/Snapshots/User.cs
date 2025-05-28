using System;
using System.Collections.Generic;
using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Interfaces.Handlers;
using EventHorizon.Abstractions.Models;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Interfaces;
using EventHorizon.EventStreaming.Pulsar.Attributes;

namespace EventHorizon.EventSourcing.Samples.Models.Snapshots;

[Stream("$type")]
[PulsarNamespace("test_bank", "user")]
[SnapshotStore("test_snapshot_bank_user")]
public class User : IState,
    IHandleCommand<ChangeUserName>,
    IApplyEvent<UserNameChangedV2>
{
    public string Id { get; set; }
    public string Name { get; set; }

    public void Handle(ChangeUserName command, AggregateContext context)
    {
        if(Name != command.Name)
            context.AddEvent(new UserNameChangedV2(command.Name));
    }

    public void Apply(UserNameChangedV2 @event)
    {
        Name = @event.Name;
    }
}

// Commands
public record ChangeUserName([property: StreamPartitionKey]string Name) : ICommand<User>;

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
