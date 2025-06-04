using System.Threading;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Samples.Models;
using Microsoft.Extensions.Hosting;

namespace EventHorizon.EventStreaming.Samples.HostedServices;

public class Feed2HostedService : IHostedService
{
    private readonly StreamingClient _streamingClient;

    public Feed2HostedService(StreamingClient streamingClient)
    {
        _streamingClient = streamingClient;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        var priceChange = new Feed2PriceChanged("123", 100);
        using var publisher = _streamingClient.CreatePublisher<Event>()
            .AddStream<Feed2PriceChanged>()
            .Build()
            .PublishAsync(new Event(priceChange.Id, priceChange));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
