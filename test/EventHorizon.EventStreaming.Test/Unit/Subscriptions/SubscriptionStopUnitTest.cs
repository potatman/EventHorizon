using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Samples.Models;
using EventHorizon.EventStreaming.Test.Fakers;
using EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace EventHorizon.EventStreaming.Test.Unit.Subscriptions;

[Trait("Category", "Unit")]
public class SubscriptionStopUnitTest
{
    private readonly StreamingClient _streamingClient;

    public SubscriptionStopUnitTest(ITestOutputHelper output)
    {
        _streamingClient = HostTestUtil.GetInMemoryHost(output).Services.GetRequiredService<StreamingClient>();
    }

    [Fact]
    public async Task StopWaitsForTheInFlightBatch()
    {
        var started = false;
        var finished = false;
        var subscription = await _streamingClient.CreateSubscription<Event>()
            .SubscriptionName($"Stop_{Guid.NewGuid():N}")
            .AddStream<Feed1PriceChanged>()
            .OnBatch(async _ =>
            {
                started = true;
                await Task.Delay(500);
                finished = true;
            })
            .Build()
            .StartAsync();

        await using var publisher = _streamingClient.CreatePublisher<Event>().AddStream<Feed1PriceChanged>().Build();
        await publisher.PublishAsync(EventStreamingFakers.RandomEventFaker.Generate(1).ToArray());
        await WaitUtil.WaitForTrue(() => started, TimeSpan.FromSeconds(10));

        await subscription.StopAsync();

        Assert.True(finished);
    }

    [Fact]
    public async Task StopDoesNotWaitForTheReceiveTimeoutWhenIdle()
    {
        var subscription = await _streamingClient.CreateSubscription<Event>()
            .SubscriptionName($"Idle_{Guid.NewGuid():N}")
            .AddStream<Feed1PriceChanged>()
            .OnBatch(_ => Task.CompletedTask)
            .Build()
            .StartAsync();
        await Task.Delay(200);

        var sw = Stopwatch.StartNew();
        await subscription.StopAsync();

        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5), $"Stop took {sw.Elapsed}");
    }
}
