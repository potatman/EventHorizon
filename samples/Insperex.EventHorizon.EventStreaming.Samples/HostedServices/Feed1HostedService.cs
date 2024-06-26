﻿using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Microsoft.Extensions.Hosting;

namespace Insperex.EventHorizon.EventStreaming.Samples.HostedServices;

public class Feed1HostedService : IHostedService
{
    private readonly StreamingClient _streamingClient;

    public Feed1HostedService(StreamingClient streamingClient)
    {
        _streamingClient = streamingClient;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var priceChange = new Feed1PriceChanged("123", 100);
        using var publisher = _streamingClient.CreatePublisher<Event>()
            .AddStream<Feed1PriceChanged>()
            .Build()
            .PublishAsync(new Event(priceChange.Id, priceChange));

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}