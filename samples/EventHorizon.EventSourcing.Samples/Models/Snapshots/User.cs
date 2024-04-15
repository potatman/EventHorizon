using EventHorizon.Abstractions.Attributes;
using EventHorizon.Abstractions.Interfaces;
using EventHorizon.Abstractions.Interfaces.Actions;
using EventHorizon.Abstractions.Interfaces.Handlers;
using EventHorizon.Abstractions.Models;

namespace EventHorizon.EventSourcing.Samples.Models.Snapshots;

public class User : IState,
    IHandleCommand<ChangeUserName>,
    IApplyEvent<UserNameChanged>
{
    public string Id { get; set; }
    public string Name { get; set; }

    public void Handle(ChangeUserName command, AggregateContext context)
    {
        if(Name != command.Name)
            context.AddEvent(new UserNameChanged(command.Name));
    }

    public void Apply(UserNameChanged @event)
    {
        Name = @event.Name;
    }
}

// Commands
public record ChangeUserName([property: StreamPartitionKey]string Name) : ICommand<User>;

// Events
public record UserNameChanged(string Name) : IEvent<User>;
