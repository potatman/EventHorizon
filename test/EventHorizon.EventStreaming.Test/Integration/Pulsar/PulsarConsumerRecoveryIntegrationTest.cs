using System;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Samples.Models;
using EventHorizon.EventStreaming.Subscriptions;
using EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pulsar.Client.Api;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Integration.Pulsar;

[Trait("Category", "Integration")]
public class PulsarConsumerRecoveryIntegrationTest(ITestOutputHelper outputHelper) : IAsyncDisposable
{
    private readonly IHost _host = HostTestUtil.GetPulsarHost(outputHelper);
    private readonly string _topicSuffix = $"recovery_{Guid.NewGuid():N}";

    /// <summary>
    /// A consumer closed underneath the subscription (for example by an exception inside the
    /// client) must be replaced, not polled forever.
    /// </summary>
    [Fact]
    public async Task ClosedConsumerIsReplacedAndKeepsDelivering()
    {
        var factory = _host.Services.GetRequiredService<IStreamFactory>();
        var topic = GetTopic();
        await using var consumer = (PulsarTopicConsumer<Event>)factory.CreateConsumer(new SubscriptionConfig<Event>
        {
            Topics = [topic],
            SubscriptionName = $"recovery_{_topicSuffix}",
            SubscriptionType = SubscriptionType.KeyShared,
            IsBeginning = true,
            BatchSize = 10,
            NoBatchDelay = TimeSpan.FromMilliseconds(10)
        });
        await consumer.InitAsync();

        await consumer.Consumer.DisposeAsync();

        var publisher = _host.Services.GetRequiredService<StreamingClient>().CreatePublisher<Event>()
            .AddStream<Feed1PriceChanged>(_topicSuffix)
            .Build();
        await publisher.PublishAsync(new Event("stream-1", 1, new Feed1PriceChanged("stream-1", 1)));

        MessageContext<Event>[] batch = null;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        while (batch is not { Length: > 0 } && !timeout.IsCancellationRequested)
            batch = await consumer.NextBatchAsync(timeout.Token);

        Assert.NotNull(batch);
        Assert.Equal("stream-1", Assert.Single(batch).Data.StreamId);
        await consumer.FinalizeBatchAsync(batch, []);
    }

    private string GetTopic() =>
        _host.Services.GetRequiredService<IStreamFactory>().GetTopicResolver()
            .GetTopics<Event>(typeof(Feed1PriceChanged), _topicSuffix)[0];

    public async ValueTask DisposeAsync()
    {
        var admin = (PulsarTopicAdmin<Event>)_host.Services.GetRequiredService<IStreamFactory>().CreateAdmin<Event>();
        await admin.DeleteTopicAsync(GetTopic(), CancellationToken.None);
        _host.Dispose();
    }
}
