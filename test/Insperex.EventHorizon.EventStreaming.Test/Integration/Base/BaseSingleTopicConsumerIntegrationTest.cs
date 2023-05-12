using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Publishers;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
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
    private readonly ITestOutputHelper _outputHelper;
    private readonly StreamingClient _streamingClient;
    private Stopwatch _stopwatch;
    private readonly TimeSpan _timeout;
    private Event[] _events;
    private readonly ListStreamConsumer<Event> _handler;
    private Publisher<Event> _publisher;
    private readonly PartialNackListStreamConsumer _partialNackHandler;

    protected BaseSingleTopicConsumerIntegrationTest(ITestOutputHelper outputHelper, IServiceProvider provider)
    {
        _outputHelper = outputHelper;
        _timeout = TimeSpan.FromSeconds(30);
        _streamingClient = provider.GetRequiredService<StreamingClient>();
        _handler = new ListStreamConsumer<Event>();
        _partialNackHandler = new(_outputHelper, 0.03, 3, 2, 100);
    }

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

    public async Task DisposeAsync()
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

    [Fact]
    public async Task TestSingleConsumerWithFailures()
    {
        // Consume
        await using var subscription = await _streamingClient.CreateSubscription<Event>()
            .AddStream<Feed1PriceChanged>()
            .BatchSize(_events.Length / 10)
            .GuaranteeMessageOrderOnFailure(true)
            .RetryBackoffPolicy(p =>
                p.MinDelay(TimeSpan.FromMilliseconds(10))
                .MaxDelay(TimeSpan.FromSeconds(15))
                .Multiplier(2))
            .OnBatch(_partialNackHandler.OnBatch) // Will nack at least some messages.
            .Build()
            .StartAsync();

        await using var publisher = await _streamingClient.CreatePublisher<Event>()
            .AddStream<Feed1PriceChanged>()
            .Build()
            .PublishAsync(_events);

        // Wait for List
        await WaitUtil.WaitForTrue(() => _events.Length <= _partialNackHandler.List.Count, _timeout);

        _partialNackHandler.Report();

        // Assert
        // Expecting the advanced failure handling to preserve message ordering despite the nacks.
        AssertUtil.AssertEventsValid(_events, _partialNackHandler.List.ToArray());
    }
}
