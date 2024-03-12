using Insperex.EventHorizon.Abstractions.Attributes;
using Insperex.EventHorizon.Abstractions.Interfaces;
using Insperex.EventHorizon.Abstractions.Interfaces.Actions;
using Insperex.EventHorizon.Abstractions.Interfaces.Handlers;
using Insperex.EventHorizon.Abstractions.Models;

namespace Insperex.EventHorizon.EventSourcing.Samples.Models.Snapshots;

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
