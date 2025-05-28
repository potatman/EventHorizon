using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventHorizon.Abstractions.Models.TopicMessages;
using EventHorizon.EventStreaming.Extensions;
using EventHorizon.EventStreaming.Interfaces.Streaming;
using EventHorizon.EventStreaming.Samples.Models;
using EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace EventHorizon.EventStreaming.Samples.Handlers;

public class PriceChangeTracker : IStreamConsumer<Event>
{
    private readonly List<PriceChanged> _priceChanges = new();
    private readonly ILogger<PriceChangeTracker> _logger;

    public PriceChangeTracker(ILogger<PriceChangeTracker> logger)
    {
        _logger = logger;
    }

    public Task OnBatch(SubscriptionContext<Event> context)
    {
        var changes = context.Messages
            .Select(x => x.Data.GetPayload())
            .ToArray();
        foreach (var change in changes)
        {
            switch (change)
            {
                case Feed1PriceChanged feed1PriceChanged: _priceChanges.Add(feed1PriceChanged); break;
                case Feed2PriceChanged feed2PriceChanged: _priceChanges.Add(feed2PriceChanged); break;
            }

            var priceChange = change as PriceChanged;
            _logger.LogInformation("{Type} {Id} = {Price}", priceChange.GetType().Name, priceChange.Id, priceChange.Price);

        }
        return Task.CompletedTask;
    }
}
