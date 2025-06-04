using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Publishers;
using EventHorizon.EventStreaming.Samples.Models;
using EventHorizon.EventStreaming.Subscriptions.Backoff;
using EventHorizon.EventStreaming.Test.Fakers;
using EventHorizon.EventStreaming.Test.Shared;
using EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventStreaming.Test.Integration.Base;

[Trait("Category", "Integration")]
public abstract class BaseMultiTopicConsumerIntegrationTest : IAsyncLifetime
{
    protected readonly ITestOutputHelper _outputHelper;
    protected readonly TimeSpan _timeout;
    private Stopwatch _stopwatch;
    protected Event[] _events;
    protected readonly StreamingClient _streamingClient;
    private readonly ListStreamConsumer<Event> _handler;
    private Publisher<Event> _publisher1;
    private Publisher<Event> _publisher2;
    private readonly PartialNackListStreamConsumer _partialNackHandler;

    protected BaseMultiTopicConsumerIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        var random = new Random((int)DateTime.UtcNow.Ticks);
        UniqueTestId = $"{random.Next()}";

        Provider = provider;
        _outputHelper = outputHelper;
        _streamingClient = provider.GetRequiredService<StreamingClient>();
        _timeout = TimeSpan.FromSeconds(20);
        _handler = new ListStreamConsumer<Event>();
        _partialNackHandler = new(_outputHelper, 0.03, 3, 2,
            100, false);
    }

    protected string UniqueTestId { get; init; }

    protected IServiceProvider Provider { get; init; }

    public async Task InitializeAsync()
    {
        // Publish Events
        _events = EventStreamingFakers.RandomEventFaker.Generate(1000).ToArray();
        _publisher1 = _streamingClient.CreatePublisher<Event>().AddStream<Feed1PriceChanged>().Build();
        _publisher2 = _streamingClient.CreatePublisher<Event>().AddStream<Feed2PriceChanged>().Build();
        await _publisher1.PublishAsync(_events.Take(_events.Length/2).ToArray());
        await _publisher2.PublishAsync(_events.Skip(_events.Length/2).ToArray());

        // Setup
        _stopwatch = Stopwatch.StartNew();
    }

    public virtual async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Feed1PriceChanged));
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Feed2PriceChanged));
        await _publisher1.DisposeAsync();
        await _publisher2.DisposeAsync();
    }

    [Fact]
    public async Task SubscribeToMultipleTopics()
    {
        // Consume
        await using var subscription = await _streamingClient.CreateSubscription<Event>()
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

        await using var subscription1 = await builder.AddStream<Feed1PriceChanged>().OnBatch(_handler.OnBatch).Build().StartAsync();
        await using var subscription2 = await builder.AddStream<Feed2PriceChanged>().OnBatch(_handler.OnBatch).Build().StartAsync();

        // Assert
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }

    [Fact]
    public async Task TestSingleConsumerWithAdvancedFailures()
    {
        // Consume
        await using var subscription = await _streamingClient.CreateSubscription<Event>()
            .SubscriptionName($"Fails_{UniqueTestId}")
            .AddStream<Feed1PriceChanged>()
            .AddStream<Feed2PriceChanged>()
            .BatchSize(_events.Length / 10)
            .GuaranteeMessageOrderOnFailure(true)
            .ExponentialBackoff(b => b
                .StartAt(TimeSpan.FromMilliseconds(10))
                .Max(TimeSpan.FromSeconds(15)))
            .OnBatch(_partialNackHandler.OnBatch) // Will nack at least some messages.
            .Build()
            .StartAsync();

        // Wait for List
        await WaitUtil.WaitForTrue(() => _events.Length <= _partialNackHandler.List.Count, _timeout);

        _partialNackHandler.Report();
        Assert.True(_partialNackHandler.RedeliveredMessages == 0,
            $"There were {_partialNackHandler.RedeliveredMessages} redeliveries of previously-accepted messages. Should not have any!");

        // Assert
        // Expecting the advanced failure handling to preserve message ordering despite the nacks.
        AssertUtil.AssertEventsValid(_events, _partialNackHandler.List.ToArray());
    }
}
