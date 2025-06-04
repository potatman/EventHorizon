using System;
using Pulsar.Client.Common;

namespace EventHorizon.EventStreaming.Pulsar.Extensions;

public static class SubscriptionMappingExtensions
{
    public static SubscriptionType ToPulsarSubscriptionType(this Abstractions.Models.SubscriptionType subscriptionType)
    {
        return subscriptionType switch
        {
            Abstractions.Models.SubscriptionType.Exclusive => SubscriptionType.Exclusive,
            Abstractions.Models.SubscriptionType.Shared => SubscriptionType.Shared,
            Abstractions.Models.SubscriptionType.Failover => SubscriptionType.Failover,
            Abstractions.Models.SubscriptionType.KeyShared => SubscriptionType.KeyShared,
            _ => throw new ArgumentOutOfRangeException(nameof(subscriptionType))
        };
    }
}
