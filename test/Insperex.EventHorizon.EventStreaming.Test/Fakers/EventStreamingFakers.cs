using System.Linq;
using Bogus;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Samples.Models;

namespace Insperex.EventHorizon.EventStreaming.Test.Fakers;

public static class EventStreamingFakers
{
    private static readonly Faker<string> StreamIdFaker = new Faker<string>()
        .CustomInstantiator(f => f.Random.Guid().ToString());

    public static readonly string[] StreamIds =
        Enumerable.Range(1, 100)
            .Select(i => StreamIdFaker.Generate())
            .ToArray();

    private static int _sequenceId;
    public static readonly Faker<Event> RandomEventFaker = new Faker<Event>()
        .CustomInstantiator(x =>
        {
            var randomInt = x.Random.Int() % 2;
            var faker = randomInt == 0 ? (PriceChanged) Feed1PriceChangedFaker.Generate() : Feed2PriceChangedFaker.Generate();
            return new Event(x.PickRandom(StreamIds), ++_sequenceId, faker);
        });

    public static readonly Faker<Feed1PriceChanged> Feed1PriceChangedFaker = new Faker<Feed1PriceChanged>()
        .CustomInstantiator(x => new Feed1PriceChanged(x.PickRandom(StreamIds), x.Random.Int(1, 100)));

    public static readonly Faker<Feed2PriceChanged> Feed2PriceChangedFaker = new Faker<Feed2PriceChanged>()
        .CustomInstantiator(x => new Feed2PriceChanged(x.PickRandom(StreamIds), x.Random.Int(1, 100)));
}
