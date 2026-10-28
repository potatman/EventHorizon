using System;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Pulsar;
using EventHorizon.EventStreaming.Samples.Models;
using EventHorizon.EventStreaming.Test.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pulsar.Client.Api;
using Pulsar.Client.Common;
using Xunit;

namespace EventHorizon.EventStreaming.Test.Integration.Pulsar;

[Trait("Category", "Integration")]
public class PulsarProducerSharingIntegrationTest(ITestOutputHelper outputHelper) : IAsyncDisposable
{
    // PublisherBuilder's default send timeout, part of the shared producer's cache key.
    private static readonly TimeSpan DefaultSendTimeout = TimeSpan.FromMinutes(2);

    private readonly IHost _host = HostTestUtil.GetPulsarHost(outputHelper);
    private readonly string _topicSuffix = $"producers_{Guid.NewGuid():N}";

    /// <summary>
    /// Aggregators and senders build a publisher per instance, and callers build those per
    /// message batch or request. Every publisher of a topic must reuse one broker producer.
    /// </summary>
    [Fact]
    public async Task PublishersOfOneTopicShareOneBrokerProducer()
    {
        var streamingClient = _host.Services.GetRequiredService<StreamingClient>();

        for (var i = 0; i < 25; i++)
        {
            var publisher = streamingClient.CreatePublisher<Event>()
                .AddStream<Feed1PriceChanged>(_topicSuffix)
                .Build();
            await publisher.PublishAsync(new Event($"stream-{i}", 1, new Feed1PriceChanged($"stream-{i}", i)));
        }

        Assert.Equal(1, await GetBrokerProducerCountAsync());
    }

    /// <summary>
    /// A shared producer that can never send again (closed, or its topic terminated) must not
    /// stay cached: the failing publish reports the error and the next one gets a new producer.
    /// </summary>
    [Fact]
    public async Task ClosedSharedProducerIsReplacedOnTheNextPublish()
    {
        var publisher = _host.Services.GetRequiredService<StreamingClient>().CreatePublisher<Event>()
            .AddStream<Feed1PriceChanged>(_topicSuffix)
            .Build();
        await publisher.PublishAsync(new Event("stream-1", 1, new Feed1PriceChanged("stream-1", 1)));

        var shared = await _host.Services.GetRequiredService<PulsarClientResolver>().GetOrAddProducerAsync<IProducer<Event>>(
            typeof(Event), GetTopic(), DefaultSendTimeout, () => throw new InvalidOperationException("producer should be cached"));
        await shared.DisposeAsync();

        await Assert.ThrowsAsync<AlreadyClosedException>(
            () => publisher.PublishAsync(new Event("stream-1", 2, new Feed1PriceChanged("stream-1", 2))));
        await publisher.PublishAsync(new Event("stream-1", 3, new Feed1PriceChanged("stream-1", 3)));

        Assert.Equal(1, await GetBrokerProducerCountAsync());
    }

    private async Task<int> GetBrokerProducerCountAsync()
    {
        var topic = GetTopic();
        var admin = _host.Services.GetRequiredService<PulsarClientResolver>().GetAdminHttpClient();
        var stats = await admin.GetFromJsonAsync<JsonElement>(
            $"{topic.Replace("persistent://", "persistent/", StringComparison.Ordinal)}/stats");
        return stats.GetProperty("publishers").GetArrayLength();
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
