using System.Threading;
using System.Threading.Tasks;
using Insperex.EventHorizon.Abstractions.Interfaces.Internal;
using Insperex.EventHorizon.EventStreaming.Pulsar.Interfaces;
using Insperex.EventHorizon.EventStreaming.Pulsar.Models;

namespace Insperex.EventHorizon.EventStreaming.Pulsar.Utils;

public class PulsarAdminKeyHashRangeProvider<T>: IPulsarKeyHashRangeProvider
    where T: ITopicMessage
{
    private PulsarTopicAdmin<T> _admin;

    public PulsarAdminKeyHashRangeProvider(PulsarTopicAdmin<T> admin)
    {
        _admin = admin;
    }

    public async Task<PulsarKeyHashRanges> GetSubscriptionHashRanges(string topic, string subscription,
        string consumer, CancellationToken ct)
    {
        return await _admin.GetTopicConsumerKeyHashRanges(topic, subscription, consumer, ct);
    }
}
