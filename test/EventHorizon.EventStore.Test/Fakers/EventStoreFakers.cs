using Bogus;
using EventHorizon.EventStore.Test.Models;

namespace EventHorizon.EventStore.Test.Fakers;

public static class EventStoreFakers
{
    public static readonly Faker<ExampleStoreState> StateFaker = new Faker<ExampleStoreState>()
        .RuleFor(x => x.Id, x => x.Random.Guid().ToString())
        .RuleFor(x => x.Name, x => x.Person.FirstName);
}
