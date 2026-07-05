using System;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.Abstractions.Util;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Pulsar.Models;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Unit.Pulsar;

[Trait("Category", "Unit")]
public class OrderGuaranteedConsumerUnitTest
{
    /// <summary>
    /// Builds an order-guaranteed consumer pointed at an unreachable admin endpoint, so the
    /// first thing NextBatchAsync does (querying key hash ranges) fails fast.
    /// </summary>
    private static Interfaces.Streaming.ITopicConsumer<Event> GetConsumer()
    {
        var config = Options.Create(new PulsarConfig
        {
            ServiceUrl = "pulsar://127.0.0.1:1",
            AdminUrl = "http://127.0.0.1:1"
        });
        var clientResolver = new PulsarClientResolver(config);
        var factory = new PulsarStreamFactory(clientResolver, new AttributeUtil(), NullLoggerFactory.Instance);

        return factory.CreateConsumer(new SubscriptionConfig<Event>
        {
            Topics = new[] { "persistent://test_tenant/test_namespace/test_topic" },
            SubscriptionName = "test_subscription",
            IsMessageOrderGuaranteedOnFailure = true
        });
    }

    [Fact]
    public async Task NextBatchRecoversAfterTransientError()
    {
        var consumer = GetConsumer();

        // First batch fails (admin endpoint unreachable).
        var first = await Record.ExceptionAsync(() => consumer.NextBatchAsync(CancellationToken.None));
        Assert.NotNull(first);

        // The batch-in-progress flag must be released on error: the next call has to retry
        // (and fail again), not silently return an empty batch forever.
        var second = await Record.ExceptionAsync(() => consumer.NextBatchAsync(CancellationToken.None));
        Assert.NotNull(second);
        Assert.IsNotType<NullReferenceException>(second);
    }
}
