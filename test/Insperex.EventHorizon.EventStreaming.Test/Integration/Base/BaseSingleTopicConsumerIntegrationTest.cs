using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions.Backoff;
using Insperex.EventHorizon.EventStreaming.Test.Fakers;
using Insperex.EventHorizon.EventStreaming.Test.Shared;
using Insperex.EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace Insperex.EventHorizon.EventStreaming.Test.Integration.Base;

[Trait("Category", "Integration")]
public abstract class BaseSingleTopicConsumerIntegrationTest : IAsyncLifetime
{
    protected readonly ITestOutputHelper _outputHelper;
    protected readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    protected readonly TimeSpan _timeout;
    protected Event[] _events;
    private readonly ListStreamConsumer<Event> _handler;
    private Publisher<Event> _publisher;
    private readonly PartialNackListStreamConsumer _partialNackHandler;

    protected BaseSingleTopicConsumerIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        var random = new Random((int)DateTime.UtcNow.Ticks);
        UniqueTestId = $"{random.Next()}";

        Provider = provider;
        _outputHelper = outputHelper;
        _timeout = TimeSpan.FromSeconds(30);
        _streamingClient = provider.GetRequiredService<StreamingClient>();
        _handler = new ListStreamConsumer<Event>();
        _partialNackHandler = new(_outputHelper, 0.03, 3, 2,
            100, false);
    }

    protected string UniqueTestId { get; init; }

    protected IServiceProvider Provider { get; init; }

    public async Task InitializeAsync()
    {
        // Publish Events
        int sequenceId = 0;
        _events = EventStreamingFakers.Feed1PriceChangedFaker.Generate(1000)
            .Select(x => new Event(x.Id, ++sequenceId, x)).ToArray();
        _publisher = _streamingClient.CreatePublisher<Event>().AddStream<Feed1PriceChanged>().Build();
        await _publisher.PublishAsync(_events);

        // Setup
        _stopwatch = Stopwatch.StartNew();
    }

    public virtual async Task DisposeAsync()
    {
        _outputHelper.WriteLine($"Test Ran in {_stopwatch.ElapsedMilliseconds}ms");
        await _streamingClient.GetAdmin<Event>().DeleteTopicAsync(typeof(Feed1PriceChanged));
        await _publisher.DisposeAsync();
    }

    [Fact]
    public async Task TestSingleConsumer()
    {
        // Consume
        await using var subscription = await _streamingClient.CreateSubscription<Event>()
            .AddStream<Feed1PriceChanged>()
            .BatchSize(_events.Length / 10)
            .OnBatch(_handler.OnBatch)
            .Build()
            .StartAsync();

        // Wait for List
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);

        // Assert
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }

    [Fact]
    public async Task TestKeySharedConsumers()
    {
        var builder = _streamingClient.CreateSubscription<Event>()
            .AddStream<Feed1PriceChanged>()
            .BatchSize(_events.Length / 10)
            .OnBatch(_handler.OnBatch);

        // Consume
        await using var subscription1 = await builder.Build().StartAsync();
        await using var subscription2 = await builder.Build().StartAsync();

        // Assert
        await WaitUtil.WaitForTrue(() => _events.Length <= _handler.List.Count, _timeout);
        AssertUtil.AssertEventsValid(_events, _handler.List.ToArray());
    }

    [Fact(Skip = "temp disable")]
    public async Task TestSingleConsumerWithAdvancedFailures()
    {
        // Consume
        await using var subscription = await _streamingClient.CreateSubscription<Event>()
            .SubscriptionName($"Fails_{UniqueTestId}")
            .SubscriptionType(SubscriptionType.KeyShared)
            .AddStream<Feed1PriceChanged>()
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
