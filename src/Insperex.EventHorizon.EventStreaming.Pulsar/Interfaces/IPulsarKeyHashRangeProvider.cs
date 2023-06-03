using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Interfaces;

public interface IPulsarKeyHashRangeProvider
{
    Task<PulsarKeyHashRanges> GetSubscriptionHashRanges(string topic, string subscription, string consumer,
        CancellationToken ct);
}
