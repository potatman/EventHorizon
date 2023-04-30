﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Insperex.EventHorizon.EventStreaming.Test.Fakers;
using Insperex.EventHorizon.EventStreaming.Test.Shared;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Base;

[Trait("Category", "Integration")]
public abstract class BaseMultiTopicConsumerIntegrationTest : IAsyncLifetime
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly TimeSpan _timeout;
    private Stopwatch _stopwatch;
    private Event[] _events;
    private readonly StreamingClient _streamingClient;
    private readonly ListTopicHandler<Event> _handler;

    protected BaseMultiTopicConsumerIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        _outputHelper = outputHelper;
        _streamingClient = provider.GetRequiredService<StreamingClient>();
        _timeout = TimeSpan.FromSeconds(15);
        _handler = new ListTopicHandler<Event>();
    }

    public async Task InitializeAsync()
    {
        _events = EventStreamingFakers.RandomEventFaker.Generate(1000).ToArray();
        // Setup
        using var publisher1 = _streamingClient.CreatePublisher<Event>().AddStream<Feed1PriceChanged>().Build();
        using var publisher2 = _streamingClient.CreatePublisher<Event>().AddStream<Feed2PriceChanged>().Build();

        await publisher1.PublishAsync(_events.Take(_events.Length/2).ToArray());
        await publisher2.PublishAsync(_events.Skip(_events.Length/2).ToArray());
        await Task.Delay(4000);

        _stopwatch = Stopwatch.StartNew();
    }

    public async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Feed1PriceChanged));
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Feed2PriceChanged));
    }

    [Fact]
    public async Task SubscribeToMultipleTopics()
    {
        // Consume
        using var subscription = await _streamingClient.CreateSubscription<Event>()
            .AddStream<Feed1PriceChanged>()
            .AddStream<Feed2PriceChanged>()
            .BatchSize(_events.Length/10)
            .OnBatch(_handler.OnBatch)
            .Build()
            .StartAsync();

        // Assert
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }

    [Fact]
    public async Task SubscribeToTopicsSeparately()
    {
        // Consume
        var builder = _streamingClient.CreateSubscription<Event>()
            .BatchSize(_events.Length / 10);

        using var subscription1 = await builder.AddStream<Feed1PriceChanged>().OnBatch(_handler.OnBatch).Build().StartAsync();
        using var subscription2 = await builder.AddStream<Feed2PriceChanged>().OnBatch(_handler.OnBatch).Build().StartAsync();

        // Assert
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }
}
