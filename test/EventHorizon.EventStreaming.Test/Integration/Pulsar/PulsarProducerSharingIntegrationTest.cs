using System;
using System.Linq;
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
using Xunit;

namespace EventHorizon.EventStreaming.Test.Integration.Pulsar;

[Trait("Category", "Integration")]
public class PulsarProducerSharingIntegrationTest(ITestOutputHelper outputHelper) : IAsyncDisposable
{
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
            .GetTopics<Event>(typeof(Feed1PriceChanged), _topicSuffix).First();

    public async ValueTask DisposeAsync()
    {
        var admin = (PulsarTopicAdmin<Event>)_host.Services.GetRequiredService<IStreamFactory>().CreateAdmin<Event>();
        await admin.DeleteTopicAsync(GetTopic(), CancellationToken.None);
        _host.Dispose();
    }
}
