using Bogus;
using EventHorizon.EventStore.Test.Models;

namespace EventHorizon.EventStore.Test.Fakers;

public static class EventStoreFakers
{
    public static readonly Faker<ExampleStoreState> StateFaker = new Faker<ExampleStoreState>()
        .RuleFor(x => x.Id, x => x.Random.Guid().ToString())
        .RuleFor(x => x.Name, x => x.Person.FirstName)
        .RuleFor(x => x.SubClasses, x => SubclassFaker.Generate(50).ToArray());

    public static readonly Faker<ExampleSubclass> SubclassFaker = new Faker<ExampleSubclass>()
        .RuleFor(x => x.Name1, x => x.Person.FirstName)
        .RuleFor(x => x.Name2, x => x.Person.FirstName)
        .RuleFor(x => x.Name3, x => x.Person.FirstName)
        .RuleFor(x => x.Name4, x => x.Person.FirstName)
        .RuleFor(x => x.Name5, x => x.Person.FirstName)
        .RuleFor(x => x.Name6, x => x.Person.FirstName)
        .RuleFor(x => x.Name7, x => x.Person.FirstName)
        .RuleFor(x => x.Name8, x => x.Person.FirstName)
        .RuleFor(x => x.Name9, x => x.Person.FirstName)
        .RuleFor(x => x.Name10, x => x.Person.FirstName);
}
