using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Bogus;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Benchmark.Models;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Pulsar.Utils;
using Insperex.EventHorizon.EventStreaming.Readers;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Benchmark.Singletons;

public class PulsarSingleton : IDisposable
{
    public static readonly PulsarSingleton Instance = new();

    public static readonly IHost Host = HostTestUtil.GetPulsarHost(null);

    public static readonly Lazy<IStreamFactory> Factory = new(() => Host.Services.GetRequiredService<IStreamFactory>());
    public static readonly Lazy<StreamingClient> StreamClient = new(() => Host.Services.GetRequiredService<StreamingClient>());

    private readonly Dictionary<Type, Publisher<Event>> Publishers = new();
    private readonly Dictionary<Type, ITopicConsumer<Event>> Consumers = new();
    private readonly Dictionary<Type, Reader<Event>> Readers = new();

    public Publisher<Event> GetPublisher<T>()
    {
        var type = typeof(T);
        if (Publishers.ContainsKey(type))
            return Publishers[type];

        Publishers[type] = StreamClient.Value.CreatePublisher<Event>()
            .AddStream<T>()
            .Build();

        return Publishers[type];
    }
    public ITopicConsumer<Event> GetConsumer<T>()
    {
        var type = typeof(T);
        if (Consumers.ContainsKey(type))
            return Consumers[type];

        var topics = Factory.Value.GetTopicResolver().GetTopics<Event>(type);
        Consumers[type] = Factory.Value.CreateConsumer(new SubscriptionConfig<Event>
        {
            Topics = topics,
            SubscriptionName = "Test-Benchmark",
            BatchSize = 1000
        });

        return Consumers[type];
    }

    public Reader<Event> GetReader<T>()
    {
        var type = typeof(T);
        if (Readers.ContainsKey(type))
            return Readers[type];

        Readers[type] = StreamClient.Value.CreateReader<Event>()
            .AddStream<T>()
            .Keys("5")
            .Build();

        return Readers[type];
    }

    public Event[] FakeEvents(int count)
    {
        var faker = new Faker();
        var list = new List<Event>();
        for (var i = 0; i < count; i++)
            list.Add(new Event("123", new ExampleEvent
            {
                Property1 = faker.Random.AlphaNumeric(10),
                Property2 = faker.Random.AlphaNumeric(10),
                Property3 = faker.Random.AlphaNumeric(10),
                Property4 = faker.Random.AlphaNumeric(10),
                Property5 = faker.Random.AlphaNumeric(10),
                Property6 = faker.Random.AlphaNumeric(10),
                Property7 = faker.Random.AlphaNumeric(10),
                Property8 = faker.Random.AlphaNumeric(10),
                Property9 = faker.Random.AlphaNumeric(10),
                Property10 = faker.Random.AlphaNumeric(10),
            }));
        return list.ToArray();
    }


    public void Dispose()
    {
        var types = Readers.Select(x => x.Key)
            .Concat(Consumers.Select(x => x.Key))
            .Concat(Publishers.Select(x => x.Key))
            .Distinct()
            .ToArray();

        // Dispose All
        foreach (var key in Readers.Keys) Readers[key]?.Dispose();
        foreach (var key in Consumers.Keys) Consumers[key]?.Dispose();
        foreach (var key in Publishers.Keys) Publishers[key]?.Dispose();

        // Delete Topics
        foreach (var type in types)
            StreamClient.Value.GetAdmin<Event>().DeleteTopicAsync(type, ct: CancellationToken.None).Wait();
    }
}
