using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Models.TopicMessages;
using Insperex.EventHorizon.EventStreaming.Interfaces.Streaming;
using Insperex.EventHorizon.EventStreaming.Samples.Models;
using Insperex.EventHorizon.EventStreaming.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Insperex.EventHorizon.EventStreaming.Samples.Handlers;

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
            .Select(x => x.GetPayload())
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
