using System.Text.Json;
using Bogus;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Test.Models;

namespace Insperex.EventHorizon.EventStreaming.Test.Fakers;

public static class EventStreamingFakers
{
    public static readonly Faker Faker = new Faker();

    private static int _sequenceId = 0;
    public static readonly Faker<Event> EventFaker = new Faker<Event>()
        .CustomInstantiator(x => new Event(x.Random.AlphaNumeric(10), ++_sequenceId, ExampleEventFaker.Generate()));
    
    public static readonly Faker<ExampleEvent1> ExampleEventFaker = new Faker<ExampleEvent1>()
        .RuleFor(x => x.StreamId, x => x.Random.Guid().ToString())
        .RuleFor(x => x.Name, x => x.Person.FirstName);
    
    public static readonly Faker<ExampleEvent2> ExampleEvent2Faker = new Faker<ExampleEvent2>()
        .RuleFor(x => x.StreamId, x => x.Random.Guid().ToString())
        .RuleFor(x => x.Name, x => x.Person.FirstName);
}