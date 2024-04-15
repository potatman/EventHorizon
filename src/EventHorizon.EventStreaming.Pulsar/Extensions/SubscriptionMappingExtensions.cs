using System;
using Pulsar.Client.Common;

namespace EventHorizon.EventStreaming.Pulsar.Extensions;

public static class SubscriptionMappingExtensions
{
    public static SubscriptionType ToPulsarSubscriptionType(this global::EventHorizon.Abstractions.Models.SubscriptionType subscriptionType)
    {
        return subscriptionType switch
        {
            global::EventHorizon.Abstractions.Models.SubscriptionType.Exclusive => SubscriptionType.Exclusive,
            global::EventHorizon.Abstractions.Models.SubscriptionType.Shared => SubscriptionType.Shared,
            global::EventHorizon.Abstractions.Models.SubscriptionType.Failover => SubscriptionType.Failover,
            global::EventHorizon.Abstractions.Models.SubscriptionType.KeyShared => SubscriptionType.KeyShared,
            _ => throw new ArgumentOutOfRangeException(nameof(subscriptionType))
        };
    }
}
