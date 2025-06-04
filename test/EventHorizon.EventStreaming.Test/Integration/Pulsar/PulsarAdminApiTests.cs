﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Pulsar.Utils;
using EventHorizon.EventStreaming.Samples.Models;
using EventHorizon.EventStreaming.Test.Fakers;
using EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventStreaming.Test.Integration.Pulsar;

[Trait("Category", "Integration")]
public class PulsarAdminApiTests: IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly PulsarClientResolver _pulsarClientResolver;
    private readonly IServiceProvider _serviceProvider;
    private readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    private readonly TimeSpan _timeout;
    private readonly PulsarTopicResolver _pulsarTopicResolver;
    private Event[] _events;

    public PulsarAdminApiTests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
        _serviceProvider = HostTestUtil.GetPulsarHost(_outputHelper).Services;
        _pulsarClientResolver = _serviceProvider.GetRequiredService<PulsarClientResolver>();
        _streamingClient = _serviceProvider.GetRequiredService<StreamingClient>();
        _timeout = TimeSpan.FromSeconds(30);
        var attributeUtil = _serviceProvider.GetRequiredService<AttributeUtil>();
        _pulsarTopicResolver = new(attributeUtil);
    }

    public Task InitializeAsync()
    {
        int sequenceId = 0;
        _events = EventStreamingFakers.Feed1PriceChangedFaker.Generate(1000)
            .Select(x => new Event(x.Id, ++sequenceId, x)).ToArray();
        _stopwatch = Stopwatch.StartNew();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Feed1PriceChanged));
    }

    [Fact]
    public async Task TestSubscriptionStats()
    {
        const string SubscriptionName = "MyTestSubscription";

        var builder = _streamingClient.CreateSubscription<Event>()
            .AddStream<Feed1PriceChanged>()
            .SubscriptionName(SubscriptionName)
            .BatchSize(100)
            .OnBatch((context) => Task.CompletedTask);

        await using var publisher = await _streamingClient.CreatePublisher<Event>()
            .AddStream<Feed1PriceChanged>()
            .Build()
            .PublishAsync(_events);

        // Set up subscriptions (so that key hash ranges get resolved on broker.
        await using var subscription1 = await builder.Build().StartAsync();
        await using var subscription2 = await builder.Build().StartAsync();
        using var httpClient = _pulsarClientResolver.GetAdminHttpClient();

        await Task.Delay(TimeSpan.FromSeconds(1));

        var cts = new CancellationTokenSource();
        cts.CancelAfter(_timeout);

        var topicName = _pulsarTopicResolver.GetTopics<Event>(typeof(Feed1PriceChanged)).First();
        var topic = PulsarTopicParser.Parse(topicName);

        var url = $"{topic.ApiRoot}/stats?subscriptionBacklogSize=false";

        var response = await httpClient.GetAsync(url, cts.Token);
        Assert.Equal(200, (int)response.StatusCode);
        var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

        var json = JsonDocument.Parse(responseBody);
        var subscriptionsRoot = json.RootElement.GetProperty("subscriptions");
        var subscriptions = subscriptionsRoot.EnumerateObject().Select(p => p.Name).ToArray();
        var subscriptionName = subscriptions.Single(s => s.Contains(SubscriptionName));
        var consumers = subscriptionsRoot
            .GetProperty(subscriptionName)
            .GetProperty("consumers")
            .EnumerateArray();

        Assert.Equal(2, consumers.Count());

        foreach (var consumer in consumers)
        {
            var keyHashRanges = consumer.GetProperty("keyHashRanges").EnumerateArray().ToArray();

            foreach (var range in keyHashRanges)
            {
                // key hash ranges should take the form of a string that deserializes into a JSON array of two numbers.
                var rangeValues = JsonValue.Parse(range.GetString()).AsArray().ToArray();
                Assert.Equal(2, rangeValues.Length);
            }
        }
    }
}
