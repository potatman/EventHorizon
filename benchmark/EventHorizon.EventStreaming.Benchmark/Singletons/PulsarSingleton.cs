using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Benchmark.Models;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Readers;
using EventHorizon.EventStreaming.Subscriptions;
using EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EventHorizon.EventStreaming.Benchmark.Singletons;

public class PulsarSingleton : IAsyncDisposable
{
    public static readonly PulsarSingleton Instance = new();

    public static readonly IHost Host = HostTestUtil.GetPulsarHost(null);

    public static readonly Lazy<IStreamFactory> Factory = new(() => Host.Services.GetRequiredService<IStreamFactory>());
    public static readonly Lazy<StreamingClient> StreamClient = new(() => Host.Services.GetRequiredService<StreamingClient>());

    private readonly Dictionary<Type, Publisher<Event>> Publishers = new();
    private readonly Dictionary<Type, ITopicConsumer<Event>> Consumers = new();
    private readonly Dictionary<Type, Reader<Event>> Readers = new();
    private PulsarTopicAdmin<Event> _topicAdmin;

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

    public PulsarTopicAdmin<Event> GetTopicAdmin()
    {
        return _topicAdmin ??= (PulsarTopicAdmin<Event>) Factory.Value.CreateAdmin<Event>();
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

    public async ValueTask DisposeAsync()
    {
        var types = Readers.Select(x => x.Key)
            .Concat(Consumers.Select(x => x.Key))
            .Concat(Publishers.Select(x => x.Key))
            .Distinct()
            .ToArray();

        // Dispose All
        foreach (var key in Readers.Keys) await Readers[key].DisposeAsync();
        foreach (var key in Consumers.Keys) await Consumers[key].DisposeAsync();
        foreach (var key in Publishers.Keys) await Publishers[key].DisposeAsync();

        // Delete Topics
        foreach (var type in types)
            await StreamClient.Value.GetAdmin<Event>().DeleteTopicAsync(type, ct: CancellationToken.None);
    }
}
